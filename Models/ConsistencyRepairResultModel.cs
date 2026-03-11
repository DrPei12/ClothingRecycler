namespace ClothingRecycler.Desktop.Models
{
    public sealed class ConsistencyRepairResultModel
    {
        public string RestorePointPath { get; init; } = string.Empty;

        public int ProcessedCustomerCount { get; init; }

        public ConsistencyCheckReportModel Report { get; init; } = new();
    }
}
