namespace ClothingRecycler.Desktop.Pages
{
    public sealed partial class AiAssistantPage : Page
    {
        public AiAssistantPage()
        {
            ViewModel = App.GetService<AiAssistantViewModel>();
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public AiAssistantViewModel ViewModel { get; }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
            ScrollConversationToEnd();
        }

        private async void OnSendClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.SendAsync();
            ScrollConversationToEnd();
        }

        private async void OnVoiceInputClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.ToggleVoiceRecordingAsync();
            ScrollConversationToEnd();
        }

        private async void OnRefreshProviderClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.RefreshProviderStatusAsync();
        }

        private void OnOpenLatestDraftClick(object sender, RoutedEventArgs e)
        {
            ViewModel.OpenLatestDraftPage();
        }

        private void OnCreateLatestOrderClick(object sender, RoutedEventArgs e)
        {
            ViewModel.CreateLatestOrder();
        }

        private void OnUseQuickPromptClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string prompt)
            {
                ViewModel.UseQuickPrompt(prompt);
            }
        }

        private void ScrollConversationToEnd()
        {
            if (ViewModel.Messages.Count == 0)
            {
                return;
            }

            ConversationListView.ScrollIntoView(ViewModel.Messages[^1]);
        }
    }
}
