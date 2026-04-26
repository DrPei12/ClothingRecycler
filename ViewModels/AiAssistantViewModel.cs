using System.Collections.ObjectModel;
using System.Text;

using ClothingRecycler.Desktop.Models;
using ClothingRecycler.Desktop.Services;

using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace ClothingRecycler.Desktop.ViewModels
{
    public sealed class AiAssistantViewModel : ViewModelBase
    {
        private readonly LocalAiDraftAgentService _localAiDraftAgentService;
        private readonly AiWorkflowAgentService _aiWorkflowAgentService;
        private readonly AiWorkflowToolService _aiWorkflowToolService;
        private readonly AiVoiceRecordingService _voiceRecordingService;
        private readonly AiSpeechRecognitionService _speechRecognitionService;
        private readonly AiLiveSpeechRecognitionService _liveSpeechRecognitionService;
        private readonly AppUiSettingsService _uiSettingsService;

        private string _promptText = string.Empty;
        private AiProviderOptionModel _selectedProviderOption;
        private AiOperationOptionModel _selectedOperationOption;
        private string _providerStatusSummary = "\u672A\u68C0\u67E5\u672C\u5730 Provider \u72B6\u6001\u3002";
        private string _providerStatusDetail = "\u8FDB\u5165\u9875\u9762\u540E\u4F1A\u81EA\u52A8\u63A2\u6D4B\u4E00\u6B21\u672C\u5730 AI \u8FD0\u884C\u65F6\u3002";
        private string _asrStatusSummary = "\u672A\u68C0\u67E5 ASR \u72B6\u6001\u3002";
        private string _asrStatusDetail = "ASR \u8D1F\u8D23\u628A\u9EA6\u514B\u98CE\u6216\u97F3\u9891\u6587\u4EF6\u8F6C\u6210 Agent \u53EF\u63A5\u6536\u7684\u6587\u672C\u3002";
        private string _voiceStatusText = string.Empty;
        private string _voiceActivityText = "\u9EA6\u514B\u98CE\u5F85\u547D\u4E2D";
        private string _voiceActivityDetail = "\u5F00\u59CB\u5F55\u97F3\u540E\u4F1A\u5728\u8FD9\u91CC\u663E\u793A\u5B9E\u65F6\u58F0\u97F3\u68C0\u6D4B\u3002";
        private string _deepSeekApiBaseUrl = "https://api.deepseek.com";
        private string _deepSeekApiModel = "deepseek-v4-flash";
        private string _deepSeekApiKey = string.Empty;
        private AiOrderDraftSuggestion? _latestSuggestion;
        private bool _isVoiceRecording;
        private bool _isVoiceFeedbackActive;
        private bool _isReadingVoiceLevel;
        private bool _hasDetectedVoiceDuringRecording;
        private string _liveVoicePromptPrefix = string.Empty;
        private bool _isUserStoppingLiveVoiceInput;
        private bool _isRestartingLiveVoiceInput;
        private int _liveVoiceRestartAttempts;
        private bool _hasLiveTranscript;
        private DateTimeOffset _liveVoiceStartedAt;
        private DateTimeOffset _lastLiveTranscriptAt;
        private string _lastLiveSpeechState = string.Empty;
        private DispatcherQueue? _voiceDispatcherQueue;
        private double _voiceLevelPercent;
        private double _voiceMeterPhase;
        private DispatcherQueueTimer? _voiceFeedbackTimer;
        private DispatcherQueueTimer? _liveVoiceWatchdogTimer;
        private bool _hasLoaded;

        public AiAssistantViewModel(
            LocalAiDraftAgentService localAiDraftAgentService,
            AiWorkflowAgentService aiWorkflowAgentService,
            AiWorkflowToolService aiWorkflowToolService,
            AiVoiceRecordingService voiceRecordingService,
            AiSpeechRecognitionService speechRecognitionService,
            AiLiveSpeechRecognitionService liveSpeechRecognitionService,
            AppUiSettingsService uiSettingsService)
        {
            _localAiDraftAgentService = localAiDraftAgentService;
            _aiWorkflowAgentService = aiWorkflowAgentService;
            _aiWorkflowToolService = aiWorkflowToolService;
            _voiceRecordingService = voiceRecordingService;
            _speechRecognitionService = speechRecognitionService;
            _liveSpeechRecognitionService = liveSpeechRecognitionService;
            _uiSettingsService = uiSettingsService;

            ProviderOptions =
            [
                new AiProviderOptionModel
                {
                    Kind = AiLocalProviderKind.HuggingFaceUltravoxPython,
                    DisplayName = "Hugging Face Ultravox",
                    Description = "\u672C\u5730 Ultravox + transformers \u8FD0\u884C\u65F6\u3002"
                },
                new AiProviderOptionModel
                {
                    Kind = AiLocalProviderKind.Ollama,
                    DisplayName = "Ollama",
                    Description = "\u672C\u673A Ollama \u8FD0\u884C\u65F6\u3002"
                },
                new AiProviderOptionModel
                {
                    Kind = AiLocalProviderKind.DeepSeekApi,
                    DisplayName = "DeepSeek API (Beta)",
                    Description = "\u4F7F\u7528 DeepSeek API \u8DD1\u6587\u672C\u8349\u7A3F\u6D41\u7A0B\uFF0C\u4E0D\u4F9D\u8D56\u672C\u5730\u5927\u6A21\u578B\u3002"
                }
            ];

            OperationOptions =
            [
                new AiOperationOptionModel
                {
                    Operation = AiDraftOperation.Inbound,
                    DisplayName = "\u5165\u5E93\u8349\u7A3F",
                    Description = "\u628A\u81EA\u7136\u8BED\u8A00\u6574\u7406\u6210\u4E00\u5F20\u5F85\u786E\u8BA4\u7684\u5165\u5E93\u8349\u7A3F\u3002"
                },
                new AiOperationOptionModel
                {
                    Operation = AiDraftOperation.Outbound,
                    DisplayName = "\u51FA\u5E93\u8349\u7A3F",
                    Description = "\u628A\u81EA\u7136\u8BED\u8A00\u6574\u7406\u6210\u4E00\u5F20\u5F85\u786E\u8BA4\u7684\u51FA\u5E93\u8349\u7A3F\u3002"
                }
            ];

            _selectedProviderOption = ProviderOptions[0];
            _selectedOperationOption = OperationOptions[0];
            LoadDeepSeekApiSettings();
            Title = "AI (Beta)";
        }

        public ObservableCollection<AiConversationMessageModel> Messages { get; } = [];

        public ObservableCollection<AiWorkflowTraceStep> AgentTraceSteps { get; } = [];

        public IReadOnlyList<AiProviderOptionModel> ProviderOptions { get; }

        public IReadOnlyList<AiOperationOptionModel> OperationOptions { get; }

        public AiProviderOptionModel SelectedProviderOption
        {
            get => _selectedProviderOption;
            set
            {
                if (SetProperty(ref _selectedProviderOption, value))
                {
                    UpdateProviderStatus();
                    OnPropertyChanged(nameof(ShowDeepSeekApiSettings));
                    OnPropertyChanged(nameof(DeepSeekApiSettingsVisibility));
                    OnPropertyChanged(nameof(VoiceButtonText));
                    OnPropertyChanged(nameof(CanToggleVoiceRecording));
                    OnPropertyChanged(nameof(VoiceCapabilityText));
                    OnPropertyChanged(nameof(VoiceCapabilityVisibility));
                }
            }
        }

        public AiOperationOptionModel SelectedOperationOption
        {
            get => _selectedOperationOption;
            set
            {
                if (SetProperty(ref _selectedOperationOption, value))
                {
                    OnPropertyChanged(nameof(OperationGuidanceText));
                    OnPropertyChanged(nameof(OpenLatestDraftButtonText));
                    OnPropertyChanged(nameof(CreateLatestOrderButtonText));
                }
            }
        }

        public string PromptText
        {
            get => _promptText;
            set
            {
                if (SetProperty(ref _promptText, value))
                {
                    OnPropertyChanged(nameof(CanSend));
                }
            }
        }

        public string ProviderStatusSummary
        {
            get => _providerStatusSummary;
            private set => SetProperty(ref _providerStatusSummary, value);
        }

        public string ProviderStatusDetail
        {
            get => _providerStatusDetail;
            private set => SetProperty(ref _providerStatusDetail, value);
        }

        public string AsrStatusSummary
        {
            get => _asrStatusSummary;
            private set => SetProperty(ref _asrStatusSummary, value);
        }

        public string AsrStatusDetail
        {
            get => _asrStatusDetail;
            private set => SetProperty(ref _asrStatusDetail, value);
        }

        public string DeepSeekApiBaseUrl
        {
            get => _deepSeekApiBaseUrl;
            set
            {
                if (SetProperty(ref _deepSeekApiBaseUrl, value))
                {
                    OnPropertyChanged(nameof(CanSaveDeepSeekApiSettings));
                }
            }
        }

        public string DeepSeekApiModel
        {
            get => _deepSeekApiModel;
            set
            {
                if (SetProperty(ref _deepSeekApiModel, value))
                {
                    OnPropertyChanged(nameof(CanSaveDeepSeekApiSettings));
                }
            }
        }

        public string DeepSeekApiKey => _deepSeekApiKey;

        public bool ShowDeepSeekApiSettings => SelectedProviderOption.Kind == AiLocalProviderKind.DeepSeekApi;

        public Visibility DeepSeekApiSettingsVisibility => ShowDeepSeekApiSettings ? Visibility.Visible : Visibility.Collapsed;

        public bool CanSaveDeepSeekApiSettings =>
            !IsBusy
            && !string.IsNullOrWhiteSpace(DeepSeekApiBaseUrl)
            && !string.IsNullOrWhiteSpace(DeepSeekApiModel);

        public bool CanSend => !IsBusy && !IsVoiceRecording && !string.IsNullOrWhiteSpace(PromptText);

        public bool IsVoiceRecording
        {
            get => _isVoiceRecording;
            private set
            {
                if (SetProperty(ref _isVoiceRecording, value))
                {
                    OnPropertyChanged(nameof(VoiceButtonText));
                    OnPropertyChanged(nameof(CanToggleVoiceRecording));
                    OnPropertyChanged(nameof(CanSend));
                    OnPropertyChanged(nameof(VoiceFeedbackVisibility));
                }
            }
        }

        public bool CanToggleVoiceRecording => IsVoiceRecording || !IsBusy;

        public bool CanUploadAudioFile => !IsBusy;

        public string VoiceButtonText => IsVoiceRecording
            ? "\u505c\u6b62\u8bed\u97f3\u8f93\u5165"
            : IsBusy && _isVoiceFeedbackActive
                ? "\u8bc6\u522b\u4e2d"
                : "\u5b9e\u65f6\u8bed\u97f3\u8f93\u5165";

        public string VoiceCapabilityText => "\u8bed\u97f3\u4f1a\u8fb9\u8bf4\u8fb9\u8f6c\u6210\u6587\u5b57\u586b\u5165\u7f16\u8f91\u680f\uff0c\u4f60\u53ef\u4ee5\u5148\u4fee\u6b63\uff0c\u518d\u70b9\u201c\u53d1\u9001\u7ed9 AI\u201d\u8fdb\u5165 Agent\u3002";

        public Visibility VoiceCapabilityVisibility => Visibility.Visible;

        public string VoiceStatusText
        {
            get => _voiceStatusText;
            private set
            {
                if (SetProperty(ref _voiceStatusText, value))
                {
                    OnPropertyChanged(nameof(VoiceStatusVisibility));
                }
            }
        }

        public Visibility VoiceStatusVisibility => string.IsNullOrWhiteSpace(VoiceStatusText) ? Visibility.Collapsed : Visibility.Visible;

        public Visibility VoiceFeedbackVisibility => (IsVoiceRecording || _isVoiceFeedbackActive) ? Visibility.Visible : Visibility.Collapsed;

        public string VoiceActivityText
        {
            get => _voiceActivityText;
            private set => SetProperty(ref _voiceActivityText, value);
        }

        public string VoiceActivityDetail
        {
            get => _voiceActivityDetail;
            private set => SetProperty(ref _voiceActivityDetail, value);
        }

        public double VoiceLevelPercent
        {
            get => _voiceLevelPercent;
            private set
            {
                var clampedValue = Math.Clamp(value, 0, 100);
                if (Math.Abs(_voiceLevelPercent - clampedValue) > 0.1)
                {
                    _voiceLevelPercent = clampedValue;
                    OnPropertyChanged();
                    RaiseVoiceMeterChanged();
                }
            }
        }

        public string VoiceLevelText => $"{VoiceLevelPercent:0}%";

        public double VoiceMeterBar1Height => CalculateVoiceMeterBarHeight(0.0);

        public double VoiceMeterBar2Height => CalculateVoiceMeterBarHeight(0.9);

        public double VoiceMeterBar3Height => CalculateVoiceMeterBarHeight(1.8);

        public double VoiceMeterBar4Height => CalculateVoiceMeterBarHeight(2.7);

        public double VoiceMeterBar5Height => CalculateVoiceMeterBarHeight(3.6);

        public bool HasLatestDraft => _latestSuggestion is not null;

        public Visibility LatestDraftVisibility => HasLatestDraft ? Visibility.Visible : Visibility.Collapsed;

        public bool HasAgentTrace => AgentTraceSteps.Count > 0;

        public Visibility AgentTraceVisibility => HasAgentTrace ? Visibility.Visible : Visibility.Collapsed;

        public bool CanCreateLatestOrder => _latestSuggestion is not null && _aiWorkflowToolService.CanCreateOrder(_latestSuggestion);

        public string LatestDraftTitle => _latestSuggestion is null
            ? "\u8FD8\u6CA1\u6709\u8349\u7A3F"
            : $"{GetOperationDisplayName(_latestSuggestion.Operation)}\u76EE\u6807\uff1A{ResolveCustomerText(_latestSuggestion)}";

        public string LatestDraftSummary => _latestSuggestion is null
            ? "\u5148\u53D1\u4E00\u6761\u6307\u4EE4\uff0C\u8BA9 AI \u5E2E\u4F60\u6574\u7406\u5165\u5E93\u6216\u51FA\u5E93\u8349\u7A3F\u3002"
            : BuildDraftSummary(_latestSuggestion);

        public string LatestDraftWarnings => _latestSuggestion is null
            ? "AI \u53EA\u4F1A\u751F\u6210\u8349\u7A3F\uff0C\u4E0D\u4F1A\u76F4\u63A5\u63D0\u4EA4\u8BA2\u5355\u6216\u6539\u52A8\u5E93\u5B58\u3002"
            : BuildDraftWarnings(_latestSuggestion);

        public string OperationGuidanceText => "\u76f4\u63a5\u8bf4\u5165\u5e93\u6216\u51fa\u5e93\uff0cAI \u4f1a\u81ea\u52a8\u8bc6\u522b\u4e1a\u52a1\u8def\u7ebf\u5e76\u8fdb\u5165\u5bf9\u5e94\u786e\u8ba4\u6d41\u3002";

        public string OpenLatestDraftButtonText => GetEffectiveOperationForActions() == AiDraftOperation.Outbound
            ? "\u6253\u5F00\u51FA\u5E93\u9875\u5E76\u5E26\u5165\u8349\u7A3F"
            : "\u6253\u5F00\u5165\u5E93\u9875\u5E76\u5E26\u5165\u8349\u7A3F";

        public string CreateLatestOrderButtonText => GetEffectiveOperationForActions() == AiDraftOperation.Outbound
            ? "\u76F4\u63A5\u521B\u5EFA\u51FA\u5E93\u8BA2\u5355"
            : "\u9884\u89C8\u5E76\u521B\u5EFA\u5165\u5E93\u8BA2\u5355";

        public string CreateLatestOrderStatusText => _latestSuggestion is null
            ? string.Empty
            : BuildCreateLatestOrderStatusText(_latestSuggestion);

        public async Task LoadAsync()
        {
            LoadDeepSeekApiSettings();

            if (!_hasLoaded)
            {
                Messages.Clear();
                Messages.Add(new AiConversationMessageModel
                {
                    Header = "AI (Beta)",
                    Body = "\u6211\u73B0\u5728\u4E3B\u8981\u5E2E\u4F60\u6574\u7406\u5165\u5E93\u548C\u51FA\u5E93\u8349\u7A3F\u3002\u4F60\u53EF\u4EE5\u76F4\u63A5\u8F93\u5165\u81EA\u7136\u8BED\u8A00\uff0C\u6BD4\u5982\u201C\u738B\u59D0\u5165\u5E93\u590F\u88C5 12 \u65A4\uff0C3 \u5757 8\u201D\uff0C\u6211\u4F1A\u628A\u5B83\u53D8\u6210\u53EF\u590D\u6838\u7684\u8349\u7A3F\u3002",
                    SupportingText = "\u5F53\u524D\u9636\u6BB5\u5148\u505A\u6587\u672C\u5F55\u5355\uff0C\u8BED\u97F3\u548C\u77E5\u8BC6\u95EE\u7B54\u540E\u7EED\u518D\u63A5\u3002",
                    TimestampText = DateTime.Now.ToString("HH:mm"),
                    IsUser = false
                });
                _hasLoaded = true;
            }

            await RefreshProviderStatusAsync();
            await RefreshAsrStatusAsync();
            _ = WarmSelectedProviderSilentlyAsync();
        }

        public async Task RefreshProviderStatusAsync()
        {
            SetBusyState(true);

            try
            {
                var results = await _localAiDraftAgentService.ProbeProvidersAsync();
                var selectedResult = results.FirstOrDefault(result => result.Kind == SelectedProviderOption.Kind);

                if (selectedResult is null)
                {
                    ProviderStatusSummary = "\u672A\u627E\u5230\u5F53\u524D Provider \u7684\u63A2\u6D4B\u7ED3\u679C\u3002";
                    ProviderStatusDetail = SelectedProviderOption.Description;
                }
                else
                {
                    ProviderStatusSummary = selectedResult.Summary;
                    ProviderStatusDetail = string.IsNullOrWhiteSpace(selectedResult.Detail)
                        ? SelectedProviderOption.Description
                        : selectedResult.Detail;
                }

                StatusMessage = "\u672C\u5730 AI \u8FD0\u884C\u65F6\u72B6\u6001\u5DF2\u5237\u65B0\u3002";
            }
            catch (Exception ex)
            {
                ProviderStatusSummary = "\u672C\u5730 Provider \u63A2\u6D4B\u5931\u8D25\u3002";
                ProviderStatusDetail = ex.Message;
                StatusMessage = "\u65E0\u6CD5\u5B8C\u6210\u672C\u5730 AI \u63A2\u6D4B\u3002";
            }
            finally
            {
                SetBusyState(false);
            }
        }

        public async Task RefreshAsrStatusAsync()
        {
            SetBusyState(true);

            try
            {
                var settings = _uiSettingsService.GetAsrRuntimeSettings();
                var results = await _speechRecognitionService.ProbeProvidersAsync();
                var selectedResult = results.FirstOrDefault(result => result.Kind == settings.ProviderKind);

                if (selectedResult is null)
                {
                    AsrStatusSummary = "\u672A\u627E\u5230\u5F53\u524D ASR Provider\u3002";
                    AsrStatusDetail = $"provider={settings.ProviderKind}; language={settings.Language}";
                }
                else
                {
                    AsrStatusSummary = selectedResult.Summary;
                    AsrStatusDetail = string.IsNullOrWhiteSpace(selectedResult.Detail)
                        ? $"provider={settings.ProviderKind}; language={settings.Language}"
                        : $"{selectedResult.Detail}{Environment.NewLine}language={settings.Language}; max_audio={settings.MaxAudioBytes / 1024 / 1024} MB; timeout={settings.Timeout.TotalSeconds:0.#}s";
                }
            }
            catch (Exception ex)
            {
                AsrStatusSummary = "ASR Provider \u63A2\u6D4B\u5931\u8D25\u3002";
                AsrStatusDetail = ex.Message;
            }
            finally
            {
                SetBusyState(false);
            }
        }

        public async Task SendAsync()
        {
            if (!CanSend)
            {
                return;
            }

            var prompt = PromptText.Trim();
            PromptText = string.Empty;
            Messages.Add(new AiConversationMessageModel
            {
                Header = "\u4F60",
                Body = prompt,
                TimestampText = DateTime.Now.ToString("HH:mm"),
                IsUser = true
            });

            SetBusyState(true);
            StatusMessage = "AI \u6B63\u5728\u6574\u7406\u8349\u7A3F\u2026";

            try
            {
                await RunPromptWorkflowAsync(prompt);
            }
            catch (Exception ex)
            {
                Messages.Add(new AiConversationMessageModel
                {
                    Header = "\u672C\u5730\u8FD0\u884C\u65F6",
                    Body = "\u8FD9\u6B21\u6CA1\u80FD\u751F\u6210\u8349\u7A3F\u3002",
                    SupportingText = ex.Message,
                    TimestampText = DateTime.Now.ToString("HH:mm"),
                    IsUser = false
                });

                StatusMessage = "\u672C\u5730 AI \u8BF7\u6C42\u5931\u8D25\u3002";
            }
            finally
            {
                SetBusyState(false);
            }
        }

        public async Task ToggleVoiceRecordingAsync()
        {
            if (IsVoiceRecording)
            {
                await StopLiveVoiceInputAsync();
                return;
            }

            if (IsBusy)
            {
                return;
            }

            try
            {
                await StartLiveVoiceInputAsync();
            }
            catch (Exception ex)
            {
                _liveSpeechRecognitionService.TranscriptUpdated -= OnLiveSpeechTranscriptUpdated;
                _liveSpeechRecognitionService.StatusChanged -= OnLiveSpeechStatusChanged;
                await _liveSpeechRecognitionService.StopAsync();
                SetVoiceRecognitionStage(
                    "\u5b9e\u65f6\u8bed\u97f3\u542f\u52a8\u5931\u8d25",
                    ex.Message);
                IsVoiceRecording = false;
                VoiceStatusText = "\u65e0\u6cd5\u542f\u52a8\u5b9e\u65f6\u8bed\u97f3\u8bc6\u522b\u3002";
                Messages.Add(new AiConversationMessageModel
                {
                    Header = "\u8bed\u97f3\u8f93\u5165",
                    Body = "\u6ca1\u80fd\u542f\u52a8\u5b9e\u65f6\u8bed\u97f3\u8f93\u5165\u3002",
                    SupportingText = ex.Message,
                    TimestampText = DateTime.Now.ToString("HH:mm"),
                    IsUser = false
                });
            }
        }

        public void UseQuickPrompt(string prompt)
        {
            PromptText = prompt;
        }

        public void UpdateDeepSeekApiKey(string apiKey)
        {
            _deepSeekApiKey = apiKey?.Trim() ?? string.Empty;
        }

        public async Task SaveDeepSeekApiSettingsAsync()
        {
            SetBusyState(true);

            try
            {
                await _uiSettingsService.SaveDeepSeekApiSettingsAsync(DeepSeekApiBaseUrl, DeepSeekApiModel, _deepSeekApiKey);
                LoadDeepSeekApiSettings();
                StatusMessage = "DeepSeek API \u672C\u673A\u914D\u7F6E\u5DF2\u4FDD\u5B58\u3002";

                if (SelectedProviderOption.Kind == AiLocalProviderKind.DeepSeekApi)
                {
                    await RefreshProviderStatusAsync();
                }
            }
            finally
            {
                SetBusyState(false);
            }
        }

        public async Task TranscribeAudioFileAndSendAsync(string audioPath)
        {
            if (IsBusy)
            {
                return;
            }

            SetBusyState(true);
            SetVoiceRecognitionStage(
                "\u6b63\u5728\u8bc6\u522b\u97f3\u9891\u6587\u4ef6",
                "\u4e0a\u4f20\u6587\u4ef6\u4e0d\u8fdb\u884c\u5b9e\u65f6\u9ea6\u514b\u98ce\u76d1\u542c\uff0c\u8bc6\u522b\u5b8c\u6210\u540e\u4f1a\u628a\u6587\u5b57\u6d41\u5f0f\u586b\u5165\u7f16\u8f91\u680f\u3002");
            VoiceStatusText = "\u6b63\u5728\u8bc6\u522b\u97f3\u9891\u6587\u4ef6\u2026";
            StatusMessage = "\u6b63\u5728\u8bc6\u522b\u97f3\u9891\u6587\u4ef6\u2026";

            try
            {
                await TranscribeAndRunWorkflowAsync(audioPath, AudioInputKind.UploadedFile);
            }
            finally
            {
                ClearVoiceFeedback();
                SetBusyState(false);
            }
        }

        public void OpenLatestDraftPage()
        {
            if (_latestSuggestion is null)
            {
                return;
            }

            var result = _aiWorkflowToolService.Execute(
                _aiWorkflowToolService.GetOpenEditorToolName(_latestSuggestion.Operation),
                _latestSuggestion);
            if (result.TraceSteps.Count > 0)
            {
                ReplaceAgentTrace(result.TraceSteps);
            }

            Messages.Add(new AiConversationMessageModel
            {
                Header = "AI \u52A9\u624B",
                Body = result.Summary,
                TimestampText = DateTime.Now.ToString("HH:mm"),
                IsUser = false
            });

            StatusMessage = result.Summary;
        }

        public void CreateLatestOrder()
        {
            if (_latestSuggestion is null)
            {
                return;
            }

            if (!_aiWorkflowToolService.CanCreateOrder(_latestSuggestion))
            {
                StatusMessage = "\u5F53\u524D AI \u8349\u7A3F\u8FD8\u4E0D\u80FD\u76F4\u63A5\u521B\u5EFA\u8BA2\u5355\uff0C\u8BF7\u5148\u8865\u9F50\u5BA2\u6237\u548C\u660E\u7EC6\uff0C\u6216\u6253\u5F00\u4E1A\u52A1\u9875\u7EE7\u7EED\u7F16\u8F91\u3002";
                return;
            }

            var result = _aiWorkflowToolService.Execute(
                _aiWorkflowToolService.GetCreateOrderToolName(_latestSuggestion.Operation),
                _latestSuggestion);
            if (result.TraceSteps.Count > 0)
            {
                ReplaceAgentTrace(result.TraceSteps);
            }

            Messages.Add(new AiConversationMessageModel
            {
                Header = "Workflow Agent",
                Body = result.Summary,
                TimestampText = DateTime.Now.ToString("HH:mm"),
                IsUser = false
            });

            StatusMessage = result.Summary;
        }

        private void UpdateProviderStatus()
        {
            if (SelectedProviderOption.Kind == AiLocalProviderKind.DeepSeekApi)
            {
                var settings = _uiSettingsService.GetDeepSeekApiRuntimeSettings();
                ProviderStatusSummary = string.IsNullOrWhiteSpace(settings.ApiKey)
                    ? "DeepSeek API \u8FD8\u6CA1\u6709\u914D\u7F6E Key\u3002"
                    : $"DeepSeek API \u5F85\u547D\u4E2D\uFF1A{settings.Model}";
                ProviderStatusDetail = $"base_url={settings.BaseUrl}; key_source={settings.SourceDescription}";
                return;
            }

            ProviderStatusSummary = $"\u5F53\u524D Provider\uFF1A{SelectedProviderOption.DisplayName}";
            ProviderStatusDetail = SelectedProviderOption.Description;
        }

        private void RaiseLatestDraftStateChanged()
        {
            OnPropertyChanged(nameof(HasLatestDraft));
            OnPropertyChanged(nameof(LatestDraftVisibility));
            OnPropertyChanged(nameof(HasAgentTrace));
            OnPropertyChanged(nameof(AgentTraceVisibility));
            OnPropertyChanged(nameof(CanCreateLatestOrder));
            OnPropertyChanged(nameof(LatestDraftTitle));
            OnPropertyChanged(nameof(LatestDraftSummary));
            OnPropertyChanged(nameof(LatestDraftWarnings));
            OnPropertyChanged(nameof(OpenLatestDraftButtonText));
            OnPropertyChanged(nameof(CreateLatestOrderButtonText));
            OnPropertyChanged(nameof(CreateLatestOrderStatusText));
        }

        private void ReplaceAgentTrace(IReadOnlyList<AiWorkflowTraceStep> traceSteps)
        {
            AgentTraceSteps.Clear();
            foreach (var step in traceSteps)
            {
                AgentTraceSteps.Add(step);
            }

            OnPropertyChanged(nameof(HasAgentTrace));
            OnPropertyChanged(nameof(AgentTraceVisibility));
        }

        private void SetBusyState(bool value)
        {
            IsBusy = value;
            OnPropertyChanged(nameof(CanSend));
            OnPropertyChanged(nameof(CanToggleVoiceRecording));
            OnPropertyChanged(nameof(CanUploadAudioFile));
            OnPropertyChanged(nameof(CanSaveDeepSeekApiSettings));
            OnPropertyChanged(nameof(VoiceButtonText));
        }

        private async Task StartLiveVoiceInputAsync()
        {
            _voiceDispatcherQueue = DispatcherQueue.GetForCurrentThread();
            _liveVoicePromptPrefix = PromptText.Trim();
            _isUserStoppingLiveVoiceInput = false;
            _isRestartingLiveVoiceInput = false;
            _liveVoiceRestartAttempts = 0;
            _hasLiveTranscript = false;
            _lastLiveSpeechState = string.Empty;
            _liveVoiceStartedAt = DateTimeOffset.Now;
            _lastLiveTranscriptAt = DateTimeOffset.MinValue;
            _liveSpeechRecognitionService.TranscriptUpdated -= OnLiveSpeechTranscriptUpdated;
            _liveSpeechRecognitionService.StatusChanged -= OnLiveSpeechStatusChanged;
            _liveSpeechRecognitionService.TranscriptUpdated += OnLiveSpeechTranscriptUpdated;
            _liveSpeechRecognitionService.StatusChanged += OnLiveSpeechStatusChanged;

            SetVoiceFeedbackActive(true);
            VoiceLevelPercent = 18;
            VoiceActivityText = "\u6b63\u5728\u542f\u52a8\u5b9e\u65f6\u8bed\u97f3\u8bc6\u522b";
            VoiceActivityDetail = "\u542f\u52a8\u540e\uff0c\u4f60\u8bf4\u7684\u5185\u5bb9\u4f1a\u5b9e\u65f6\u51fa\u73b0\u5728\u4e0b\u65b9\u7f16\u8f91\u6846\u3002";
            VoiceStatusText = "\u6b63\u5728\u542f\u52a8\u5b9e\u65f6\u8bed\u97f3\u8f93\u5165\u2026";
            StatusMessage = "\u6b63\u5728\u542f\u52a8\u5b9e\u65f6\u8bed\u97f3\u8f93\u5165\u2026";

            var settings = _uiSettingsService.GetAsrRuntimeSettings();
            await _liveSpeechRecognitionService.StartAsync(settings.Language);

            IsVoiceRecording = true;
            StartLiveVoiceWatchdogTimer();
            VoiceLevelPercent = 12;
            VoiceActivityText = "\u6b63\u5728\u7b49\u5f85 Windows \u8fd4\u56de\u6587\u5b57";
            VoiceActivityDetail = "\u8bf7\u76f4\u63a5\u8bf4\u8ba2\u5355\u4fe1\u606f\u3002\u53ea\u8981 Windows \u8bed\u97f3\u8bc6\u522b\u4ea7\u751f\u5019\u9009\u6587\u672c\uff0c\u5b83\u5c31\u4f1a\u7acb\u5373\u8fdb\u5165\u7f16\u8f91\u6846\u3002";
            VoiceStatusText = "\u5b9e\u65f6\u8bed\u97f3\u8f93\u5165\u4e2d\uff0c\u518d\u70b9\u4e00\u6b21\u505c\u6b62\u3002";
            StatusMessage = "\u5b9e\u65f6\u8bed\u97f3\u8f93\u5165\u4e2d\u2026";
        }

        private async Task StopLiveVoiceInputAsync()
        {
            _isUserStoppingLiveVoiceInput = true;
            await _liveSpeechRecognitionService.StopAsync();
            StopLiveVoiceWatchdogTimer();
            _liveSpeechRecognitionService.TranscriptUpdated -= OnLiveSpeechTranscriptUpdated;
            _liveSpeechRecognitionService.StatusChanged -= OnLiveSpeechStatusChanged;
            IsVoiceRecording = false;
            _isRestartingLiveVoiceInput = false;

            if (string.IsNullOrWhiteSpace(PromptText))
            {
                ResetVoiceFeedbackToIdle();
                VoiceStatusText = "\u672a\u83b7\u53d6\u5230\u8bed\u97f3\u6587\u5b57\u3002";
                StatusMessage = "\u5b9e\u65f6\u8bed\u97f3\u8f93\u5165\u5df2\u505c\u6b62\u3002";
                return;
            }

            ResetVoiceFeedbackToIdle();
            VoiceStatusText = "\u5b9e\u65f6\u8bed\u97f3\u8f93\u5165\u5df2\u505c\u6b62\uff0c\u6587\u5b57\u5df2\u7559\u5728\u7f16\u8f91\u6846\u3002";
            StatusMessage = "\u8bed\u97f3\u6587\u5b57\u5df2\u586b\u5165\uff0c\u53ef\u4fee\u6539\u540e\u53d1\u9001\u3002";
        }

        private void OnLiveSpeechTranscriptUpdated(object? sender, LiveSpeechRecognitionUpdate update)
        {
            EnqueueVoiceUiUpdate(() => ApplyLiveSpeechTranscript(update));
        }

        private void OnLiveSpeechStatusChanged(object? sender, string status)
        {
            EnqueueVoiceUiUpdate(() => ApplyLiveSpeechStatus(status));
        }

        private void ApplyLiveSpeechTranscript(LiveSpeechRecognitionUpdate update)
        {
            var liveText = AsrTextPostProcessor.NormalizeForAgent(update.Text);
            if (string.IsNullOrWhiteSpace(liveText))
            {
                return;
            }

            _hasLiveTranscript = true;
            _lastLiveTranscriptAt = DateTimeOffset.Now;
            _liveVoiceRestartAttempts = 0;
            PromptText = MergeLiveVoicePrompt(_liveVoicePromptPrefix, liveText);
            VoiceLevelPercent = update.IsFinal ? 82 : 56;
            VoiceActivityText = update.IsFinal
                ? "\u5df2\u786e\u8ba4\u4e00\u6bb5\u8bed\u97f3"
                : "\u6b63\u5728\u5b9e\u65f6\u8f6c\u6587\u5b57";
            VoiceActivityDetail = update.IsFinal
                ? $"\u5df2\u5199\u5165\uff1a{liveText}"
                : $"\u6b63\u5728\u542c\uff1a{liveText}";
            VoiceStatusText = $"\u5b9e\u65f6\u8bed\u97f3\uff1a{liveText}";
            StatusMessage = "\u8bed\u97f3\u6b63\u5728\u5b9e\u65f6\u8f6c\u5199\u5230\u7f16\u8f91\u6846\u2026";
        }

        private void ApplyLiveSpeechStatus(string status)
        {
            if (status.StartsWith("live_speech_requesting_access", StringComparison.OrdinalIgnoreCase))
            {
                VoiceActivityText = "\u6b63\u5728\u8bf7\u6c42 Windows \u8bed\u97f3\u6743\u9650";
                VoiceActivityDetail = "\u5982\u679c\u7cfb\u7edf\u8981\u6c42\u786e\u8ba4\u8bed\u97f3\u9690\u79c1\u7b56\u7565\uff0c\u8bf7\u5148\u540c\u610f\uff1b\u5426\u5219\u9700\u8981\u5230 Windows \u8bbe\u7f6e\u91cc\u6253\u5f00\u201c\u5728\u7ebf\u8bed\u97f3\u8bc6\u522b\u201d\u3002";
                return;
            }

            if (status.StartsWith("live_speech_initializing", StringComparison.OrdinalIgnoreCase))
            {
                VoiceActivityText = "\u6b63\u5728\u51c6\u5907 Windows \u8bed\u97f3\u8bc6\u522b";
                VoiceActivityDetail = "\u5982\u679c\u9996\u6b21\u4f7f\u7528\u65f6\u95f4\u8f83\u957f\uff0c\u53ef\u80fd\u662f Windows \u6b63\u5728\u542f\u52a8\u8bed\u97f3\u670d\u52a1\u3002";
                return;
            }

            if (status.StartsWith("live_speech_listening", StringComparison.OrdinalIgnoreCase))
            {
                VoiceLevelPercent = 12;
                VoiceActivityText = "\u5df2\u542f\u52a8\uff0c\u7b49\u5f85\u8f6c\u5199\u6587\u5b57";
                VoiceActivityDetail = "\u8bf7\u8bf4\u8bdd\u3002\u5982\u679c\u8f83\u957f\u65f6\u95f4\u6ca1\u6709\u4efb\u4f55\u6587\u5b57\u8fdb\u5165\u7f16\u8f91\u6846\uff0c\u8bf4\u660e Windows \u8bed\u97f3\u8bc6\u522b\u6ca1\u6709\u4ea7\u751f\u5019\u9009\u6587\u672c\u3002";
                return;
            }

            if (status.StartsWith("live_speech_state:", StringComparison.OrdinalIgnoreCase))
            {
                ApplyLiveSpeechRecognizerState(status["live_speech_state:".Length..]);
                return;
            }

            if (status.StartsWith("live_speech_completed", StringComparison.OrdinalIgnoreCase)
                && IsVoiceRecording)
            {
                if (_isUserStoppingLiveVoiceInput)
                {
                    ResetVoiceFeedbackToIdle();
                    return;
                }

                VoiceLevelPercent = 24;
                VoiceActivityText = "\u5b9e\u65f6\u8bed\u97f3\u6b63\u5728\u81ea\u52a8\u6062\u590d";
                VoiceActivityDetail = "\u4e0d\u662f\u4f60\u624b\u52a8\u505c\u6b62\uff0cWindows \u6682\u505c\u4e86\u5f53\u524d\u542c\u5199\u4f1a\u8bdd\uff0c\u6211\u6b63\u5728\u81ea\u52a8\u91cd\u65b0\u8fde\u63a5\u3002";
                StatusMessage = "\u5b9e\u65f6\u8bed\u97f3\u4f1a\u8bdd\u81ea\u52a8\u6062\u590d\u4e2d\u2026";
                _ = RestartLiveVoiceInputAfterUnexpectedCompletionAsync();
            }
        }

        private void ApplyLiveSpeechRecognizerState(string state)
        {
            _lastLiveSpeechState = state;
            switch (state)
            {
                case "Capturing":
                    VoiceLevelPercent = 22;
                    VoiceActivityText = "\u6b63\u5728\u76d1\u542c\u9ea6\u514b\u98ce";
                    VoiceActivityDetail = "\u7cfb\u7edf\u6b63\u5728\u6355\u83b7\u97f3\u9891\uff0c\u4f46\u8fd8\u6ca1\u6709\u8fd4\u56de\u8f6c\u5199\u6587\u5b57\u3002";
                    break;
                case "SoundStarted":
                    VoiceLevelPercent = 46;
                    VoiceActivityText = "\u7cfb\u7edf\u68c0\u6d4b\u5230\u58f0\u97f3";
                    VoiceActivityDetail = "\u5df2\u6536\u5230\u58f0\u97f3\u4fe1\u53f7\uff0c\u6b63\u5728\u7b49\u5f85 Windows \u8bed\u97f3\u8bc6\u522b\u4ea7\u751f\u6587\u5b57\u3002";
                    break;
                case "SpeechDetected":
                    VoiceLevelPercent = 68;
                    VoiceActivityText = "\u7cfb\u7edf\u68c0\u6d4b\u5230\u8bed\u97f3";
                    VoiceActivityDetail = "\u5df2\u68c0\u6d4b\u5230\u8bf4\u8bdd\uff0c\u6b63\u5728\u7b49\u5f85\u5019\u9009\u6587\u672c\u3002";
                    break;
                case "Processing":
                case "SoundEnded":
                    VoiceLevelPercent = 84;
                    VoiceActivityText = "\u6b63\u5728\u6574\u7406\u8bed\u97f3";
                    VoiceActivityDetail = "\u7cfb\u7edf\u6b63\u5728\u5904\u7406\u521a\u624d\u7684\u8bed\u97f3\uff0c\u5982\u679c\u8bc6\u522b\u6210\u529f\u4f1a\u5199\u5165\u7f16\u8f91\u6846\u3002";
                    break;
                case "Idle":
                case "Paused":
                    VoiceLevelPercent = _hasLiveTranscript ? 28 : 8;
                    VoiceActivityText = "\u7cfb\u7edf\u6682\u672a\u8f93\u51fa\u6587\u5b57";
                    VoiceActivityDetail = "\u5b9e\u65f6\u8bed\u97f3\u4f1a\u8bdd\u4ecd\u5728\uff0c\u4f46\u5f53\u524d\u6ca1\u6709\u65b0\u7684\u8f6c\u5199\u6587\u5b57\u3002";
                    break;
            }
        }

        private void StartLiveVoiceWatchdogTimer()
        {
            StopLiveVoiceWatchdogTimer();

            var dispatcherQueue = _voiceDispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
            if (dispatcherQueue is null)
            {
                return;
            }

            _liveVoiceWatchdogTimer = dispatcherQueue.CreateTimer();
            _liveVoiceWatchdogTimer.Interval = TimeSpan.FromSeconds(1);
            _liveVoiceWatchdogTimer.Tick += OnLiveVoiceWatchdogTick;
            _liveVoiceWatchdogTimer.Start();
        }

        private void StopLiveVoiceWatchdogTimer()
        {
            if (_liveVoiceWatchdogTimer is null)
            {
                return;
            }

            _liveVoiceWatchdogTimer.Stop();
            _liveVoiceWatchdogTimer.Tick -= OnLiveVoiceWatchdogTick;
            _liveVoiceWatchdogTimer = null;
        }

        private void OnLiveVoiceWatchdogTick(DispatcherQueueTimer sender, object args)
        {
            if (!IsVoiceRecording || _isUserStoppingLiveVoiceInput)
            {
                return;
            }

            ApplyLiveVoiceWatchdogState();
        }

        private void ApplyLiveVoiceWatchdogState()
        {
            var now = DateTimeOffset.Now;
            var secondsSinceStart = (now - _liveVoiceStartedAt).TotalSeconds;
            var secondsSinceText = _lastLiveTranscriptAt == DateTimeOffset.MinValue
                ? double.PositiveInfinity
                : (now - _lastLiveTranscriptAt).TotalSeconds;

            if (!_hasLiveTranscript && secondsSinceStart >= 8)
            {
                VoiceLevelPercent = 0;
                VoiceActivityText = "\u7cfb\u7edf\u4f1a\u8bdd\u8fd0\u884c\uff0c\u4f46\u8fd8\u6ca1\u6709\u8f6c\u5199\u6587\u5b57";
                VoiceActivityDetail = string.IsNullOrWhiteSpace(_lastLiveSpeechState)
                    ? "\u8fd8\u6ca1\u6536\u5230 Windows \u8bed\u97f3\u8bc6\u522b\u7684\u72b6\u6001\u6216\u5019\u9009\u6587\u672c\u3002\u8bf7\u786e\u8ba4\u9ea6\u514b\u98ce\u6743\u9650\u3001\u9ed8\u8ba4\u8f93\u5165\u8bbe\u5907\u3001\u4e2d\u6587\u8bed\u97f3\u8bed\u8a00\u5305\u548c\u5728\u7ebf\u8bed\u97f3\u8bc6\u522b\u5df2\u5f00\u542f\u3002"
                    : $"\u6700\u540e\u4e00\u4e2a Windows \u72b6\u6001\uff1a{_lastLiveSpeechState}\u3002\u4f1a\u8bdd\u8fd8\u5728\u8fd0\u884c\uff0c\u4f46 Windows \u6ca1\u6709\u8fd4\u56de\u53ef\u5199\u5165\u7f16\u8f91\u6846\u7684\u6587\u5b57\u3002";
                VoiceStatusText = "\u5b9e\u65f6\u8bed\u97f3\u8f93\u5165\u4e2d\uff0c\u4f46\u8fd8\u6ca1\u6709\u6536\u5230\u53ef\u5199\u5165\u7684\u6587\u5b57\u3002";
                return;
            }

            if (_hasLiveTranscript && secondsSinceText >= 15)
            {
                VoiceLevelPercent = Math.Min(VoiceLevelPercent, 35);
                VoiceActivityText = "\u4ecd\u5728\u76d1\u542c\uff0c\u6700\u8fd1\u6ca1\u6709\u65b0\u6587\u5b57";
                VoiceActivityDetail = $"\u6700\u8fd1\u4e00\u6b21\u8f6c\u5199\u5728 {secondsSinceText:0} \u79d2\u524d\u3002\u4f60\u53ef\u4ee5\u7ee7\u7eed\u8bf4\uff0c\u6216\u70b9\u51fb\u505c\u6b62\u540e\u5148\u4fee\u6539\u5df2\u6709\u6587\u5b57\u3002";
                return;
            }

            if (!_hasLiveTranscript && secondsSinceStart >= 3 && VoiceLevelPercent < 20)
            {
                VoiceLevelPercent = 8;
                VoiceActivityText = "\u7b49\u5f85\u7b2c\u4e00\u6bb5\u8bed\u97f3\u6587\u5b57";
                VoiceActivityDetail = "\u4f1a\u8bdd\u5df2\u542f\u52a8\uff0c\u6b63\u5728\u7b49\u5f85 Windows \u4ea7\u751f\u5019\u9009\u6587\u672c\u3002";
            }
        }

        private async Task RestartLiveVoiceInputAfterUnexpectedCompletionAsync()
        {
            if (_isRestartingLiveVoiceInput || _isUserStoppingLiveVoiceInput)
            {
                return;
            }

            _isRestartingLiveVoiceInput = true;
            try
            {
                _liveVoicePromptPrefix = PromptText.Trim();
                _liveVoiceRestartAttempts++;
                var settings = _uiSettingsService.GetAsrRuntimeSettings();

                await Task.Delay(Math.Min(1000 + (_liveVoiceRestartAttempts * 300), 2500));
                if (_isUserStoppingLiveVoiceInput)
                {
                    return;
                }

                await _liveSpeechRecognitionService.StartAsync(settings.Language);
                _liveVoiceStartedAt = DateTimeOffset.Now;
                if (_isUserStoppingLiveVoiceInput)
                {
                    return;
                }

                IsVoiceRecording = true;
                VoiceLevelPercent = 12;
                VoiceActivityText = "\u5b9e\u65f6\u4f1a\u8bdd\u5df2\u91cd\u8fde\uff0c\u7b49\u5f85\u6587\u5b57";
                VoiceActivityDetail = "\u8bf7\u7ee7\u7eed\u8bf4\u3002\u5982\u679c\u4ecd\u6ca1\u6709\u6587\u5b57\u8fdb\u5165\u7f16\u8f91\u6846\uff0c\u8bf4\u660e Windows \u8bed\u97f3\u670d\u52a1\u6ca1\u6709\u8fd4\u56de\u8f6c\u5199\u7ed3\u679c\u3002";
                VoiceStatusText = "\u5b9e\u65f6\u8bed\u97f3\u8f93\u5165\u4e2d\uff0c\u518d\u70b9\u4e00\u6b21\u624d\u4f1a\u505c\u6b62\u3002";
                StatusMessage = "\u5b9e\u65f6\u8bed\u97f3\u8f93\u5165\u5df2\u6062\u590d\u3002";
            }
            catch (Exception ex)
            {
                IsVoiceRecording = false;
                ResetVoiceFeedbackToIdle();
                VoiceStatusText = $"\u5b9e\u65f6\u8bed\u97f3\u8f93\u5165\u5df2\u505c\u6b62\uff0c\u53ef\u4ee5\u518d\u70b9\u4e00\u6b21\u91cd\u8bd5\u3002{ex.Message}";
                StatusMessage = "\u5b9e\u65f6\u8bed\u97f3\u8f93\u5165\u6062\u590d\u5931\u8d25\u3002";
            }
            finally
            {
                _isRestartingLiveVoiceInput = false;
            }
        }

        private void EnqueueVoiceUiUpdate(Action update)
        {
            var dispatcherQueue = _voiceDispatcherQueue ?? DispatcherQueue.GetForCurrentThread();
            if (dispatcherQueue is null || dispatcherQueue.HasThreadAccess)
            {
                update();
                return;
            }

            dispatcherQueue.TryEnqueue(() => update());
        }

        private static string MergeLiveVoicePrompt(string prefix, string liveText)
        {
            var cleanPrefix = prefix.Trim();
            var cleanLiveText = liveText.Trim();
            return string.IsNullOrWhiteSpace(cleanPrefix)
                ? cleanLiveText
                : $"{cleanPrefix} {cleanLiveText}";
        }

        private void SetVoiceFeedbackActive(bool value)
        {
            if (_isVoiceFeedbackActive == value)
            {
                return;
            }

            _isVoiceFeedbackActive = value;
            OnPropertyChanged(nameof(VoiceFeedbackVisibility));
            OnPropertyChanged(nameof(VoiceButtonText));
        }

        private void StartVoiceFeedbackTimer()
        {
            StopVoiceFeedbackTimer();
            _hasDetectedVoiceDuringRecording = false;
            _voiceMeterPhase = 0;
            VoiceLevelPercent = 0;
            VoiceActivityText = "\u6b63\u5728\u76d1\u542c\u9ea6\u514b\u98ce";
            VoiceActivityDetail = "\u8bf4\u8bdd\u65f6\u8fd9\u91cc\u4f1a\u663e\u793a\u58f0\u97f3\u7535\u5e73\uff0c\u5982\u679c\u957f\u65f6\u95f4\u6ca1\u53d8\u5316\uff0c\u5c31\u53ef\u80fd\u6ca1\u6709\u6536\u5230\u9ea6\u514b\u98ce\u58f0\u97f3\u3002";
            SetVoiceFeedbackActive(true);

            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            if (dispatcherQueue is null)
            {
                VoiceActivityDetail = "\u5f53\u524d\u7ebf\u7a0b\u6ca1\u6709 UI Dispatcher\uff0c\u65e0\u6cd5\u542f\u52a8\u5b9e\u65f6\u7535\u5e73\u52a8\u753b\u3002";
                return;
            }

            _voiceFeedbackTimer = dispatcherQueue.CreateTimer();
            _voiceFeedbackTimer.Interval = TimeSpan.FromMilliseconds(180);
            _voiceFeedbackTimer.Tick += OnVoiceFeedbackTimerTick;
            _voiceFeedbackTimer.Start();
        }

        private void StopVoiceFeedbackTimer()
        {
            if (_voiceFeedbackTimer is null)
            {
                return;
            }

            _voiceFeedbackTimer.Stop();
            _voiceFeedbackTimer.Tick -= OnVoiceFeedbackTimerTick;
            _voiceFeedbackTimer = null;
            _isReadingVoiceLevel = false;
        }

        private async void OnVoiceFeedbackTimerTick(DispatcherQueueTimer sender, object args)
        {
            if (_isReadingVoiceLevel || !IsVoiceRecording)
            {
                return;
            }

            _isReadingVoiceLevel = true;
            try
            {
                var snapshot = await _voiceRecordingService.GetCurrentLevelAsync();
                if (IsVoiceRecording)
                {
                    ApplyVoiceLevelSnapshot(snapshot);
                }
            }
            finally
            {
                _isReadingVoiceLevel = false;
            }
        }

        private void ApplyVoiceLevelSnapshot(VoiceAudioLevelSnapshot snapshot)
        {
            if (!snapshot.IsAvailable)
            {
                VoiceLevelPercent = 0;
                VoiceActivityText = "\u6b63\u5728\u7b49\u5f85\u58f0\u97f3\u6570\u636e";
                VoiceActivityDetail = snapshot.Detail;
                return;
            }

            _hasDetectedVoiceDuringRecording |= snapshot.HasSpeech;
            VoiceLevelPercent = snapshot.LevelPercent;
            VoiceActivityText = snapshot.HasSpeech
                ? "\u5df2\u68c0\u6d4b\u5230\u58f0\u97f3"
                : "\u76ee\u524d\u504f\u5b89\u9759";
            VoiceActivityDetail = snapshot.HasSpeech
                ? $"\u9ea6\u514b\u98ce\u6709\u8f93\u5165\uff0c\u7535\u5e73 {snapshot.LevelPercent:0}%\uff0c\u8bf7\u7ee7\u7eed\u8bf4\u5b8c\u540e\u70b9\u51fb\u201c\u505c\u6b62\u5e76\u8bc6\u522b\u201d\u3002"
                : $"\u8fd8\u6ca1\u6709\u68c0\u6d4b\u5230\u660e\u663e\u8bf4\u8bdd\u58f0\uff0c\u7535\u5e73 {snapshot.LevelPercent:0}%\u3002";
        }

        private void SetVoiceRecognitionStage(string activityText, string activityDetail, double levelPercent = 0)
        {
            StopVoiceFeedbackTimer();
            SetVoiceFeedbackActive(true);
            VoiceLevelPercent = levelPercent;
            VoiceActivityText = activityText;
            VoiceActivityDetail = activityDetail;
        }

        private void ClearVoiceFeedback()
        {
            StopVoiceFeedbackTimer();
            StopLiveVoiceWatchdogTimer();
            VoiceLevelPercent = 0;
            SetVoiceFeedbackActive(false);
        }

        private void ResetVoiceFeedbackToIdle()
        {
            StopVoiceFeedbackTimer();
            StopLiveVoiceWatchdogTimer();
            _voiceMeterPhase = 0;
            VoiceLevelPercent = 0;
            VoiceActivityText = "\u9ea6\u514b\u98ce\u5f85\u547d\u4e2d";
            VoiceActivityDetail = "\u70b9\u51fb\u201c\u5b9e\u65f6\u8bed\u97f3\u8f93\u5165\u201d\u540e\uff0c\u4f60\u8bf4\u7684\u5185\u5bb9\u4f1a\u76f4\u63a5\u586b\u5230\u7f16\u8f91\u6846\u3002";
            SetVoiceFeedbackActive(false);
        }

        private async Task StreamTranscriptIntoPromptAsync(string transcript)
        {
            SetVoiceRecognitionStage(
                "\u8bc6\u522b\u5b8c\u6210\uff0c\u6b63\u5728\u586b\u5165\u7f16\u8f91\u680f",
                "\u4e3a\u4e86\u8ba9\u8f6c\u5199\u7ed3\u679c\u53ef\u89c1\uff0c\u6211\u4f1a\u50cf\u6253\u5b57\u4e00\u6837\u628a\u6587\u5b57\u586b\u5165\u4e0b\u65b9\u8f93\u5165\u6846\uFF0C\u7136\u540E\u518D\u4EA4\u7ED9 Agent\u3002",
                100);

            PromptText = string.Empty;
            foreach (var character in transcript)
            {
                PromptText += character;
                await Task.Delay(18);
            }

            await Task.Delay(220);
        }

        private void RaiseVoiceMeterChanged()
        {
            _voiceMeterPhase += 0.55;
            OnPropertyChanged(nameof(VoiceLevelText));
            OnPropertyChanged(nameof(VoiceMeterBar1Height));
            OnPropertyChanged(nameof(VoiceMeterBar2Height));
            OnPropertyChanged(nameof(VoiceMeterBar3Height));
            OnPropertyChanged(nameof(VoiceMeterBar4Height));
            OnPropertyChanged(nameof(VoiceMeterBar5Height));
        }

        private double CalculateVoiceMeterBarHeight(double offset)
        {
            var normalizedLevel = Math.Clamp(VoiceLevelPercent / 100, 0, 1);
            var wave = 0.72 + 0.28 * Math.Sin(_voiceMeterPhase + offset);
            return 8 + (42 * Math.Clamp(normalizedLevel * wave, 0, 1));
        }

        private async Task StopVoiceRecordingAndSendAsync()
        {
            string audioPath;
            try
            {
                audioPath = await _voiceRecordingService.StopAsync();
                IsVoiceRecording = false;
                SetVoiceRecognitionStage(
                    "\u5f55\u97f3\u5df2\u4fdd\u5b58\uff0c\u6b63\u5728\u8bc6\u522b",
                    _hasDetectedVoiceDuringRecording
                        ? "\u5f55\u97f3\u9636\u6bb5\u5df2\u68c0\u6d4b\u5230\u58f0\u97f3\uff0c\u73b0\u5728\u6b63\u5728\u8c03\u7528 ASR \u8f6c\u5199\u3002"
                        : "\u5f55\u97f3\u9636\u6bb5\u6ca1\u68c0\u6d4b\u5230\u660e\u663e\u58f0\u97f3\uff0c\u4ecd\u4f1a\u5c1d\u8bd5\u8bc6\u522b\uff0c\u5982\u5931\u8d25\u8bf7\u68c0\u67e5\u9ea6\u514b\u98ce\u3002");
            }
            catch (Exception ex)
            {
                await _voiceRecordingService.CancelAsync();
                IsVoiceRecording = false;
                ClearVoiceFeedback();
                VoiceStatusText = "\u8bed\u97f3\u5f55\u5236\u6ca1\u6709\u6210\u529f\u7ed3\u675f\u3002";
                Messages.Add(new AiConversationMessageModel
                {
                    Header = "\u8bed\u97f3\u8f93\u5165",
                    Body = "\u8fd9\u6b21\u5f55\u97f3\u6ca1\u6709\u4fdd\u5b58\u6210\u529f\u3002",
                    SupportingText = ex.Message,
                    TimestampText = DateTime.Now.ToString("HH:mm"),
                    IsUser = false
                });
                return;
            }

            SetBusyState(true);
            VoiceStatusText = "\u6b63\u5728\u8bc6\u522b\u8bed\u97f3\u2026";
            StatusMessage = "\u6b63\u5728\u8bc6\u522b\u8bed\u97f3\u2026";

            try
            {
                await TranscribeAndRunWorkflowAsync(audioPath, AudioInputKind.MicrophoneRecording);
            }
            catch (Exception ex)
            {
                VoiceStatusText = "\u8fd9\u6b21\u6ca1\u6709\u542c\u6e05\uff0c\u53ef\u4ee5\u91cd\u65b0\u5f55\u4e00\u6b21\u6216\u76f4\u63a5\u8f93\u5165\u6587\u672c\u3002";
                Messages.Add(new AiConversationMessageModel
                {
                    Header = "\u672c\u5730\u8fd0\u884c\u65f6",
                    Body = "\u8fd9\u6b21\u8bed\u97f3\u6ca1\u80fd\u8f6c\u6210\u53ef\u7528\u6307\u4ee4\u3002",
                    SupportingText = ex.Message,
                    TimestampText = DateTime.Now.ToString("HH:mm"),
                    IsUser = false
                });

                StatusMessage = "\u8bed\u97f3\u8bc6\u522b\u5931\u8d25\u3002";
            }
            finally
            {
                ClearVoiceFeedback();
                SetBusyState(false);
            }
        }

        private async Task TranscribeAndRunWorkflowAsync(string audioPath, AudioInputKind inputKind)
        {
            var result = await _speechRecognitionService.TranscribeAudioFileAsync(audioPath, inputKind);
            if (!result.Success)
            {
                SetVoiceRecognitionStage(
                    "\u8bed\u97f3\u6ca1\u6709\u8bc6\u522b\u6210\u529f",
                    $"{result.ErrorCode}: {result.Error}");
                VoiceStatusText = "\u8fd9\u6b21\u6ca1\u6709\u542c\u6e05\uff0c\u53ef\u4ee5\u91cd\u65b0\u5f55\u4e00\u6b21\u6216\u76f4\u63a5\u8f93\u5165\u6587\u672c\u3002";
                Messages.Add(new AiConversationMessageModel
                {
                    Header = "ASR",
                    Body = "\u8fd9\u6b21\u8bed\u97f3\u6ca1\u80fd\u8f6c\u6210\u53ef\u7528\u6587\u672c\u3002",
                    SupportingText = $"{result.ErrorCode}: {result.Error}",
                    TimestampText = DateTime.Now.ToString("HH:mm"),
                    IsUser = false
                });

                StatusMessage = "\u8bed\u97f3\u8bc6\u522b\u5931\u8d25\u3002";
                return;
            }

            var transcript = result.AgentInputText;
            VoiceStatusText = $"\u8bed\u97f3\u8bc6\u522b\uff1a{transcript}";
            await StreamTranscriptIntoPromptAsync(transcript);

            Messages.Add(new AiConversationMessageModel
            {
                Header = inputKind == AudioInputKind.UploadedFile ? "\u4f60\uff08\u97f3\u9891\u6587\u4ef6\uff09" : "\u4f60\uff08\u8bed\u97f3\uff09",
                Body = transcript,
                SupportingText = BuildTranscriptionSupportingText(audioPath, result),
                TimestampText = DateTime.Now.ToString("HH:mm"),
                IsUser = true
            });

            PromptText = string.Empty;
            SetVoiceRecognitionStage(
                "\u6587\u5b57\u5df2\u586b\u5165\uff0c\u6b63\u5728\u4ea4\u7ed9 Agent",
                "\u8f6c\u5199\u7ed3\u679c\u5df2\u7ecf\u8fdb\u5165\u73b0\u6709 Agent workflow\uff0c\u63a5\u4e0b\u6765\u4f1a\u6839\u636e\u5185\u5bb9\u81ea\u52a8\u5224\u65ad\u5165\u5e93\u6216\u51fa\u5e93\u3002",
                100);
            StatusMessage = "\u8bed\u97f3\u5df2\u8bc6\u522b\uff0cAI \u6b63\u5728\u6574\u7406\u8349\u7a3f\u2026";
            await RunPromptWorkflowAsync(transcript);
        }

        private async Task RunPromptWorkflowAsync(string prompt)
        {
            var conversationDraft = _latestSuggestion is not null
                && !_aiWorkflowToolService.CanCreateOrder(_latestSuggestion)
                    ? _latestSuggestion
                    : null;

            var execution = await _aiWorkflowAgentService.RunAutoAsync(
                prompt,
                SelectedProviderOption.Kind,
                conversationDraft);

            var suggestion = execution.Suggestion;
            if (suggestion is not null)
            {
                _latestSuggestion = suggestion;
            }
            else if (conversationDraft is null)
            {
                _latestSuggestion = null;
            }

            ReplaceAgentTrace(execution.TraceSteps);
            RaiseLatestDraftStateChanged();

            Messages.Add(new AiConversationMessageModel
            {
                Header = "AI \u52A9\u624B",
                Body = BuildAgentConversationReply(execution),
                SupportingText = suggestion is null ? string.Empty : BuildDraftSummary(suggestion),
                TimestampText = DateTime.Now.ToString("HH:mm"),
                IsUser = false
            });

            Messages.Add(new AiConversationMessageModel
            {
                Header = "Workflow Agent",
                Body = execution.Summary,
                SupportingText = BuildAgentTraceSummary(execution.TraceSteps),
                TimestampText = DateTime.Now.ToString("HH:mm"),
                IsUser = false
            });

            StatusMessage = execution.Summary;
        }

        private async Task WarmSelectedProviderSilentlyAsync()
        {
            try
            {
                await _localAiDraftAgentService.WarmProviderAsync(SelectedProviderOption.Kind);
            }
            catch
            {
                // Best-effort warm-up only.
            }
        }

        private AiDraftOperation GetEffectiveOperationForActions()
        {
            return _latestSuggestion?.Operation ?? SelectedOperationOption.Operation;
        }

        private static string BuildAssistantSupportingText(AiOrderDraftSuggestion suggestion)
        {
            var parts = new List<string>
            {
                $"{GetProviderDisplayName(suggestion.ProviderKind)} · confidence {suggestion.Confidence:0.##}"
            };

            if (!string.IsNullOrWhiteSpace(suggestion.AssistantMessage))
            {
                parts.Add(suggestion.AssistantMessage);
            }

            if (suggestion.Warnings.Count > 0)
            {
                parts.Add(string.Join(" | ", suggestion.Warnings.Take(2)));
            }

            return string.Join("  ", parts);
        }

        private static string BuildAgentConversationReply(AiWorkflowExecutionResult execution)
        {
            var suggestion = execution.Suggestion;
            if (execution.ToolName == AiWorkflowToolNames.AskForOperation || suggestion is null)
            {
                return execution.Summary;
            }

            if (execution.ToolName is AiWorkflowToolNames.PreviewInboundConfirmation or AiWorkflowToolNames.CreateOutboundOrder)
            {
                return "\u6536\u5230\uff0c\u4fe1\u606f\u5df2\u9f50\u5168\uff0c\u6b63\u5728\u521b\u5efa\u8ba2\u5355\u3002\u8bf7\u5728\u5f39\u51fa\u7684\u786e\u8ba4\u5355\u91cc\u590d\u6838\u540e\u518d\u786e\u8ba4\u3002";
            }

            if (suggestion.MissingFields.Count > 0)
            {
                return $"\u8fd8\u5dee {BuildMissingFieldText(suggestion.MissingFields)}\uff0c\u8bf7\u8865\u5145\u540e\u6211\u518d\u5e2e\u4f60\u521b\u5efa\u8ba2\u5355\u3002";
            }

            return "\u6211\u5df2\u7ecf\u6574\u7406\u51fa\u8349\u7a3f\uff0c\u4f46\u8fd8\u9700\u8981\u4f60\u6253\u5f00\u76ee\u6807\u9875\u590d\u6838\u540e\u7ee7\u7eed\u3002";
        }

        private static string BuildMissingFieldText(IReadOnlyList<string> missingFields)
        {
            return string.Join(
                "\u3001",
                missingFields.Select(field => field.ToLowerInvariant() switch
                {
                    "customer_name" => "\u5ba2\u6237",
                    "operation" => "\u4e1a\u52a1\u7c7b\u578b\uff08\u5165\u5e93/\u51fa\u5e93\uff09",
                    "category_name" => "\u5206\u7c7b",
                    "quantity" => "\u6570\u91cf",
                    "unit_price" => "\u5355\u4ef7",
                    _ => field
                }));
        }

        private static string BuildAgentTraceSummary(IReadOnlyList<AiWorkflowTraceStep> traceSteps)
        {
            if (traceSteps.Count == 0)
            {
                return string.Empty;
            }

            var builder = new StringBuilder();
            foreach (var step in traceSteps)
            {
                builder.AppendLine($"{step.StepLabel}. {step.Title} - {step.Status}");
                if (!string.IsNullOrWhiteSpace(step.Detail))
                {
                    builder.AppendLine(step.Detail);
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildTranscriptionSupportingText(string audioPath, TranscriptionResult result)
        {
            var parts = new List<string>
            {
                $"ASR={result.ProviderName}",
                $"language={result.Language}"
            };

            if (result.Duration.HasValue)
            {
                parts.Add($"duration={result.Duration.Value.TotalSeconds:0.#}s");
            }

            if (result.Confidence.HasValue)
            {
                parts.Add($"confidence={result.Confidence.Value:0.##}");
            }

            parts.Add($"audio={audioPath}");
            return string.Join("; ", parts);
        }

        private static string BuildDraftSummary(AiOrderDraftSuggestion suggestion)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"{GetOperationDisplayName(suggestion.Operation)}\u76EE\u6807\uFF1A{ResolveCustomerText(suggestion)}");

            if (suggestion.Lines.Count == 0)
            {
                builder.Append("\u8FD9\u6B21\u8FD8\u6CA1\u6709\u62FF\u5230\u53EF\u5E94\u7528\u7684\u5206\u7C7B\u884C\u3002");
                return builder.ToString();
            }

            foreach (var line in suggestion.Lines)
            {
                var unitType = line.InputUnitType ?? WeightUnit.Kilogram;
                var quantity = line.Quantity ?? 0;
                var unitPrice = line.UnitPrice ?? 0;
                builder.AppendLine($"- {line.CategoryName} · {quantity:0.##} {WeightUnitHelper.GetLabel(unitType)} · \u00A5{unitPrice:0.##}");
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildDraftWarnings(AiOrderDraftSuggestion suggestion)
        {
            var parts = new List<string>();

            if (suggestion.MissingFields.Count > 0)
            {
                parts.Add($"\u7F3A\u5931\u5B57\u6BB5\uFF1A{string.Join(", ", suggestion.MissingFields)}");
            }

            if (suggestion.Warnings.Count > 0)
            {
                parts.Add($"\u6CE8\u610F\u4E8B\u9879\uFF1A{string.Join(" | ", suggestion.Warnings)}");
            }

            return parts.Count == 0
                ? "\u6CA1\u6709\u989D\u5916\u8B66\u544A\u3002\u4F46\u4F60\u4ECD\u7136\u9700\u8981\u5728\u786E\u8BA4\u9875\u91CC\u68C0\u67E5\u5BA2\u6237\u3001\u6570\u91CF\u3001\u5355\u4EF7\u548C\u5E93\u5B58\u5F71\u54CD\u3002"
                : string.Join(Environment.NewLine, parts);
        }

        private string BuildCreateLatestOrderStatusText(AiOrderDraftSuggestion suggestion)
        {
            if (_aiWorkflowToolService.CanCreateOrder(suggestion))
            {
                return suggestion.Operation == AiDraftOperation.Outbound
                    ? "\u70B9\u51FB\u540E\u4F1A\u76F4\u63A5\u521B\u5EFA\u51FA\u5E93\u8BA2\u5355\u5E76\u6253\u5F00\u786E\u8BA4\u5355\u3002"
                    : "\u5982\u679C\u672C\u6B21 AI \u8349\u7A3F\u4FE1\u606F\u5B8C\u6574\uff0C\u53D1\u9001\u540E\u4F1A\u81EA\u52A8\u6253\u5F00\u5165\u5E93\u786E\u8BA4\u7A97\u53E3\u3002";
            }

            if (suggestion.Operation == AiDraftOperation.Outbound && suggestion.Lines.Count > 1)
            {
                return "\u5F53\u524D AI \u51FA\u5E93\u76F4\u521B\u4EC5\u652F\u6301\u5355\u884C\u8349\u7A3F\uFF0C\u8BF7\u5148\u6253\u5F00\u51FA\u5E93\u9875\u7EE7\u7EED\u7F16\u8F91\u3002";
            }

            return "\u76F4\u63A5\u521B\u5EFA\u8BA2\u5355\u9700\u8981\u5DF2\u786E\u5B9A\u7684\u5BA2\u6237\u3001\u5206\u7C7B\u3001\u6570\u91CF\u548C\u5355\u4EF7\u3002";
        }

        private static string ResolveCustomerText(AiOrderDraftSuggestion suggestion)
        {
            return string.IsNullOrWhiteSpace(suggestion.CustomerName)
                ? "\u5F85\u786E\u8BA4\u5BA2\u6237"
                : suggestion.CustomerName.Trim();
        }

        private static string GetOperationDisplayName(AiDraftOperation operation)
        {
            return operation == AiDraftOperation.Outbound ? "\u51FA\u5E93\u8349\u7A3F" : "\u5165\u5E93\u8349\u7A3F";
        }

        private static string GetProviderDisplayName(AiLocalProviderKind providerKind)
        {
            return providerKind switch
            {
                AiLocalProviderKind.Ollama => "Ollama",
                AiLocalProviderKind.HuggingFaceUltravoxPython => "HF Ultravox",
                AiLocalProviderKind.DeepSeekApi => "DeepSeek API",
                _ => providerKind.ToString()
            };
        }

        private void LoadDeepSeekApiSettings()
        {
            var settings = _uiSettingsService.GetDeepSeekApiRuntimeSettings();
            DeepSeekApiBaseUrl = settings.BaseUrl;
            DeepSeekApiModel = settings.Model;
            _deepSeekApiKey = settings.ApiKey;
        }
    }
}
