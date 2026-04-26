using Windows.Globalization;
using Windows.Media.SpeechRecognition;

namespace ClothingRecycler.Desktop.Services;

public sealed class LiveSpeechRecognitionUpdate
{
    public string Text { get; init; } = string.Empty;

    public string StableText { get; init; } = string.Empty;

    public string HypothesisText { get; init; } = string.Empty;

    public bool IsFinal { get; init; }

    public double? Confidence { get; init; }
}

public sealed class AiLiveSpeechRecognitionService : IDisposable
{
    private readonly object _syncRoot = new();
    private readonly List<string> _stableSegments = [];
    private SpeechRecognizer? _recognizer;
    private bool _isListening;

    public event EventHandler<LiveSpeechRecognitionUpdate>? TranscriptUpdated;

    public event EventHandler<string>? StatusChanged;

    public bool IsListening => _isListening;

    public async Task StartAsync(string languageTag = "zh-CN", CancellationToken cancellationToken = default)
    {
        if (_isListening)
        {
            return;
        }

        await StopAsync(cancellationToken);
        lock (_syncRoot)
        {
            _stableSegments.Clear();
        }

        try
        {
            await EnsureSpeechRecognitionAccessAsync(cancellationToken);

            var language = CreateLanguage(languageTag);
            _recognizer = new SpeechRecognizer(language);
            _recognizer.Timeouts.InitialSilenceTimeout = TimeSpan.FromHours(1);
            _recognizer.Timeouts.EndSilenceTimeout = TimeSpan.FromHours(1);
            _recognizer.Timeouts.BabbleTimeout = TimeSpan.FromHours(1);
            _recognizer.Constraints.Add(new SpeechRecognitionTopicConstraint(
                SpeechRecognitionScenario.Dictation,
                "ai-live-dictation"));

            _recognizer.HypothesisGenerated += OnHypothesisGenerated;
            _recognizer.StateChanged += OnStateChanged;
            _recognizer.ContinuousRecognitionSession.ResultGenerated += OnResultGenerated;
            _recognizer.ContinuousRecognitionSession.Completed += OnCompleted;

            StatusChanged?.Invoke(this, $"live_speech_initializing:{language.LanguageTag}");
            var compilation = await _recognizer.CompileConstraintsAsync().AsTask(cancellationToken);
            if (compilation.Status != SpeechRecognitionResultStatus.Success)
            {
                DisposeRecognizer();
                throw new InvalidOperationException($"Windows speech recognition is not ready: {compilation.Status}");
            }

            _isListening = true;
            StatusChanged?.Invoke(this, $"live_speech_listening:{language.LanguageTag}");
            await _recognizer.ContinuousRecognitionSession.StartAsync().AsTask(cancellationToken);
        }
        catch (Exception ex) when (IsSpeechPrivacyException(ex))
        {
            _isListening = false;
            DisposeRecognizer();
            throw new InvalidOperationException(BuildSpeechPrivacyGuidance(), ex);
        }
        catch
        {
            _isListening = false;
            DisposeRecognizer();
            throw;
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_recognizer is null)
        {
            _isListening = false;
            return;
        }

        try
        {
            if (_isListening)
            {
                await _recognizer.ContinuousRecognitionSession.StopAsync().AsTask(cancellationToken);
            }
        }
        catch
        {
            // Stop is best-effort; callers care most that resources are released.
        }
        finally
        {
            _isListening = false;
            StatusChanged?.Invoke(this, "live_speech_stopped");
            DisposeRecognizer();
        }
    }

    public void Dispose()
    {
        _isListening = false;
        DisposeRecognizer();
    }

    private void OnHypothesisGenerated(SpeechRecognizer sender, SpeechRecognitionHypothesisGeneratedEventArgs args)
    {
        var hypothesis = args.Hypothesis.Text?.Trim() ?? string.Empty;
        var stableText = GetStableText();
        TranscriptUpdated?.Invoke(this, new LiveSpeechRecognitionUpdate
        {
            Text = CombineSegments(stableText, hypothesis),
            StableText = stableText,
            HypothesisText = hypothesis,
            IsFinal = false
        });
    }

    private void OnStateChanged(SpeechRecognizer sender, SpeechRecognizerStateChangedEventArgs args)
    {
        StatusChanged?.Invoke(this, $"live_speech_state:{args.State}");
    }

    private void OnResultGenerated(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionResultGeneratedEventArgs args)
    {
        var result = args.Result;
        if (result.Status != SpeechRecognitionResultStatus.Success)
        {
            StatusChanged?.Invoke(this, $"live_speech_result_status:{result.Status}");
            return;
        }

        var text = result.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        string stableText;
        lock (_syncRoot)
        {
            if (_stableSegments.Count == 0
                || !string.Equals(_stableSegments[^1], text, StringComparison.OrdinalIgnoreCase))
            {
                _stableSegments.Add(text);
            }

            stableText = string.Join(" ", _stableSegments);
        }

        TranscriptUpdated?.Invoke(this, new LiveSpeechRecognitionUpdate
        {
            Text = stableText,
            StableText = stableText,
            HypothesisText = string.Empty,
            IsFinal = true,
            Confidence = MapConfidence(result.Confidence)
        });
    }

    private void OnCompleted(SpeechContinuousRecognitionSession sender, SpeechContinuousRecognitionCompletedEventArgs args)
    {
        _isListening = false;
        StatusChanged?.Invoke(this, $"live_speech_completed:{args.Status}");
    }

    private string GetStableText()
    {
        lock (_syncRoot)
        {
            return string.Join(" ", _stableSegments);
        }
    }

    private void DisposeRecognizer()
    {
        if (_recognizer is null)
        {
            return;
        }

        _recognizer.HypothesisGenerated -= OnHypothesisGenerated;
        _recognizer.StateChanged -= OnStateChanged;
        _recognizer.ContinuousRecognitionSession.ResultGenerated -= OnResultGenerated;
        _recognizer.ContinuousRecognitionSession.Completed -= OnCompleted;
        _recognizer.Dispose();
        _recognizer = null;
    }

    private async Task EnsureSpeechRecognitionAccessAsync(CancellationToken cancellationToken)
    {
        StatusChanged?.Invoke(this, "live_speech_requesting_access");
        await Task.CompletedTask;
    }

    private static string BuildSpeechPrivacyGuidance()
    {
        return "Windows 实时语音识别需要先同意语音隐私策略。请打开 Windows 设置 -> 隐私和安全性 -> 语音，开启“在线语音识别”；同时在 隐私和安全性 -> 麦克风 中允许桌面应用访问麦克风。完成后回到本页重新点击“实时语音输入”。";
    }

    private static bool IsSpeechPrivacyException(Exception ex)
    {
        if (ex.Message.Contains("speech privacy policy", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ex.InnerException is not null && IsSpeechPrivacyException(ex.InnerException);
    }

    private static Language CreateLanguage(string languageTag)
    {
        try
        {
            return new Language(string.IsNullOrWhiteSpace(languageTag) ? "zh-CN" : languageTag);
        }
        catch
        {
            return SpeechRecognizer.SystemSpeechLanguage;
        }
    }

    private static string CombineSegments(string stableText, string hypothesis)
    {
        if (string.IsNullOrWhiteSpace(stableText))
        {
            return hypothesis.Trim();
        }

        if (string.IsNullOrWhiteSpace(hypothesis))
        {
            return stableText.Trim();
        }

        return $"{stableText.Trim()} {hypothesis.Trim()}";
    }

    private static double MapConfidence(SpeechRecognitionConfidence confidence)
    {
        return confidence switch
        {
            SpeechRecognitionConfidence.High => 0.92,
            SpeechRecognitionConfidence.Medium => 0.72,
            SpeechRecognitionConfidence.Low => 0.48,
            SpeechRecognitionConfidence.Rejected => 0.12,
            _ => 0.5
        };
    }
}
