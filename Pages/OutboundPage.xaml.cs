namespace ClothingRecycler.Desktop.Pages
{
    public sealed partial class OutboundPage : Page
    {
        public OutboundPage()
        {
            ViewModel = App.GetService<OutboundViewModel>();
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public OutboundViewModel ViewModel { get; }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private void OnCategoryEntryHeaderClick(object sender, RoutedEventArgs e)
        {
            if (TryGetEntry(sender, out var entry))
            {
                ViewModel.ToggleEntry(entry);
            }
        }

        private async void OnEntrySubmitClick(object sender, RoutedEventArgs e)
        {
            if (!TryGetEntry(sender, out var entry))
            {
                return;
            }

            try
            {
                var confirmation = await ViewModel.SubmitAsync(entry);
                var window = new ClothingRecycler.Desktop.OrderWindows.OutboundOrderConfirmationWindow(confirmation);
                ClothingRecycler.Desktop.Helpers.SecondaryWindowManager.Show(
                    window,
                    "\u51FA\u5E93\u786E\u8BA4\u5355",
                    preferredWidth: 1520,
                    preferredHeight: 1000);
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "\u51FA\u5E93\u5931\u8D25",
                    CloseButtonText = "\u786E\u5B9A",
                    DefaultButton = ContentDialogButton.Close,
                    Content = ex.Message
                };

                await dialog.ShowAsync();
            }
        }

        private static bool TryGetEntry(object sender, out OutboundCategoryDraftModel entry)
        {
            entry = null!;

            if (sender is not FrameworkElement element || element.DataContext is not OutboundCategoryDraftModel model)
            {
                return false;
            }

            entry = model;
            return true;
        }
    }
}
