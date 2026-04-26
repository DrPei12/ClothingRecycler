namespace ClothingRecycler.Desktop.Services;

public interface IAudioTranscriptionProvider
{
    AsrProviderKind Kind { get; }

    string DisplayName { get; }

    IReadOnlySet<string> SupportedFileExtensions { get; }

    Task<AsrProviderProbeResult> ProbeAsync(CancellationToken cancellationToken = default);

    Task<TranscriptionResult> TranscribeAsync(
        AudioTranscriptionInput input,
        CancellationToken cancellationToken = default);
}
