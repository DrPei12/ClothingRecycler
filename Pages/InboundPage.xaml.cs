using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

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
                var pendingOrder = await ViewModel.PrepareSubmitAsync();
                var window = new ClothingRecycler.Desktop.OrderWindows.InboundOrderConfirmationWindow(
                    pendingOrder.ToConfirmationModel(),
                    () => ViewModel.ConfirmSubmitAsync(pendingOrder));
                ClothingRecycler.Desktop.Helpers.SecondaryWindowManager.Show(
                    window,
                    "\u5165\u5E93\u786E\u8BA4\u5355",
                    preferredWidth: 1520,
                    preferredHeight: 1000);
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

        private void OnAddCategoryEntryClick(object sender, RoutedEventArgs e)
        {
            ViewModel.AddCategoryEntry();
        }

        private void OnRemoveCategoryEntryClick(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element
                && element.Tag is InboundOrderDraftLineModel entry)
            {
                ViewModel.RemoveCategoryEntry(entry);
            }
        }

        private void OnInteractionSurfacePointerPressed(object sender, PointerRoutedEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source)
            {
                return;
            }

            if (ShouldKeepCurrentInputFocus(source))
            {
                return;
            }

            PointerDismissFocusTarget.Focus(FocusState.Programmatic);
        }

        private static bool ShouldKeepCurrentInputFocus(DependencyObject source)
        {
            return FindAncestor<NumberBox>(source) is not null
                || FindAncestor<TextBox>(source) is not null
                || FindAncestor<ComboBox>(source) is not null
                || FindAncestor<ButtonBase>(source) is not null
                || FindAncestor<SelectorItem>(source) is not null
                || FindAncestor<ScrollBar>(source) is not null;
        }

        private static T? FindAncestor<T>(DependencyObject? source)
            where T : DependencyObject
        {
            while (source is not null)
            {
                if (source is T match)
                {
                    return match;
                }

                source = source switch
                {
                    FrameworkElement element when element.Parent is not null => element.Parent,
                    _ => VisualTreeHelper.GetParent(source)
                };
            }

            return null;
        }

        private void OnRecentRecordsToggleChecked(object sender, RoutedEventArgs e)
        {
            RecentRecordsBody.Visibility = Visibility.Visible;
            UpdateRecentRecordsToggleButton(true);
        }

        private void OnRecentRecordsToggleUnchecked(object sender, RoutedEventArgs e)
        {
            RecentRecordsBody.Visibility = Visibility.Collapsed;
            UpdateRecentRecordsToggleButton(false);
        }

        private void UpdateRecentRecordsToggleButton(bool isExpanded)
        {
            if (RecentRecordsToggleButton.Content is StackPanel panel
                && panel.Children.Count >= 2
                && panel.Children[0] is FontIcon icon
                && panel.Children[1] is TextBlock text)
            {
                icon.Glyph = isExpanded ? "\uE70E" : "\uE70D";
                text.Text = isExpanded ? "\u6536\u8D77\u8BB0\u5F55" : "\u5C55\u5F00\u8BB0\u5F55";
            }
        }
    }
}
