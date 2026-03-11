namespace ClothingRecycler.Desktop.Models
{
    public sealed class CustomerModel
    {
        public long Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string Phone { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public string Address { get; init; } = string.Empty;

        public string Note { get; init; } = string.Empty;

        public bool HasInboundOrders { get; init; }

        public bool HasOutboundOrders { get; init; }

        public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.Now;

        public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.Now;

        public string DisplayName =>
            string.IsNullOrWhiteSpace(Name) ? "\u672A\u547D\u540D\u5BA2\u6237" : Name;

        public string PhoneDisplayText => string.IsNullOrWhiteSpace(Phone) ? "\u672A\u586B\u5199" : Phone.Trim();

        public string EmailDisplayText => string.IsNullOrWhiteSpace(Email) ? "\u672A\u586B\u5199" : Email.Trim();

        public string AddressDisplayText => string.IsNullOrWhiteSpace(Address) ? "\u672A\u586B\u5199" : Address.Trim();

        public string NoteDisplayText => string.IsNullOrWhiteSpace(Note) ? "\u6682\u65E0\u5907\u6CE8" : Note.Trim();
    }
}
