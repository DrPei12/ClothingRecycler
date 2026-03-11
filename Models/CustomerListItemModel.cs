namespace ClothingRecycler.Desktop.Models
{
    public sealed class CustomerListItemModel
    {
        public long Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public bool HasInboundOrders { get; init; }

        public bool HasOutboundOrders { get; init; }

        public int TotalOrderCount { get; init; }

        public int InboundOrderCount { get; init; }

        public int OutboundOrderCount { get; init; }

        public DateTimeOffset? LastTransactionAt { get; init; }

        public string DisplayName => string.IsNullOrWhiteSpace(Name)
            ? "\u672A\u547D\u540D\u5BA2\u6237"
            : Name;

        public string InboundStatusText => HasInboundOrders ? "\u6709\u5165\u5E93\u5355" : "\u65E0\u5165\u5E93\u5355";

        public string OutboundStatusText => HasOutboundOrders ? "\u6709\u51FA\u5E93\u5355" : "\u65E0\u51FA\u5E93\u5355";

        public string TotalOrderCountText => TotalOrderCount.ToString(CultureInfo.InvariantCulture);

        public string InboundOrderCountText => InboundOrderCount.ToString(CultureInfo.InvariantCulture);

        public string OutboundOrderCountText => OutboundOrderCount.ToString(CultureInfo.InvariantCulture);

        public string LastTransactionText => LastTransactionAt?.ToLocalTime().ToString("MM-dd HH:mm") ?? "\u6682\u65E0\u8BA2\u5355";
    }
}
