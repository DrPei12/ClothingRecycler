using Windows.Storage.Pickers;
using Windows.System;

using WinRT.Interop;

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
            DeepSeekApiKeyBox.Password = ViewModel.DeepSeekApiKey;
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

        private async void OnUploadAudioClick(object sender, RoutedEventArgs e)
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".wav");
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.GetService<MainWindow>()));

            var file = await picker.PickSingleFileAsync();
            if (file is null)
            {
                return;
            }

            await ViewModel.TranscribeAudioFileAndSendAsync(file.Path);
            ScrollConversationToEnd();
        }

        private async void OnSaveDeepSeekApiSettingsClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.SaveDeepSeekApiSettingsAsync();
        }

        private async void OnOpenSpeechSettingsClick(object sender, RoutedEventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-speech"));
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

        private void OnDeepSeekApiKeyChanged(object sender, RoutedEventArgs e)
        {
            ViewModel.UpdateDeepSeekApiKey(DeepSeekApiKeyBox.Password);
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
