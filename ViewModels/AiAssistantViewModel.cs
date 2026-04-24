using System.Collections.ObjectModel;
using System.Text;

using ClothingRecycler.Desktop.Models;
using ClothingRecycler.Desktop.Services;

using Microsoft.UI.Xaml;

namespace ClothingRecycler.Desktop.ViewModels
{
    public sealed class AiAssistantViewModel : ViewModelBase
    {
        private readonly LocalAiDraftAgentService _localAiDraftAgentService;
        private readonly AiWorkflowAgentService _aiWorkflowAgentService;
        private readonly AiWorkflowToolService _aiWorkflowToolService;
        private readonly AiVoiceRecordingService _voiceRecordingService;

        private string _promptText = string.Empty;
        private AiProviderOptionModel _selectedProviderOption;
        private AiOperationOptionModel _selectedOperationOption;
        private string _providerStatusSummary = "\u672A\u68C0\u67E5\u672C\u5730 Provider \u72B6\u6001\u3002";
        private string _providerStatusDetail = "\u8FDB\u5165\u9875\u9762\u540E\u4F1A\u81EA\u52A8\u63A2\u6D4B\u4E00\u6B21\u672C\u5730 AI \u8FD0\u884C\u65F6\u3002";
        private string _voiceStatusText = string.Empty;
        private AiOrderDraftSuggestion? _latestSuggestion;
        private bool _isVoiceRecording;
        private bool _hasLoaded;

        public AiAssistantViewModel(
            LocalAiDraftAgentService localAiDraftAgentService,
            AiWorkflowAgentService aiWorkflowAgentService,
            AiWorkflowToolService aiWorkflowToolService,
            AiVoiceRecordingService voiceRecordingService)
        {
            _localAiDraftAgentService = localAiDraftAgentService;
            _aiWorkflowAgentService = aiWorkflowAgentService;
            _aiWorkflowToolService = aiWorkflowToolService;
            _voiceRecordingService = voiceRecordingService;

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

        public bool CanSend => !IsBusy && !string.IsNullOrWhiteSpace(PromptText);

        public bool IsVoiceRecording
        {
            get => _isVoiceRecording;
            private set
            {
                if (SetProperty(ref _isVoiceRecording, value))
                {
                    OnPropertyChanged(nameof(VoiceButtonText));
                    OnPropertyChanged(nameof(CanToggleVoiceRecording));
                }
            }
        }

        public bool CanToggleVoiceRecording => IsVoiceRecording || !IsBusy;

        public string VoiceButtonText => IsVoiceRecording ? "\u505c\u6b62\u5e76\u8bc6\u522b" : "\u8bed\u97f3\u8f93\u5165";

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
                await StopVoiceRecordingAndSendAsync();
                return;
            }

            if (IsBusy)
            {
                return;
            }

            try
            {
                await _voiceRecordingService.StartAsync();
                IsVoiceRecording = true;
                VoiceStatusText = "\u6b63\u5728\u5f55\u97f3\uff0c\u518d\u70b9\u4e00\u6b21\u505c\u6b62\u5e76\u8bc6\u522b\u3002";
                StatusMessage = "\u6b63\u5728\u5f55\u97f3\u2026";
            }
            catch (Exception ex)
            {
                VoiceStatusText = "\u65e0\u6cd5\u542f\u52a8\u9ea6\u514b\u98ce\u5f55\u97f3\u3002";
                Messages.Add(new AiConversationMessageModel
                {
                    Header = "\u8bed\u97f3\u8f93\u5165",
                    Body = "\u6ca1\u80fd\u542f\u52a8\u9ea6\u514b\u98ce\u3002",
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
        }

        private async Task StopVoiceRecordingAndSendAsync()
        {
            string audioPath;
            try
            {
                audioPath = await _voiceRecordingService.StopAsync();
                IsVoiceRecording = false;
            }
            catch (Exception ex)
            {
                await _voiceRecordingService.CancelAsync();
                IsVoiceRecording = false;
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
                var transcript = await _localAiDraftAgentService.TranscribeAudioAsync(audioPath, SelectedProviderOption.Kind);
                VoiceStatusText = $"\u8bed\u97f3\u8bc6\u522b\uff1a{transcript}";

                Messages.Add(new AiConversationMessageModel
                {
                    Header = "\u4f60\uff08\u8bed\u97f3\uff09",
                    Body = transcript,
                    SupportingText = $"\u5f55\u97f3\u6587\u4ef6\uff1a{audioPath}",
                    TimestampText = DateTime.Now.ToString("HH:mm"),
                    IsUser = true
                });

                StatusMessage = "\u8bed\u97f3\u5df2\u8bc6\u522b\uff0cAI \u6b63\u5728\u6574\u7406\u8349\u7a3f\u2026";
                await RunPromptWorkflowAsync(transcript);
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
                SetBusyState(false);
            }
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
                _ => providerKind.ToString()
            };
        }
    }
}
