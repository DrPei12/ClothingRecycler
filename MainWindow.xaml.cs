using Microsoft.UI.Windowing;
using Windows.Graphics;
using WinRT.Interop;

namespace ClothingRecycler.Desktop
{
    public sealed partial class MainWindow : Window
    {
        private readonly Dictionary<string, NavigationTarget> _navigationTargets = new()
        {
            ["dashboard"] = new(typeof(DashboardPage), "\u4EEA\u8868\u76D8"),
            ["inbound"] = new(typeof(InboundPage), "\u5165\u5E93"),
            ["outbound"] = new(typeof(OutboundPage), "\u51FA\u5E93"),
            ["stock"] = new(typeof(StockPage), "\u5E93\u5B58"),
            ["orders"] = new(typeof(OrdersPage), "\u8BA2\u5355"),
            ["customers"] = new(typeof(CustomersPage), "\u5BA2\u6237"),
            ["analytics"] = new(typeof(AnalyticsPage), "\u7ECF\u8425\u5206\u6790"),
            ["forecast"] = new(typeof(ForecastPage), "\u9884\u8BA1\u6536\u5165"),
            ["settings"] = new(typeof(SettingsPage), "\u8BBE\u7F6E"),
        };
        private readonly AppUiSettingsService _uiSettingsService;
        private string _currentNavigationTag = "dashboard";

        public MainWindow()
        {
            InitializeComponent();
            _uiSettingsService = App.GetService<AppUiSettingsService>();
            _uiSettingsService.FontSizePresetChanged += OnFontSizePresetChanged;
            Title = "\u8863\u7269\u56DE\u6536\u7BA1\u7406";
            ApplyShellFontSizing();
            ConfigureWindow();
            NavigateToDefault();
        }

        private void ConfigureWindow()
        {
            var hWnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            appWindow.MoveAndResize(GetDefaultWindowBounds(appWindow));

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }
        }

        private static RectInt32 GetDefaultWindowBounds(AppWindow appWindow)
        {
            var workArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea;

            var preferredWidth = Math.Min(1480, Math.Max(1220, (int)(workArea.Width * 0.74)));
            var preferredHeight = Math.Min(920, Math.Max(780, (int)(workArea.Height * 0.82)));

            var maxWidth = Math.Max(960, workArea.Width - 96);
            var maxHeight = Math.Max(700, workArea.Height - 72);

            var width = Math.Min(preferredWidth, maxWidth);
            var height = Math.Min(preferredHeight, maxHeight);

            var x = workArea.X + Math.Max((workArea.Width - width) / 2, 24);
            var y = workArea.Y + Math.Max((workArea.Height - height) / 2, 24);

            return new RectInt32(x, y, width, height);
        }

        private void NavigateToDefault()
        {
            if (RootNavigationView.MenuItems.Count == 0)
            {
                return;
            }

            if (RootNavigationView.SelectedItem is null && RootNavigationView.MenuItems[0] is NavigationViewItem firstItem)
            {
                RootNavigationView.SelectedItem = firstItem;
                NavigateTo(firstItem.Tag?.ToString() ?? "dashboard");
            }
        }

        private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer?.Tag is string tag)
            {
                NavigateTo(tag);
            }
        }

        private void NavigateTo(string tag, bool forceReload = false)
        {
            if (!_navigationTargets.TryGetValue(tag, out var target))
            {
                return;
            }

            _currentNavigationTag = tag;
            RootNavigationView.Header = target.Header;

            if (target.Parameter is null)
            {
                if (forceReload || ContentFrame.CurrentSourcePageType != target.PageType)
                {
                    ContentFrame.Navigate(target.PageType);
                }

                return;
            }

            ContentFrame.Navigate(target.PageType, target.Parameter);
        }

        private void OnFontSizePresetChanged(object? sender, AppFontSizePreset preset)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                ApplyShellFontSizing();
                NavigateTo(_currentNavigationTag, forceReload: true);
            });
        }

        private void ApplyShellFontSizing()
        {
            if (App.Current.Resources.TryGetValue("AppBodyFontSize", out var bodyFontSize) && bodyFontSize is double bodySize)
            {
                RootNavigationView.FontSize = bodySize;
                ContentFrame.FontSize = bodySize;
            }
        }

        private sealed record NavigationTarget(Type PageType, string Header, object? Parameter = null);
    }
}
