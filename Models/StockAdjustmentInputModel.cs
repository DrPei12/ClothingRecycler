namespace ClothingRecycler.Desktop.Models
{
    public sealed class StockAdjustmentInputModel
    {
        public long CategoryId { get; init; }

        public double AdjustedQuantity { get; init; }

        public string Reason { get; init; } = string.Empty;
    }
}
