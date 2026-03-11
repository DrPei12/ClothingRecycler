namespace ClothingRecycler.Desktop.Models
{
    public sealed class CustomerPriceMemoryItemModel
    {
        public long CategoryId { get; init; }

        public string CategoryName { get; init; } = string.Empty;

        public double Price { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }

        public string PriceText => $"\u00A5{Price:0.##}";

        public string UpdatedAtText => UpdatedAt.ToLocalTime().ToString("MM-dd HH:mm");
    }
}
