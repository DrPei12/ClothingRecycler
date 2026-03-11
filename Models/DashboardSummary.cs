namespace ClothingRecycler.Desktop.Models
{
    public sealed class DashboardSummary
    {
        public int CategoryCount { get; init; }

        public int LowStockCount { get; init; }

        public double TotalInventoryCost { get; init; }

        public double ForecastRevenue { get; init; }

        public double TodayInboundAmount { get; init; }

        public double TodayOutboundAmount { get; init; }
    }
}
