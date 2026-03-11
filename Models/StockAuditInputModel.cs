namespace ClothingRecycler.Desktop.Models
{
    public sealed class StockAuditInputModel
    {
        public long CategoryId { get; init; }

        public double ActualQuantity { get; init; }

        public string Note { get; init; } = string.Empty;
    }
}
