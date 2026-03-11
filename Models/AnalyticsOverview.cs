namespace ClothingRecycler.Desktop.Models
{
    public sealed class AnalyticsOverview
    {
        public double TotalInboundAmount { get; init; }

        public double TotalOutboundAmount { get; init; }

        public double EstimatedMargin { get; init; }

        public int ActiveCustomerCount { get; init; }

        public int TotalOrderCount { get; init; }
    }
}
