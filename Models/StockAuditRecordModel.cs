namespace ClothingRecycler.Desktop.Models
{
    public sealed class StockAuditRecordModel
    {
        public long Id { get; init; }

        public long CategoryId { get; init; }

        public string CategoryName { get; init; } = string.Empty;

        public double SystemQuantity { get; init; }

        public double ActualQuantity { get; init; }

        public double DeltaQuantity { get; init; }

        public WeightUnit UnitType { get; init; }

        public string Note { get; init; } = string.Empty;

        public DateTimeOffset Timestamp { get; init; }

        public string UnitLabel => UnitType switch
        {
            WeightUnit.Kilogram => "kg",
            WeightUnit.Jin => "\u65A4",
            WeightUnit.Piece => "\u4EF6",
            _ => "kg",
        };

        public string DeltaText => DeltaQuantity switch
        {
            > 0 => $"+{QuantityText(DeltaQuantity)}",
            < 0 => QuantityText(DeltaQuantity),
            _ => "\u5DEE\u5F02 0"
        };

        public string BeforeAfterText => $"{QuantityText(SystemQuantity)} \u2192 {QuantityText(ActualQuantity)}";

        public string TimestampText => Timestamp.ToLocalTime().ToString("MM-dd HH:mm");

        public string StatusText => Math.Abs(DeltaQuantity) < 0.0001 ? "\u76D8\u5E73" : "\u5DF2\u6821\u6B63";

        public string NoteText => string.IsNullOrWhiteSpace(Note) ? "\u672A\u586B\u5199\u76D8\u70B9\u5907\u6CE8" : Note.Trim();

        private string QuantityText(double quantity) => UnitType == WeightUnit.Piece
            ? $"{Math.Round(quantity):0} {UnitLabel}"
            : $"{quantity:0.##} {UnitLabel}";
    }
}
