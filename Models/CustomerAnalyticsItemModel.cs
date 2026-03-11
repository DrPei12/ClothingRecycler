namespace ClothingRecycler.Desktop.Models
{
    public sealed class CustomerAnalyticsItemModel
    {
        public long CustomerId { get; init; }

        public string CustomerName { get; init; } = string.Empty;

        public int OrderCount { get; init; }

        public double TotalAmount { get; init; }

        public DateTimeOffset? LastTransactionAt { get; init; }

        public string CustomerDisplayName => string.IsNullOrWhiteSpace(CustomerName)
            ? "\u672A\u547D\u540D\u5BA2\u6237"
            : CustomerName;

        public string OrderCountText => OrderCount.ToString(CultureInfo.InvariantCulture);

        public string TotalAmountText => $"\u00A5{TotalAmount:0.##}";

        public string LastTransactionText => LastTransactionAt?.ToLocalTime().ToString("MM-dd HH:mm") ?? "\u6682\u65E0";
    }
}
