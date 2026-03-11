namespace ClothingRecycler.Desktop.Models
{
    public sealed class ConsistencyCheckReportModel
    {
        private static readonly HashSet<string> AutoRepairableChecks =
        [
            "客户使用标记",
            "客户价格记忆"
        ];

        public DateTimeOffset GeneratedAt { get; init; } = DateTimeOffset.Now;

        public int CategoryCount { get; init; }

        public int CustomerCount { get; init; }

        public int OrderCount { get; init; }

        public IReadOnlyList<ConsistencyCheckItemModel> CheckItems { get; init; } = [];

        public IReadOnlyList<ConsistencyCheckIssueModel> Issues { get; init; } = [];

        public int IssueCount => Issues.Count;

        public bool IsHealthy => IssueCount == 0;

        public int RepairableIssueCount => Issues.Count(issue => AutoRepairableChecks.Contains(issue.CheckName));

        public bool HasRepairableIssues => RepairableIssueCount > 0;

        public string GeneratedAtText => GeneratedAt.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        public string SummaryMessage => IsHealthy
            ? "账本结构和关键派生数据都通过了本次自检。"
            : $"本次自检发现 {IssueCount} 项问题，建议先核对报告再决定是否修复。";

        public string AutoRepairHintMessage =>
            $"其中 {RepairableIssueCount} 项问题可通过重建客户使用标记和价格记忆自动修复，不会改动历史订单和历史流水。";

        public InfoBarSeverity SummarySeverity => IsHealthy ? InfoBarSeverity.Success : InfoBarSeverity.Warning;

        public Visibility AutoRepairHintVisibility => HasRepairableIssues ? Visibility.Visible : Visibility.Collapsed;

        public Visibility IssuesVisibility => Issues.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EmptyIssuesVisibility => Issues.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }
}
