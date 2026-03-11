namespace ClothingRecycler.Desktop.Models
{
    public sealed class DailyAnalyticsItemModel
    {
        public DateTime Date { get; init; }

        public double InboundAmount { get; init; }

        public double OutboundAmount { get; init; }

        public double EstimatedMargin => Math.Round(OutboundAmount - InboundAmount, 2);

        public string DayText => Date.ToString("MM-dd");

        public string InboundAmountText => $"\u00A5{InboundAmount:0.##}";

        public string OutboundAmountText => $"\u00A5{OutboundAmount:0.##}";

        public string EstimatedMarginText => $"\u00A5{EstimatedMargin:0.##}";
    }
}
