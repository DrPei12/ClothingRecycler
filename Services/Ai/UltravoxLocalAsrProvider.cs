namespace ClothingRecycler.Desktop.Services;

public sealed class UltravoxLocalAsrProvider : IAudioTranscriptionProvider
{
    private readonly ILocalAiProvider _ultravoxProvider;

    public UltravoxLocalAsrProvider(IEnumerable<ILocalAiProvider> localAiProviders)
    {
        _ultravoxProvider = localAiProviders.First(provider => provider.Kind == AiLocalProviderKind.HuggingFaceUltravoxPython);
    }

    public AsrProviderKind Kind => AsrProviderKind.HuggingFaceUltravoxLocal;

    public string DisplayName => "Hugging Face Ultravox Local ASR";

    public IReadOnlySet<string> SupportedFileExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        ".wav"
    };

    public async Task<AsrProviderProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var result = await _ultravoxProvider.ProbeAsync(cancellationToken);
        return new AsrProviderProbeResult
        {
            Kind = Kind,
            IsAvailable = result.IsAvailable,
            Summary = result.IsAvailable
                ? "Local Ultravox ASR provider is ready."
                : "Local Ultravox ASR provider is not ready.",
            Detail = result.Detail
        };
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        AudioTranscriptionInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var rawText = await _ultravoxProvider.CompleteAsync(BuildRequest(input.FilePath), cancellationToken);
        return new TranscriptionResult
        {
            Success = true,
            ProviderKind = Kind,
            ProviderName = DisplayName,
            Text = rawText,
            Language = input.LanguageHint,
            Duration = input.Duration
        };
    }

    private static AiCompletionRequest BuildRequest(string audioPath)
    {
        var systemPrompt =
            """
            You are an ASR engine for a Chinese clothing recycling order-entry app.
            Transcribe the user's speech into plain text only.
            Preserve names, categories, quantities, units, prices, and words such as 入库, 出库, 公斤, 斤, 件, 单价, 每公斤.
            Do not create an order. Do not answer the user's request. Do not return JSON.
            """;

        return new AiCompletionRequest
        {
            AudioPath = audioPath,
            MaxNewTokens = 128,
            ResponseFormat = AiCompletionResponseFormat.Text,
            Messages =
            [
                new AiChatMessage { Role = "system", Content = systemPrompt },
                new AiChatMessage { Role = "user", Content = "Transcribe this audio. Return only the spoken sentence." }
            ]
        };
    }
}
