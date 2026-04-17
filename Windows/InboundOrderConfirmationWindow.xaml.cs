using System.Diagnostics;
using System.Linq;

using ClothingRecycler.Desktop.Models;

namespace ClothingRecycler.Desktop.OrderWindows
{
    public sealed partial class InboundOrderConfirmationWindow : Window
    {
        private readonly OrderExportService _orderExportService;
        private readonly Func<Task<InboundOrderConfirmationModel>>? _confirmInboundAsync;
        private readonly Button? _closeButton;
        private Button? _confirmButton;
        private bool _isExporting;
        private bool _isConfirming;
        private bool _isAwaitingConfirmation;

        public InboundOrderConfirmationWindow(
            InboundOrderConfirmationModel confirmation,
            Func<Task<InboundOrderConfirmationModel>>? confirmInboundAsync = null)
        {
            Confirmation = confirmation;
            _confirmInboundAsync = confirmInboundAsync;
            _isAwaitingConfirmation = confirmInboundAsync is not null;
            _orderExportService = App.GetService<OrderExportService>();
            InitializeComponent();

            _closeButton = FooterActionsPanel.Children.OfType<Button>().LastOrDefault();

            if (_isAwaitingConfirmation)
            {
                ConfigurePreviewActions();
            }

            UpdateActionState();
        }

        public InboundOrderConfirmationModel Confirmation { get; private set; }

        private async void OnExportPdfClick(object sender, RoutedEventArgs e)
        {
            await ExportAsync(
                exporter: () => _orderExportService.ExportAsPdfAsync(DocumentRoot, Confirmation.OrderNumber),
                successTitle: "PDF 导出完成");
        }

        private async void OnExportPngClick(object sender, RoutedEventArgs e)
        {
            await ExportAsync(
                exporter: () => _orderExportService.ExportAsPngAsync(DocumentRoot, Confirmation.OrderNumber),
                successTitle: "PNG 导出完成");
        }

        private void OnCloseClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void OnConfirmInboundClick(object sender, RoutedEventArgs e)
        {
            if (_confirmInboundAsync is null || _isConfirming)
            {
                return;
            }

            try
            {
                _isConfirming = true;
                UpdateActionState();

                Confirmation = await _confirmInboundAsync();
                _isAwaitingConfirmation = false;
                ConfigureCompletedActions();
                Bindings.Update();
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = RootLayout.XamlRoot,
                    Title = "确认入库失败",
                    CloseButtonText = "确定",
                    DefaultButton = ContentDialogButton.Close,
                    Content = ex.Message
                };

                await dialog.ShowAsync();
            }
            finally
            {
                _isConfirming = false;
                UpdateActionState();
            }
        }

        private async Task ExportAsync(Func<Task<string>> exporter, string successTitle)
        {
            if (_isExporting || _isAwaitingConfirmation)
            {
                return;
            }

            try
            {
                _isExporting = true;
                UpdateActionState();

                var exportPath = await exporter();
                var dialog = new ContentDialog
                {
                    XamlRoot = RootLayout.XamlRoot,
                    Title = successTitle,
                    PrimaryButtonText = "打开目录",
                    CloseButtonText = "知道了",
                    DefaultButton = ContentDialogButton.Close,
                    Content = $"确认单已导出到：\n{exportPath}"
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    OpenContainingFolder(exportPath);
                }
            }
            catch (Exception ex)
            {
                var dialog = new ContentDialog
                {
                    XamlRoot = RootLayout.XamlRoot,
                    Title = "导出失败",
                    CloseButtonText = "确定",
                    DefaultButton = ContentDialogButton.Close,
                    Content = ex.Message
                };

                await dialog.ShowAsync();
            }
            finally
            {
                _isExporting = false;
                UpdateActionState();
            }
        }

        private void ConfigurePreviewActions()
        {
            ExportPngButton.Visibility = Visibility.Collapsed;
            ExportPdfButton.Visibility = Visibility.Collapsed;

            if (_closeButton is not null)
            {
                _closeButton.Content = "取消";
                _closeButton.Style = null;
            }

            if (_confirmButton is null)
            {
                _confirmButton = new Button
                {
                    MinWidth = 180,
                    Content = "确认入库",
                    Style = Application.Current.Resources["AccentButtonStyle"] as Style
                };
                _confirmButton.Click += OnConfirmInboundClick;
                FooterActionsPanel.Children.Add(_confirmButton);
            }
        }

        private void ConfigureCompletedActions()
        {
            ExportPngButton.Visibility = Visibility.Visible;
            ExportPdfButton.Visibility = Visibility.Visible;

            if (_closeButton is not null)
            {
                _closeButton.Content = "完成";
                _closeButton.Style = Application.Current.Resources["AccentButtonStyle"] as Style;
            }

            if (_confirmButton is not null)
            {
                FooterActionsPanel.Children.Remove(_confirmButton);
                _confirmButton.Click -= OnConfirmInboundClick;
                _confirmButton = null;
            }
        }

        private void UpdateActionState()
        {
            var canExport = !_isExporting && !_isConfirming && !_isAwaitingConfirmation;
            ExportPngButton.IsEnabled = canExport;
            ExportPdfButton.IsEnabled = canExport;

            if (_closeButton is not null)
            {
                _closeButton.IsEnabled = !_isExporting && !_isConfirming;
            }

            if (_confirmButton is not null)
            {
                _confirmButton.IsEnabled = !_isExporting && !_isConfirming;
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
