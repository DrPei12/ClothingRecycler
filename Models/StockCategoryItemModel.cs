namespace ClothingRecycler.Desktop.Models
{
    public sealed class StockCategoryItemModel
    {
        public CategoryModel Category { get; init; } = new();

        public double CalculatedInventoryCost { get; init; }

        public double CalculatedProjectedNetProfit { get; init; }

        public int PriceBucketCount { get; init; }

        public string BuyPriceRangeText { get; init; } = "¥0";

        public string PriceBucketSummaryText { get; init; } = "暂无库存";

        public int InboundRecordCount { get; init; }

        public int OutboundRecordCount { get; init; }

        public DateTimeOffset? LastInboundAt { get; init; }

        public DateTimeOffset? LastOutboundAt { get; init; }

        public DateTimeOffset? LastActivityAt =>
            LastInboundAt.HasValue || LastOutboundAt.HasValue
                ? new[] { LastInboundAt, LastOutboundAt }
                    .Where(value => value.HasValue)
                    .Select(value => value!.Value)
                    .Max()
                : null;

        public long Id => Category.Id;

        public string Name => Category.Name;

        public bool IsArchived => Category.IsArchived;

        public bool IsLowStock => Category.IsLowStock;

        public bool HasStock => Category.DisplayStock > 0.0001;

        public string UnitLabel => Category.UnitLabel;

        public double CurrentQuantity => Category.DisplayStock;

        public string DisplayStockText => Category.DisplayStockText;

        public string BuyPriceText => Category.BuyPriceText;

        public string SellPriceText => Category.SellPriceText;

        public string InventoryCostText => Currency(CalculatedInventoryCost);

        public string ProjectedNetProfitText => Currency(CalculatedProjectedNetProfit);

        public double LowStockThreshold => Category.UnitType == WeightUnit.Piece ? 10 : 5;

        public double SuggestedTargetQuantity => Category.UnitType == WeightUnit.Piece ? 30 : 15;

        public double SuggestedRestockQuantity => Math.Max(0, RoundQuantity(SuggestedTargetQuantity - CurrentQuantity));

        public bool CanQuickRestock => !IsArchived && IsLowStock;

        public string LastActivityText => LastActivityAt?.ToLocalTime().ToString("MM-dd HH:mm") ?? "暂无流水";

        public string LastInboundText => LastInboundAt?.ToLocalTime().ToString("MM-dd HH:mm") ?? "暂无";

        public string LastOutboundText => LastOutboundAt?.ToLocalTime().ToString("MM-dd HH:mm") ?? "暂无";

        public string ActivitySummaryText => $"入库 {InboundRecordCount} 笔 / 出库 {OutboundRecordCount} 笔";

        public string SuggestedTargetText => QuantityText(SuggestedTargetQuantity);

        public string SuggestedRestockText => QuantityText(SuggestedRestockQuantity);

        public string RestockHintText => IsArchived
            ? "已归档，不参与补货"
            : IsLowStock
                ? $"建议补 {SuggestedRestockText}，补至 {SuggestedTargetText}"
                : "当前库存充足";

        public string HealthText => IsArchived
            ? "已归档"
            : IsLowStock
                ? "低库存"
                : "库存正常";

        public double ItemOpacity => IsArchived ? 0.72 : 1.0;

        private double RoundQuantity(double quantity) => Category.UnitType == WeightUnit.Piece
            ? Math.Round(quantity)
            : Math.Round(quantity, 2);

        private string QuantityText(double quantity) => Category.UnitType == WeightUnit.Piece
            ? $"{Math.Round(quantity):0} {UnitLabel}"
            : $"{quantity:0.##} {UnitLabel}";

        private static string Currency(double value) => $"¥{value:0.##}";
    }
}
