namespace ClothingRecycler.Desktop.Services;

public interface ILocalAiProvider
{
    AiLocalProviderKind Kind { get; }

    Task<AiProviderProbeResult> ProbeAsync(CancellationToken cancellationToken = default);

    Task WarmAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    Task<string> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default);
}
