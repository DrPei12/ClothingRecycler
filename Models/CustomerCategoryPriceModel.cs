namespace ClothingRecycler.Desktop.Models
{
    public sealed class CustomerCategoryPriceModel
    {
        public long CustomerId { get; init; }

        public long CategoryId { get; init; }

        public double Price { get; init; }

        public DateTimeOffset UpdatedAt { get; init; }
    }
}
