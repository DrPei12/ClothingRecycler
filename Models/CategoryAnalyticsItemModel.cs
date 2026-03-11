namespace ClothingRecycler.Desktop.Models
{
    public sealed class CategoryAnalyticsItemModel
    {
        public string CategoryName { get; init; } = string.Empty;

        public double Quantity { get; init; }

        public double Amount { get; init; }

        public WeightUnit UnitType { get; init; }

        public string UnitLabel => UnitType switch
        {
            WeightUnit.Kilogram => "kg",
            WeightUnit.Jin => "\u65A4",
            WeightUnit.Piece => "\u4EF6",
            _ => "kg",
        };

        public string QuantityText => UnitType == WeightUnit.Piece
            ? $"{Math.Round(Quantity):0} {UnitLabel}"
            : $"{Quantity:0.##} {UnitLabel}";

        public string AmountText => $"\u00A5{Amount:0.##}";
    }
}
