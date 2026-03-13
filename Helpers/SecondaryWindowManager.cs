using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using WinRT.Interop;

namespace ClothingRecycler.Desktop.Helpers
{
    internal static class SecondaryWindowManager
    {
        private static readonly HashSet<Window> OpenWindows = [];

        public static void Show(Window window, string title, int preferredWidth = 1280, int preferredHeight = 900)
        {
            Configure(window, title, preferredWidth, preferredHeight);
            window.Closed += OnWindowClosed;
            OpenWindows.Add(window);
            window.Activate();
        }

        private static void Configure(Window window, string title, int preferredWidth, int preferredHeight)
        {
            window.Title = title;

            var hWnd = WindowNative.GetWindowHandle(window);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hWnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);

            if (appWindow.Presenter is OverlappedPresenter presenter)
            {
                presenter.IsResizable = true;
                presenter.IsMaximizable = true;
                presenter.IsMinimizable = true;
            }

            var iconPath = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            if (File.Exists(iconPath))
            {
                appWindow.SetIcon(iconPath);
            }

            appWindow.MoveAndResize(GetCenteredBounds(appWindow, preferredWidth, preferredHeight));
        }

        private static RectInt32 GetCenteredBounds(AppWindow appWindow, int preferredWidth, int preferredHeight)
        {
            var workArea = DisplayArea.GetFromWindowId(appWindow.Id, DisplayAreaFallback.Primary).WorkArea;

            var width = Math.Min(preferredWidth, Math.Max(840, workArea.Width - 72));
            var height = Math.Min(preferredHeight, Math.Max(700, workArea.Height - 72));

            width = Math.Min(width, Math.Max(640, workArea.Width - 32));
            height = Math.Min(height, Math.Max(520, workArea.Height - 32));

            var x = workArea.X + Math.Max((workArea.Width - width) / 2, 16);
            var y = workArea.Y + Math.Max((workArea.Height - height) / 2, 16);

            return new RectInt32(x, y, width, height);
        }

        private static void OnWindowClosed(object sender, WindowEventArgs args)
        {
            if (sender is not Window window)
            {
                return;
            }

            window.Closed -= OnWindowClosed;
            OpenWindows.Remove(window);
        }
    }
}
