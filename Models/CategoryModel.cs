namespace ClothingRecycler.Desktop.Models
{
    public sealed class CategoryModel
    {
        public long Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public double BuyPrice { get; set; }

        public double SellPrice { get; set; }

        public double Stock { get; set; }

        public double StockInJin { get; set; }

        public int StockInPieces { get; set; }

        public WeightUnit UnitType { get; set; } = WeightUnit.Kilogram;

        public bool IsArchived { get; set; }

        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

        public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

        public double? InventoryCostOverride { get; set; }

        public double? ForecastSalesAmountOverride { get; set; }

        public double? ForecastNetProfitOverride { get; set; }

        public int PriceBucketCount { get; set; }

        public string UnitLabel => UnitType switch
        {
            WeightUnit.Kilogram => "kg",
            WeightUnit.Jin => "\u65A4",
            WeightUnit.Piece => "\u4EF6",
            _ => "kg",
        };

        public double DisplayStock => UnitType switch
        {
            WeightUnit.Kilogram => Stock,
            WeightUnit.Jin => StockInJin,
            WeightUnit.Piece => StockInPieces,
            _ => Stock,
        };

        public string DisplayStockText => UnitType == WeightUnit.Piece
            ? $"{StockInPieces} {UnitLabel}"
            : $"{DisplayStock:0.##} {UnitLabel}";

        public double InventoryCost => Math.Round(InventoryCostOverride ?? (DisplayStock * BuyPrice), 2);

        public double ForecastSalesAmount => Math.Round(ForecastSalesAmountOverride ?? (DisplayStock * SellPrice), 2);

        public double ForecastNetProfit => Math.Round(ForecastNetProfitOverride ?? (ForecastSalesAmount - InventoryCost), 2);

        public double ForecastRevenue => ForecastNetProfit;

        public string BuyPriceText => Currency(BuyPrice);

        public string SellPriceText => Currency(SellPrice);

        public string InventoryCostText => Currency(InventoryCost);

        public string ForecastSalesAmountText => Currency(ForecastSalesAmount);

        public string ForecastNetProfitText => Currency(ForecastNetProfit);

        public string ForecastRevenueText => ForecastNetProfitText;

        public bool IsLowStock => UnitType == WeightUnit.Piece ? DisplayStock <= 10 : DisplayStock <= 5;

        private static string Currency(double value) => $"\u00A5{value:0.##}";
    }
}
