namespace ClothingRecycler.Desktop.Models
{
    public sealed class OrderListItemModel
    {
        public long Id { get; init; }

        public string OrderNumber { get; init; } = string.Empty;

        public string Type { get; init; } = string.Empty;

        public string CustomerName { get; init; } = string.Empty;

        public double TotalWeight { get; init; }

        public double TotalAmount { get; init; }

        public int CategoryCount { get; init; }

        public int ItemCount { get; init; }

        public DateTimeOffset Timestamp { get; init; }

        public string TypeText => string.Equals(Type, "outbound", StringComparison.OrdinalIgnoreCase)
            ? "\u51FA\u5E93"
            : "\u5165\u5E93";

        public string TypeBadgeText => string.Equals(Type, "outbound", StringComparison.OrdinalIgnoreCase)
            ? "\u9500\u552E"
            : "\u6536\u8D27";

        public string CustomerDisplayName => string.IsNullOrWhiteSpace(CustomerName)
            ? "\u533F\u540D"
            : CustomerName;

        public string TotalWeightText => $"{TotalWeight:0.##}";

        public string TotalAmountText => $"\u00A5{TotalAmount:0.##}";

        public string CategoryCountText => CategoryCount.ToString(CultureInfo.InvariantCulture);

        public string ItemCountText => ItemCount.ToString(CultureInfo.InvariantCulture);

        public string TimestampText => Timestamp.ToLocalTime().ToString("MM-dd HH:mm");
    }
}
