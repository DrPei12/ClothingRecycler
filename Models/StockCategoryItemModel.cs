namespace ClothingRecycler.Desktop.Models
{
    public sealed class StockCategoryItemModel
    {
        public CategoryModel Category { get; init; } = new();

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

        public string InventoryCostText => Category.InventoryCostText;

        public string PriceSpreadText => Currency(Math.Round(Category.SellPrice - Category.BuyPrice, 2));

        public double LowStockThreshold => Category.UnitType == WeightUnit.Piece ? 10 : 5;

        public double SuggestedTargetQuantity => Category.UnitType == WeightUnit.Piece ? 30 : 15;

        public double SuggestedRestockQuantity => Math.Max(0, RoundQuantity(SuggestedTargetQuantity - CurrentQuantity));

        public bool CanQuickRestock => !IsArchived && IsLowStock;

        public string LastActivityText => LastActivityAt?.ToLocalTime().ToString("MM-dd HH:mm") ?? "\u6682\u65E0\u6D41\u6C34";

        public string LastInboundText => LastInboundAt?.ToLocalTime().ToString("MM-dd HH:mm") ?? "\u6682\u65E0";

        public string LastOutboundText => LastOutboundAt?.ToLocalTime().ToString("MM-dd HH:mm") ?? "\u6682\u65E0";

        public string ActivitySummaryText =>
            $"\u5165\u5E93 {InboundRecordCount} \u7B14 / \u51FA\u5E93 {OutboundRecordCount} \u7B14";

        public string SuggestedTargetText => QuantityText(SuggestedTargetQuantity);

        public string SuggestedRestockText => QuantityText(SuggestedRestockQuantity);

        public string RestockHintText => IsArchived
            ? "\u5DF2\u5F52\u6863\uFF0C\u4E0D\u53C2\u4E0E\u8865\u8D27"
            : IsLowStock
                ? $"\u5EFA\u8BAE\u8865 {SuggestedRestockText}\uFF0C\u8865\u81F3 {SuggestedTargetText}"
                : "\u5F53\u524D\u5E93\u5B58\u5145\u8DB3";

        public string HealthText => IsArchived
            ? "\u5DF2\u5F52\u6863"
            : IsLowStock
                ? "\u4F4E\u5E93\u5B58"
                : "\u5E93\u5B58\u6B63\u5E38";

        public double ItemOpacity => IsArchived ? 0.72 : 1.0;

        private double RoundQuantity(double quantity) => Category.UnitType == WeightUnit.Piece
            ? Math.Round(quantity)
            : Math.Round(quantity, 2);

        private string QuantityText(double quantity) => Category.UnitType == WeightUnit.Piece
            ? $"{Math.Round(quantity):0} {UnitLabel}"
            : $"{quantity:0.##} {UnitLabel}";

        private static string Currency(double value) => $"\u00A5{value:0.##}";
    }
}
