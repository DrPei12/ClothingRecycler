namespace ClothingRecycler.Desktop.Controls
{
    public sealed partial class InboundOrderConfirmationDialog : ContentDialog
    {
        public InboundOrderConfirmationDialog(InboundOrderConfirmationModel confirmation)
        {
            Confirmation = confirmation;
            InitializeComponent();
        }

        public InboundOrderConfirmationModel Confirmation { get; }
    }
}
