namespace ClothingRecycler.Desktop.Models
{
    public sealed class InboundOrderConfirmationModel
    {
        public long OrderId { get; init; }

        public string OrderNumber { get; init; } = string.Empty;

        public string CustomerName { get; init; } = string.Empty;

        public DateTimeOffset Timestamp { get; init; }

        public int CategoryCount { get; init; }

        public int ItemCount { get; init; }

        public double TotalAmount { get; init; }

        public IReadOnlyList<OrderDetailItemModel> Items { get; init; } = Array.Empty<OrderDetailItemModel>();

        public string CustomerDisplayName => string.IsNullOrWhiteSpace(CustomerName)
            ? "匿名客户"
            : CustomerName;

        public string TimestampText => Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");

        public string CategoryCountText => CategoryCount.ToString(CultureInfo.InvariantCulture);

        public string ItemCountText => ItemCount.ToString(CultureInfo.InvariantCulture);

        public string TotalAmountText => $"¥{TotalAmount:0.##}";

        public string CategoriesText => Items.Count == 0
            ? "暂无明细"
            : string.Join("、", Items.Select(item => item.CategoryName));

        public string QuantitySummaryText
        {
            get
            {
                if (Items.Count == 0)
                {
                    return "0";
                }

                return string.Join(" / ",
                    Items
                        .GroupBy(item => item.UnitType)
                        .Select(group =>
                        {
                            var quantity = group.Sum(item => item.Quantity);
                            var unitLabel = group.First().UnitLabel;
                            return group.Key == WeightUnit.Piece
                                ? $"{Math.Round(quantity):0} {unitLabel}"
                                : $"{quantity:0.##} {unitLabel}";
                        }));
            }
        }
    }
}
