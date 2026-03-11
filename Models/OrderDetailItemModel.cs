namespace ClothingRecycler.Desktop.Models
{
    public sealed class OrderDetailItemModel
    {
        public long CategoryId { get; init; }

        public string CategoryName { get; init; } = string.Empty;

        public double Quantity { get; init; }

        public WeightUnit UnitType { get; init; }

        public double UnitPrice { get; init; }

        public double LineAmount { get; init; }

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

        public string UnitPriceText => $"\u00A5{UnitPrice:0.##}";

        public string LineAmountText => $"\u00A5{LineAmount:0.##}";
    }
}
