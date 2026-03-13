using System.Diagnostics;

using ClothingRecycler.Desktop.Models;

namespace ClothingRecycler.Desktop.OrderWindows
{
    public sealed partial class InboundOrderConfirmationWindow : Window
    {
        private readonly OrderExportService _orderExportService;
        private bool _isExporting;

        public InboundOrderConfirmationWindow(InboundOrderConfirmationModel confirmation)
        {
            Confirmation = confirmation;
            _orderExportService = App.GetService<OrderExportService>();
            InitializeComponent();
        }

        public InboundOrderConfirmationModel Confirmation { get; }

        private async void OnExportPdfClick(object sender, RoutedEventArgs e)
        {
            await ExportAsync(
                exporter: () => _orderExportService.ExportAsPdfAsync(DocumentRoot, Confirmation.OrderNumber),
                successTitle: "PDF 导出完成");
        }

        private async void OnExportPngClick(object sender, RoutedEventArgs e)
        {
            await ExportAsync(
                exporter: () => _orderExportService.ExportAsPngAsync(DocumentRoot, Confirmation.OrderNumber),
                successTitle: "PNG 导出完成");
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async Task ExportAsync(Func<Task<string>> exporter, string successTitle)
        {
            if (_isExporting)
            {
                return;
            }

            try
            {
                _isExporting = true;
                ExportPngButton.IsEnabled = false;
                ExportPdfButton.IsEnabled = false;

                var exportPath = await exporter();
                var dialog = new ContentDialog
                {
                    XamlRoot = RootLayout.XamlRoot,
                    Title = successTitle,
                    PrimaryButtonText = "打开目录",
                    CloseButtonText = "知道了",
                    DefaultButton = ContentDialogButton.Close,
                    Content = $"确认单已导出到：\n{exportPath}"
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    OpenContainingFolder(exportPath);
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = RootLayout.XamlRoot,
                    Title = "导出失败",
                    CloseButtonText = "确定",
                    DefaultButton = ContentDialogButton.Close,
                    Content = ex.Message
                };

                await dialog.ShowAsync();
            }
            finally
            {
                _isExporting = false;
                ExportPngButton.IsEnabled = true;
                ExportPdfButton.IsEnabled = true;
            }
        }

        private static void OpenContainingFolder(string filePath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
        }
    }
}
