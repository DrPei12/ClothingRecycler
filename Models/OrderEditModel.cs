namespace ClothingRecycler.Desktop.Models
{
    public sealed class OrderEditModel
    {
        public long OrderId { get; init; }

        public string OrderNumber { get; init; } = string.Empty;

        public string Type { get; init; } = string.Empty;

        public long? CustomerId { get; init; }

        public string CustomerName { get; init; } = string.Empty;

        public long CategoryId { get; init; }

        public double Quantity { get; init; }

        public double UnitPrice { get; init; }

        public WeightUnit UnitType { get; init; }

        public DateTimeOffset Timestamp { get; init; }

        public bool IsOutbound => string.Equals(Type, "outbound", StringComparison.OrdinalIgnoreCase);

        public string TypeText => IsOutbound ? "\u51FA\u5E93" : "\u5165\u5E93";
    }
}
