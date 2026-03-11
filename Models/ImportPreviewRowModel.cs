namespace ClothingRecycler.Desktop.Models
{
    public sealed class ImportPreviewRowModel
    {
        public int LineNumber { get; init; }

        public string Name { get; init; } = string.Empty;

        public string ActionText { get; init; } = string.Empty;

        public string DetailText { get; init; } = string.Empty;

        public string LineText => $"第 {LineNumber} 行";
    }
}
