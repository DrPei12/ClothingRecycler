using System.Globalization;

namespace ClothingRecycler.Desktop.Models
{
    public sealed class PendingInboundOrderModel
    {
        public string OrderNumber { get; init; } = string.Empty;

        public string CustomerName { get; init; } = string.Empty;

        public DateTimeOffset Timestamp { get; init; }

        public IReadOnlyList<OrderDetailItemModel> Items { get; init; } = Array.Empty<OrderDetailItemModel>();

        public int CategoryCount => Items.Select(item => item.CategoryId).Distinct().Count();

        public int ItemCount => Items.Sum(item => item.UnitType == WeightUnit.Piece
            ? Math.Max(1, (int)Math.Round(item.Quantity))
            : 1);

        public double TotalAmount => Math.Round(Items.Sum(item => item.LineAmount), 2);

        public InboundOrderConfirmationModel ToConfirmationModel(long orderId = 0)
        {
            return new InboundOrderConfirmationModel
            {
                OrderId = orderId,
                OrderNumber = OrderNumber,
                CustomerName = CustomerName,
                Timestamp = Timestamp,
                CategoryCount = CategoryCount,
                ItemCount = ItemCount,
                TotalAmount = TotalAmount,
                Items = Items
            };
        }
    }
}
