namespace ClothingRecycler.Desktop.Models
{
    public sealed class ImportPreviewIssueModel
    {
        public int LineNumber { get; init; }

        public string Message { get; init; } = string.Empty;

        public string LineText => LineNumber > 0 ? $"第 {LineNumber} 行" : "文件";
    }
}
