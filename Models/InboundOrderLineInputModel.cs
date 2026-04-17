namespace ClothingRecycler.Desktop.Models
{
    public sealed class InboundOrderLineInputModel
    {
        public long CategoryId { get; init; }

        public double Quantity { get; init; }

        public double UnitPrice { get; init; }

        public WeightUnit? InputUnitType { get; init; }
    }
}
