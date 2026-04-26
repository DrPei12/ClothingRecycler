namespace ClothingRecycler.Desktop.Models;

public enum AsrProviderKind
{
    HuggingFaceUltravoxLocal,
    WhisperCppLocal,
    FasterWhisperLocal,
    OpenAiWhisperApi,
    GoogleSpeechToText,
    AzureSpeech,
    BrowserWebSpeech
}

public enum AudioInputKind
{
    MicrophoneRecording,
    UploadedFile
}

public enum AsrErrorCode
{
    None,
    MissingFile,
    EmptyFile,
    UnsupportedFormat,
    NoSpeechDetected,
    AudioTooShort,
    AudioTooLong,
    FileTooLarge,
    ProviderUnavailable,
    ProviderTimeout,
    RecognitionFailed,
    EmptyTranscript
}

public sealed class AudioTranscriptionInput
{
    public required string FilePath { get; init; }

    public AudioInputKind InputKind { get; init; }

    public string LanguageHint { get; init; } = "zh-CN";

    public TimeSpan? Duration { get; init; }

    public long SizeBytes { get; init; }

    public int? SampleRate { get; init; }

    public int? Channels { get; init; }

    public string Format { get; init; } = string.Empty;
}

public sealed class TranscriptionSegment
{
    public TimeSpan Start { get; init; }

    public TimeSpan End { get; init; }

    public string Text { get; init; } = string.Empty;

    public double? Confidence { get; init; }
}

public sealed class TranscriptionResult
{
    public bool Success { get; init; }

    public AsrProviderKind ProviderKind { get; init; }

    public string ProviderName { get; init; } = string.Empty;

    public string Text { get; init; } = string.Empty;

    public string NormalizedText { get; init; } = string.Empty;

    public string AgentInputText { get; init; } = string.Empty;

    public string Language { get; init; } = "zh-CN";

    public double? Confidence { get; init; }

    public TimeSpan? Duration { get; init; }

    public IReadOnlyList<TranscriptionSegment> Segments { get; init; } = Array.Empty<TranscriptionSegment>();

    public AsrErrorCode ErrorCode { get; init; } = AsrErrorCode.None;

    public string Error { get; init; } = string.Empty;

    public static TranscriptionResult Failed(
        AsrProviderKind providerKind,
        string providerName,
        AsrErrorCode errorCode,
        string error,
        TimeSpan? duration = null)
    {
        return new TranscriptionResult
        {
            Success = false,
            ProviderKind = providerKind,
            ProviderName = providerName,
            ErrorCode = errorCode,
            Error = error,
            Duration = duration
        };
    }
}

public sealed class AsrProviderProbeResult
{
    public AsrProviderKind Kind { get; init; }

    public bool IsAvailable { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
}
