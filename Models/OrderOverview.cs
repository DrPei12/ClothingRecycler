namespace ClothingRecycler.Desktop.Models
{
    public sealed class OrderOverview
    {
        public int TotalOrderCount { get; init; }

        public int TodayOrderCount { get; init; }

        public int InboundOrderCount { get; init; }

        public int OutboundOrderCount { get; init; }
    }
}
