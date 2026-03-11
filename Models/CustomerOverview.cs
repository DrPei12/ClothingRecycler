namespace ClothingRecycler.Desktop.Models
{
    public sealed class CustomerOverview
    {
        public int TotalCustomerCount { get; init; }

        public int InboundCustomerCount { get; init; }

        public int OutboundCustomerCount { get; init; }

        public int ActiveTodayCustomerCount { get; init; }
    }
}
