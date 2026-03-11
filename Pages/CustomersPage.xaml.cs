namespace ClothingRecycler.Desktop.Pages
{
    public sealed partial class CustomersPage : Page
    {
        public CustomersPage()
        {
            ViewModel = App.GetService<CustomersViewModel>();
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public CustomersViewModel ViewModel { get; }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private async void OnCustomerSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            await ViewModel.LoadSelectedCustomerDetailsAsync();
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private async void OnAddCustomerClick(object sender, RoutedEventArgs e)
        {
            var dialog = new CustomerEditorDialog
            {
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary || dialog.Result is null)
            {
                return;
            }

            try
            {
                await ViewModel.SaveCustomerAsync(dialog.Result);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u4FDD\u5B58\u5931\u8D25", ex.Message);
            }
        }

        private async void OnEditCustomerClick(object sender, RoutedEventArgs e)
        {
            var customer = await ViewModel.GetSelectedCustomerAsync();
            if (customer is null)
            {
                return;
            }

            var dialog = new CustomerEditorDialog(customer)
            {
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary || dialog.Result is null)
            {
                return;
            }

            try
            {
                await ViewModel.SaveCustomerAsync(dialog.Result);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u4FDD\u5B58\u5931\u8D25", ex.Message);
            }
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = title,
                Content = message,
                CloseButtonText = "\u786E\u5B9A"
            };

            await dialog.ShowAsync();
        }
    }
}
