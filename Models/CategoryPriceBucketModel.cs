namespace ClothingRecycler.Desktop.Models
{
    public sealed class CategoryPriceBucketModel
    {
        public long CategoryId { get; init; }

        public string CategoryName { get; init; } = string.Empty;

        public WeightUnit UnitType { get; init; } = WeightUnit.Kilogram;

        public double UnitPrice { get; init; }

        public double Quantity { get; init; }

        public double SellPrice { get; init; }

        public bool IsEstimated { get; init; }

        public DateTimeOffset? LastInboundAt { get; init; }

        public string UnitLabel => UnitType switch
        {
            WeightUnit.Kilogram => "kg",
            WeightUnit.Jin => "斤",
            WeightUnit.Piece => "件",
            _ => "kg",
        };

        public double TotalCost => Math.Round(Quantity * UnitPrice, 2);

        public double ProjectedNetProfit => Math.Round((SellPrice - UnitPrice) * Quantity, 2);

        public string UnitPriceText => Currency(UnitPrice);

        public string QuantityText => UnitType == WeightUnit.Piece
            ? $"{Math.Round(Quantity):0} {UnitLabel}"
            : $"{Quantity:0.##} {UnitLabel}";

        public string TotalCostText => Currency(TotalCost);

        public string ProjectedNetProfitText => Currency(ProjectedNetProfit);

        public string SourceText => IsEstimated
            ? "按当前参考进价估算"
            : "来自真实入库进价";

        public string LastInboundText => LastInboundAt?.ToLocalTime().ToString("MM-dd HH:mm") ?? "库存调整补入";

        private static string Currency(double value) => $"¥{value:0.##}";
    }
}
