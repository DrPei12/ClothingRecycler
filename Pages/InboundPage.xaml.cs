namespace ClothingRecycler.Desktop.Pages
{
    public sealed partial class InboundPage : Page
    {
        public InboundPage()
        {
            ViewModel = App.GetService<InboundViewModel>();
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public InboundViewModel ViewModel { get; }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private async void OnSubmitClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await ViewModel.SubmitAsync();
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = Content.XamlRoot,
                    Title = "\u5165\u5E93\u5931\u8D25",
                    CloseButtonText = "\u786E\u5B9A",
                    DefaultButton = ContentDialogButton.Close,
                    Content = ex.Message
                };

                await dialog.ShowAsync();
            }
        }
    }
}
