namespace ClothingRecycler.Desktop.Controls
{
    public sealed partial class OutboundOrderConfirmationDialog : ContentDialog
    {
        public OutboundOrderConfirmationDialog(OutboundOrderConfirmationModel confirmation)
        {
            Confirmation = confirmation;
            InitializeComponent();
        }

        public OutboundOrderConfirmationModel Confirmation { get; }
    }
}
