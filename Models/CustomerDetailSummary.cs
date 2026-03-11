namespace ClothingRecycler.Desktop.Models
{
    public sealed class CustomerDetailSummary
    {
        public int TotalOrderCount { get; init; }

        public int InboundOrderCount { get; init; }

        public int OutboundOrderCount { get; init; }

        public double InboundAmount { get; init; }

        public double OutboundAmount { get; init; }

        public DateTimeOffset? LastTransactionAt { get; init; }
    }
}
