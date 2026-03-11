namespace ClothingRecycler.Desktop.Models
{
    public sealed class StockAdjustmentRecordModel
    {
        public long Id { get; init; }

        public long CategoryId { get; init; }

        public string CategoryName { get; init; } = string.Empty;

        public double PreviousQuantity { get; init; }

        public double AdjustedQuantity { get; init; }

        public double DeltaQuantity { get; init; }

        public WeightUnit UnitType { get; init; }

        public string Reason { get; init; } = string.Empty;

        public DateTimeOffset Timestamp { get; init; }

        public string UnitLabel => UnitType switch
        {
            WeightUnit.Kilogram => "kg",
            WeightUnit.Jin => "\u65A4",
            WeightUnit.Piece => "\u4EF6",
            _ => "kg",
        };

        public string DeltaText => $"{(DeltaQuantity >= 0 ? "+" : string.Empty)}{QuantityText(DeltaQuantity)}";

        public string BeforeAfterText => $"{QuantityText(PreviousQuantity)} \u2192 {QuantityText(AdjustedQuantity)}";

        public string ReasonText => string.IsNullOrWhiteSpace(Reason) ? "\u624B\u52A8\u6821\u6B63" : Reason.Trim();

        public string TimestampText => Timestamp.ToLocalTime().ToString("MM-dd HH:mm");

        public string DirectionText => DeltaQuantity switch
        {
            > 0 => "\u8C03\u589E",
            < 0 => "\u8C03\u51CF",
            _ => "\u6821\u5E73"
        };

        private string QuantityText(double quantity) => UnitType == WeightUnit.Piece
            ? $"{Math.Round(quantity):0} {UnitLabel}"
            : $"{quantity:0.##} {UnitLabel}";
    }
}
