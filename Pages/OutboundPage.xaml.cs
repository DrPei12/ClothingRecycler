namespace ClothingRecycler.Desktop.Pages
{
    public sealed partial class OutboundPage : Page
    {
        private readonly AiDraftHandoffService _aiDraftHandoffService;

        public OutboundPage()
        {
            ViewModel = App.GetService<OutboundViewModel>();
            _aiDraftHandoffService = App.GetService<AiDraftHandoffService>();
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public OutboundViewModel ViewModel { get; }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();

            var pendingRequest = _aiDraftHandoffService.Consume(AiDraftOperation.Outbound);
            if (pendingRequest is not null)
            {
                await ViewModel.ApplyAiSuggestionAsync(pendingRequest.Suggestion);

                if (pendingRequest.LaunchMode == AiDraftLaunchMode.ConfirmationPreview)
                {
                    var entry = ResolveAutoSubmitEntry();
                    if (entry is null)
                    {
                        await ShowErrorDialogAsync(
                            "\u51FA\u5E93\u5931\u8D25",
                            "AI \u8349\u7A3F\u8FD8\u4E0D\u5B8C\u6574\uff0C\u6682\u65F6\u65E0\u6CD5\u76F4\u63A5\u521B\u5EFA\u51FA\u5E93\u8BA2\u5355\u3002\u8BF7\u5148\u6253\u5F00\u51FA\u5E93\u9875\u8865\u5145\u5FC5\u8981\u4FE1\u606F\u3002");
                    }
                    else
                    {
                        await ShowOutboundConfirmationAsync(entry);
                    }
                }
            }
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

            await ShowOutboundConfirmationAsync(entry);
        }

        private async Task ShowOutboundConfirmationAsync(OutboundCategoryDraftModel entry)
        {
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
                await ShowErrorDialogAsync("\u51FA\u5E93\u5931\u8D25", ex.Message);
            }
        }

        private OutboundCategoryDraftModel? ResolveAutoSubmitEntry()
        {
            return ViewModel.CategoryEntries.FirstOrDefault(entry => entry.IsExpanded && entry.CanSubmit)
                ?? ViewModel.CategoryEntries.FirstOrDefault(entry => entry.CanSubmit);
        }

        private async Task ShowErrorDialogAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = Content.XamlRoot,
                Title = title,
                CloseButtonText = "\u786E\u5B9A",
                DefaultButton = ContentDialogButton.Close,
                Content = message
            };

            await dialog.ShowAsync();
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
