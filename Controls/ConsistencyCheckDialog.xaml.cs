namespace ClothingRecycler.Desktop.Controls
{
    public sealed partial class ConsistencyCheckDialog : ContentDialog
    {
        private readonly bool _allowRepair;

        public ConsistencyCheckDialog(ConsistencyCheckReportModel report, bool allowRepair = true)
        {
            Report = report;
            _allowRepair = allowRepair;
            InitializeComponent();
            Title = "账本一致性检查";

            if (allowRepair && report.HasRepairableIssues)
            {
                PrimaryButtonText = "修复可自动重建项";
                CloseButtonText = "暂不修复";
            }
            else
            {
                CloseButtonText = "关闭";
            }

            DefaultButton = ContentDialogButton.Close;
        }

        public ConsistencyCheckReportModel Report { get; }

        public Visibility AutoRepairHintVisibility =>
            _allowRepair && Report.HasRepairableIssues ? Visibility.Visible : Visibility.Collapsed;
    }
}
