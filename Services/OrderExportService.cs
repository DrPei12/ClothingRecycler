using System.Globalization;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media.Imaging;

using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace ClothingRecycler.Desktop.Services
{
    public sealed class OrderExportService
    {
        private const int BytesPerPixel = 4;
        private const double PdfMargin = 24d;
        private const double A4PortraitWidth = 595d;
        private const double A4PortraitHeight = 842d;
        private const double A4LandscapeWidth = 842d;
        private const double A4LandscapeHeight = 595d;

        private readonly AppLogger _logger;

        public OrderExportService(AppLogger logger)
        {
            _logger = logger;
        }

        public async Task<string> ExportAsPngAsync(FrameworkElement element, string orderName)
        {
            var snapshot = await CaptureAsync(element);
            var filePath = CreateUniqueExportPath(orderName, ".png");
            await SaveBitmapAsync(snapshot, BitmapEncoder.PngEncoderId, filePath);
            await _logger.LogInfoAsync($"Order PNG exported: {filePath}");
            return filePath;
        }

        public async Task<string> ExportAsPdfAsync(FrameworkElement element, string orderName)
        {
            var snapshot = await CaptureAsync(element);
            var filePath = CreateUniqueExportPath(orderName, ".pdf");
            await SavePdfAsync(snapshot, filePath);
            await _logger.LogInfoAsync($"Order PDF exported: {filePath}");
            return filePath;
        }

        private static async Task<RenderedElementSnapshot> CaptureAsync(FrameworkElement element)
        {
            element.UpdateLayout();

            if (element.ActualWidth < 1 || element.ActualHeight < 1)
            {
                throw new InvalidOperationException("确认单尚未完成布局，请稍后再试。");
            }

            var renderer = new RenderTargetBitmap();
            await renderer.RenderAsync(element);
            var pixels = (await renderer.GetPixelsAsync()).ToArray();

            if (renderer.PixelWidth <= 0 || renderer.PixelHeight <= 0 || pixels.Length == 0)
            {
                throw new InvalidOperationException("确认单导出失败，未能生成可用图像。");
            }

            return new RenderedElementSnapshot(renderer.PixelWidth, renderer.PixelHeight, pixels);
        }

        private static async Task SaveBitmapAsync(RenderedElementSnapshot snapshot, Guid encoderId, string filePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(encoderId, stream);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                (uint)snapshot.Width,
                (uint)snapshot.Height,
                dpiX: 96,
                dpiY: 96,
                snapshot.Pixels);
            await encoder.FlushAsync();

            stream.Seek(0);
            await using var output = File.Create(filePath);
            await stream.AsStreamForRead().CopyToAsync(output);
        }

        private static async Task SavePdfAsync(RenderedElementSnapshot snapshot, string filePath)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);

            var (pageWidth, pageHeight) = snapshot.Width >= snapshot.Height
                ? (A4LandscapeWidth, A4LandscapeHeight)
                : (A4PortraitWidth, A4PortraitHeight);

            var drawableWidth = pageWidth - (PdfMargin * 2);
            var drawableHeight = pageHeight - (PdfMargin * 2);
            var scale = drawableWidth / snapshot.Width;
            var maxSliceHeight = Math.Max(1, (int)Math.Floor(drawableHeight / scale));

            var slices = new List<PdfImageSlice>();
            for (var startRow = 0; startRow < snapshot.Height; startRow += maxSliceHeight)
            {
                var sliceHeight = Math.Min(maxSliceHeight, snapshot.Height - startRow);
                var slicePixels = CopySlicePixels(snapshot, startRow, sliceHeight);
                var jpegBytes = await EncodeSliceAsJpegAsync(snapshot.Width, sliceHeight, slicePixels);
                slices.Add(new PdfImageSlice(snapshot.Width, sliceHeight, jpegBytes));
            }

            var documentBytes = BuildPdfDocument(slices, pageWidth, pageHeight, drawableWidth);
            await File.WriteAllBytesAsync(filePath, documentBytes);
        }

        private static byte[] CopySlicePixels(RenderedElementSnapshot snapshot, int startRow, int rowCount)
        {
            var stride = snapshot.Width * BytesPerPixel;
            var slice = new byte[stride * rowCount];
            System.Buffer.BlockCopy(snapshot.Pixels, startRow * stride, slice, 0, slice.Length);
            return slice;
        }

        private static async Task<byte[]> EncodeSliceAsJpegAsync(int width, int height, byte[] pixels)
        {
            using var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                (uint)width,
                (uint)height,
                dpiX: 96,
                dpiY: 96,
                pixels);
            await encoder.FlushAsync();

            stream.Seek(0);
            using var output = new MemoryStream();
            await stream.AsStreamForRead().CopyToAsync(output);
            return output.ToArray();
        }

        private static byte[] BuildPdfDocument(IReadOnlyList<PdfImageSlice> slices, double pageWidth, double pageHeight, double drawableWidth)
        {
            using var stream = new MemoryStream();
            var offsets = new List<long> { 0 };

            WriteAscii(stream, "%PDF-1.4\n");

            var pageObjectNumbers = new List<int>(slices.Count);

            WriteObject(stream, offsets, 1, $"<< /Type /Catalog /Pages 2 0 R >>");

            for (var index = 0; index < slices.Count; index++)
            {
                pageObjectNumbers.Add(3 + (index * 3));
            }

            WriteObject(
                stream,
                offsets,
                2,
                $"<< /Type /Pages /Count {slices.Count} /Kids [{string.Join(" ", pageObjectNumbers.Select(number => $"{number} 0 R"))}] >>");

            for (var index = 0; index < slices.Count; index++)
            {
                var slice = slices[index];
                var pageObjectNumber = 3 + (index * 3);
                var contentObjectNumber = pageObjectNumber + 1;
                var imageObjectNumber = pageObjectNumber + 2;
                var imageName = $"Im{index + 1}";
                var displayHeight = slice.Height * (drawableWidth / slice.Width);
                var y = pageHeight - PdfMargin - displayHeight;

                var pageObject =
                    FormattableString.Invariant(
                        $"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {pageWidth:0.###} {pageHeight:0.###}] /Resources << /XObject << /{imageName} {imageObjectNumber} 0 R >> >> /Contents {contentObjectNumber} 0 R >>");
                WriteObject(stream, offsets, pageObjectNumber, pageObject);

                var contentStream =
                    FormattableString.Invariant(
                        $"q\n{drawableWidth:0.###} 0 0 {displayHeight:0.###} {PdfMargin:0.###} {y:0.###} cm\n/{imageName} Do\nQ\n");
                WriteStreamObject(stream, offsets, contentObjectNumber, Encoding.ASCII.GetBytes(contentStream));

                var imageHeader =
                    $"<< /Type /XObject /Subtype /Image /Width {slice.Width} /Height {slice.Height} /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /DCTDecode /Length {slice.JpegBytes.Length} >>";
                WriteStreamObject(stream, offsets, imageObjectNumber, slice.JpegBytes, imageHeader);
            }

            var startXref = stream.Position;
            WriteAscii(stream, $"xref\n0 {offsets.Count}\n");
            WriteAscii(stream, "0000000000 65535 f \n");
            foreach (var offset in offsets.Skip(1))
            {
                WriteAscii(stream, $"{offset:0000000000} 00000 n \n");
            }

            WriteAscii(stream, $"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{startXref}\n%%EOF");
            return stream.ToArray();
        }

        private static void WriteObject(Stream stream, ICollection<long> offsets, int objectNumber, string body)
        {
            offsets.Add(stream.Position);
            WriteAscii(stream, $"{objectNumber} 0 obj\n{body}\nendobj\n");
        }

        private static void WriteStreamObject(Stream stream, ICollection<long> offsets, int objectNumber, byte[] payload, string? header = null)
        {
            offsets.Add(stream.Position);
            var objectHeader = header ?? $"<< /Length {payload.Length} >>";
            WriteAscii(stream, $"{objectNumber} 0 obj\n{objectHeader}\nstream\n");
            stream.Write(payload, 0, payload.Length);
            WriteAscii(stream, "\nendstream\nendobj\n");
        }

        private static void WriteAscii(Stream stream, string value)
        {
            var buffer = Encoding.ASCII.GetBytes(value);
            stream.Write(buffer, 0, buffer.Length);
        }

        private static string CreateUniqueExportPath(string orderName, string extension)
        {
            AppDataPaths.EnsureDirectories();

            var exportDirectory = Path.Combine(AppDataPaths.ExportDirectory, "orders");
            Directory.CreateDirectory(exportDirectory);

            var safeName = SanitizeFileName(orderName);
            var path = Path.Combine(exportDirectory, safeName + extension);
            var suffix = 2;

            while (File.Exists(path))
            {
                path = Path.Combine(exportDirectory, $"{safeName}_{suffix}{extension}");
                suffix++;
            }

            return path;
        }

        private static string SanitizeFileName(string value)
        {
            var sanitized = string.IsNullOrWhiteSpace(value) ? "订单确认单" : value.Trim();
            foreach (var invalidCharacter in Path.GetInvalidFileNameChars())
            {
                sanitized = sanitized.Replace(invalidCharacter, '_');
            }

            sanitized = sanitized.Trim().TrimEnd('.', ' ');
            return string.IsNullOrWhiteSpace(sanitized) ? "订单确认单" : sanitized;
        }

        private sealed record RenderedElementSnapshot(int Width, int Height, byte[] Pixels);

        private sealed record PdfImageSlice(int Width, int Height, byte[] JpegBytes);
    }
}
