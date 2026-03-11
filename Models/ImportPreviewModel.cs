namespace ClothingRecycler.Desktop.Models
{
    public sealed class ImportPreviewModel
    {
        public string EntityDisplayName { get; init; } = string.Empty;

        public string SourcePath { get; init; } = string.Empty;

        public int TotalRowCount { get; init; }

        public int ReadyToImportCount { get; init; }

        public int InsertCount { get; init; }

        public int UpdateCount { get; init; }

        public int SkippedCount { get; init; }

        public IReadOnlyList<ImportPreviewRowModel> Rows { get; init; } = [];

        public IReadOnlyList<ImportPreviewIssueModel> Issues { get; init; } = [];

        public string SourceFileName => Path.GetFileName(SourcePath);

        public int IssueCount => Issues.Count;

        public bool CanImport => ReadyToImportCount > 0 && IssueCount == 0;

        public string SummaryMessage => CanImport
            ? $"已识别 {ReadyToImportCount} 条有效记录，确认后会直接写入本地数据。"
            : IssueCount > 0
                ? $"发现 {IssueCount} 个问题，修正 CSV 后再导入会更稳妥。"
                : "没有识别到可导入的有效记录。";

        public InfoBarSeverity SummarySeverity => CanImport
            ? InfoBarSeverity.Success
            : IssueCount > 0
                ? InfoBarSeverity.Warning
                : InfoBarSeverity.Informational;

        public Visibility RowsVisibility => Rows.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EmptyRowsVisibility => Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility IssuesVisibility => Issues.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EmptyIssuesVisibility => Issues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
