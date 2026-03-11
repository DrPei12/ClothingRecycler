namespace ClothingRecycler.Desktop.Pages
{
    public sealed partial class StockPage : Page
    {
        public StockPage()
        {
            ViewModel = App.GetService<StockViewModel>();
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public StockViewModel ViewModel { get; }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private async void OnFilterSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await ViewModel.LoadSelectedCategoryDetailsAsync();
        }

        private async void OnSearchTextChanged(object sender, TextChangedEventArgs e)
        {
            ViewModel.SearchText = SearchTextBox.Text;
            await ViewModel.LoadSelectedCategoryDetailsAsync();
        }

        private async void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            await ViewModel.LoadSelectedCategoryDetailsAsync();
        }

        private async void OnAdjustStockClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedCategory is null)
            {
                await ShowMessageAsync("\u6682\u65E0\u53EF\u8C03\u6574\u5206\u7C7B", "\u8BF7\u5148\u4ECE\u5DE6\u4FA7\u9009\u62E9\u4E00\u4E2A\u5206\u7C7B\u3002");
                return;
            }

            try
            {
                var dialog = new StockAdjustmentDialog(ViewModel.SelectedCategory)
                {
                    XamlRoot = XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Result is not null)
                {
                    await ViewModel.AdjustSelectedCategoryStockAsync(dialog.Result);
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u5E93\u5B58\u8C03\u6574\u5931\u8D25", ex.Message);
            }
        }

        private async void OnAuditStockClick(object sender, RoutedEventArgs e)
        {
            if (ViewModel.SelectedCategory is null)
            {
                await ShowMessageAsync("\u6682\u65E0\u53EF\u76D8\u70B9\u5206\u7C7B", "\u8BF7\u5148\u4ECE\u5DE6\u4FA7\u9009\u62E9\u4E00\u4E2A\u5206\u7C7B\u3002");
                return;
            }

            try
            {
                var dialog = new StockAuditDialog(ViewModel.SelectedCategory)
                {
                    XamlRoot = XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Result is not null)
                {
                    await ViewModel.AuditSelectedCategoryAsync(dialog.Result);
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u76D8\u70B9\u4FDD\u5B58\u5931\u8D25", ex.Message);
            }
        }

        private async void OnQuickRestockClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button button || button.Tag is not StockCategoryItemModel item)
            {
                return;
            }

            try
            {
                await ViewModel.FocusCategoryAsync(item.Id);

                var dialog = new StockAdjustmentDialog(
                    item,
                    initialAdjustedQuantity: item.SuggestedTargetQuantity,
                    defaultReason: "\u8865\u8D27\u81F3\u5EFA\u8BAE\u5E93\u5B58",
                    title: $"\u5FEB\u901F\u8865\u8D27 - {item.Name}",
                    primaryButtonText: "\u786E\u8BA4\u8865\u8D27")
                {
                    XamlRoot = XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Result is not null)
                {
                    await ViewModel.AdjustSelectedCategoryStockAsync(dialog.Result);
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u5FEB\u901F\u8865\u8D27\u5931\u8D25", ex.Message);
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
