namespace ClothingRecycler.Desktop.Controls
{
    public sealed partial class CustomerEditorDialog : ContentDialog
    {
        private readonly CustomerModel? _existingCustomer;

        public CustomerEditorDialog(CustomerModel? existingCustomer = null)
        {
            InitializeComponent();
            _existingCustomer = existingCustomer;
            Title = existingCustomer is null ? "\u65B0\u589E\u5BA2\u6237" : "\u7F16\u8F91\u5BA2\u6237";

            if (existingCustomer is null)
            {
                return;
            }

            NameTextBox.Text = existingCustomer.Name;
            PhoneTextBox.Text = existingCustomer.Phone;
            EmailTextBox.Text = existingCustomer.Email;
            AddressTextBox.Text = existingCustomer.Address;
            NoteTextBox.Text = existingCustomer.Note;
        }

        public CustomerModel? Result { get; private set; }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var name = NameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowError("\u8BF7\u8F93\u5165\u5BA2\u6237\u540D\u79F0\u3002");
                args.Cancel = true;
                return;
            }

            Result = new CustomerModel
            {
                Id = _existingCustomer?.Id ?? 0,
                Name = name,
                Phone = PhoneTextBox.Text.Trim(),
                Email = EmailTextBox.Text.Trim(),
                Address = AddressTextBox.Text.Trim(),
                Note = NoteTextBox.Text.Trim(),
                HasInboundOrders = _existingCustomer?.HasInboundOrders ?? false,
                HasOutboundOrders = _existingCustomer?.HasOutboundOrders ?? false,
                CreatedAt = _existingCustomer?.CreatedAt ?? DateTimeOffset.Now,
                UpdatedAt = DateTimeOffset.Now
            };
        }

        private void ShowError(string message)
        {
            ValidationInfoBar.Message = message;
            ValidationInfoBar.IsOpen = true;
        }
    }
}
