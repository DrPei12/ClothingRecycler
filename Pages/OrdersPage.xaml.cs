using System.Diagnostics;

namespace ClothingRecycler.Desktop.Pages
{
    public sealed partial class OrdersPage : Page
    {
        private readonly OrderExportService _orderExportService;
        private bool _isExporting;

        public OrdersPage()
        {
            ViewModel = App.GetService<OrdersViewModel>();
            _orderExportService = App.GetService<OrderExportService>();
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public OrdersViewModel ViewModel { get; }

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
            if (!IsLoaded)
            {
                return;
            }

            await ViewModel.LoadAsync();
        }

        private async void OnOrderSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!IsLoaded)
            {
                return;
            }

            await ViewModel.LoadSelectedOrderDetailsAsync();
        }

        private async void OnEditOrderClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var order = await ViewModel.GetSelectedOrderEditModelAsync();
                if (order is null)
                {
                    return;
                }

                var categoriesTask = ViewModel.GetCategoriesAsync();
                var customersTask = ViewModel.GetCustomersAsync();
                var memoriesTask = ViewModel.GetCustomerCategoryPricesAsync();
                await Task.WhenAll(categoriesTask, customersTask, memoriesTask);

                var dialog = new OrderEditorDialog(order, categoriesTask.Result, customersTask.Result, memoriesTask.Result)
                {
                    XamlRoot = XamlRoot
                };

                if (await dialog.ShowAsync() != ContentDialogResult.Primary || dialog.Result is null)
                {
                    return;
                }

                await ViewModel.UpdateOrderAsync(dialog.Result);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u7F16\u8F91\u5931\u8D25", ex.Message);
            }
        }

        private async void OnDeleteOrderClick(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.HasSelectedOrder)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "\u5220\u9664\u8BA2\u5355",
                Content = "\u5220\u9664\u540E\u4F1A\u540C\u6B65\u56DE\u6EDA\u5E93\u5B58\u3001\u5BA2\u6237\u72B6\u6001\u548C\u4EF7\u683C\u8BB0\u5FC6\u3002\u8FD9\u4E2A\u64CD\u4F5C\u4E0D\u53EF\u64A4\u9500\uff0C\u786E\u5B9A\u7EE7\u7EED\u5417\uff1F",
                PrimaryButtonText = "\u5220\u9664",
                CloseButtonText = "\u53D6\u6D88",
                DefaultButton = ContentDialogButton.Close
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            try
            {
                await ViewModel.DeleteSelectedOrderAsync();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u5220\u9664\u5931\u8D25", ex.Message);
            }
        }

        private async void OnExportPngClick(object sender, RoutedEventArgs e)
        {
            await ExportSelectedOrderAsync(
                exporter: () => _orderExportService.ExportAsPngAsync(OrderExportRoot, ViewModel.SelectedOrder!.OrderNumber),
                successTitle: "PNG 导出完成");
        }

        private async void OnExportPdfClick(object sender, RoutedEventArgs e)
        {
            await ExportSelectedOrderAsync(
                exporter: () => _orderExportService.ExportAsPdfAsync(OrderExportRoot, ViewModel.SelectedOrder!.OrderNumber),
                successTitle: "PDF 导出完成");
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

        private async Task ExportSelectedOrderAsync(Func<Task<string>> exporter, string successTitle)
        {
            if (!ViewModel.HasSelectedOrder || _isExporting)
            {
                return;
            }

            try
            {
                _isExporting = true;
                await Task.Yield();

                var exportPath = await exporter();
                var dialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = successTitle,
                    PrimaryButtonText = "打开目录",
                    CloseButtonText = "知道了",
                    DefaultButton = ContentDialogButton.Close,
                    Content = $"订单确认文件已导出到：\n{exportPath}"
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    OpenContainingFolder(exportPath);
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("导出失败", ex.Message);
            }
            finally
            {
                _isExporting = false;
            }
        }

        private static void OpenContainingFolder(string filePath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{filePath}\"",
                UseShellExecute = true
            });
        }
    }
}
