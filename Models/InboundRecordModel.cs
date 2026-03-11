namespace ClothingRecycler.Desktop.Models
{
    public sealed class InboundRecordModel
    {
        public long Id { get; init; }

        public long CategoryId { get; init; }

        public string CategoryName { get; init; } = string.Empty;

        public string CustomerName { get; init; } = string.Empty;

        public double Quantity { get; init; }

        public WeightUnit UnitType { get; init; }

        public double UnitPrice { get; init; }

        public double TotalCost { get; init; }

        public DateTimeOffset Timestamp { get; init; }

        public string UnitLabel => UnitType switch
        {
            WeightUnit.Kilogram => "kg",
            WeightUnit.Jin => "\u65A4",
            WeightUnit.Piece => "\u4EF6",
            _ => "kg",
        };

        public string CustomerDisplayName => string.IsNullOrWhiteSpace(CustomerName)
            ? "\u533F\u540D"
            : CustomerName;

        public string QuantityText => UnitType == WeightUnit.Piece
            ? $"{Math.Round(Quantity):0} {UnitLabel}"
            : $"{Quantity:0.##} {UnitLabel}";

        public string UnitPriceText => Currency(UnitPrice);

        public string TotalCostText => Currency(TotalCost);

        public string TimestampText => Timestamp.ToLocalTime().ToString("MM-dd HH:mm");

        private static string Currency(double value) => $"\u00A5{value:0.##}";
    }
}
