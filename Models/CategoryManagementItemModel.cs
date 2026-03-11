namespace ClothingRecycler.Desktop.Models
{
    public sealed class CategoryManagementItemModel
    {
        public CategoryModel Category { get; init; } = new();

        public int InboundRecordCount { get; init; }

        public int OutboundRecordCount { get; init; }

        public double TotalInboundQuantity { get; init; }

        public double TotalOutboundQuantity { get; init; }

        public DateTimeOffset? LastActivityAt { get; init; }

        public long Id => Category.Id;

        public string Name => Category.Name;

        public string UnitLabel => Category.UnitLabel;

        public string BuyPriceText => Category.BuyPriceText;

        public string SellPriceText => Category.SellPriceText;

        public string DisplayStockText => Category.DisplayStockText;

        public bool IsArchived => Category.IsArchived;

        public bool HasHistory => InboundRecordCount > 0 || OutboundRecordCount > 0;

        public bool HasStock => Category.DisplayStock > 0.0001;

        public bool CanDelete => !HasHistory && !HasStock;

        public string StatusText => IsArchived ? "\u5DF2\u5F52\u6863" : "\u542F\u7528\u4E2D";

        public string ArchiveActionText => IsArchived ? "\u542F\u7528" : "\u5F52\u6863";

        public string LastActivityText => LastActivityAt?.ToLocalTime().ToString("MM-dd HH:mm") ?? "\u6682\u65E0\u6D41\u6C34";

        public string QuantitySummaryText => $"\u5165\u5E93 {QuantityText(TotalInboundQuantity)} / \u51FA\u5E93 {QuantityText(TotalOutboundQuantity)}";

        public string RecordSummaryText =>
            $"{InboundRecordCount + OutboundRecordCount} \u7B14\u8BB0\u5F55  |  \u6700\u540E\u6D41\u6C34 {LastActivityText}";

        public double ItemOpacity => IsArchived ? 0.72 : 1.0;

        private string QuantityText(double quantity) => Category.UnitType == WeightUnit.Piece
            ? $"{Math.Round(quantity):0} {UnitLabel}"
            : $"{quantity:0.##} {UnitLabel}";
    }
}
