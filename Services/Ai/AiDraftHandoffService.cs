namespace ClothingRecycler.Desktop.Services;

public enum AiDraftLaunchMode
{
    Editor,
    ConfirmationPreview
}

public sealed class AiDraftHandoffRequest
{
    public required AiOrderDraftSuggestion Suggestion { get; init; }

    public AiDraftLaunchMode LaunchMode { get; init; } = AiDraftLaunchMode.Editor;
}

public sealed class AiDraftHandoffService
{
    private readonly object _syncRoot = new();
    private AiDraftHandoffRequest? _pendingInboundRequest;
    private AiDraftHandoffRequest? _pendingOutboundRequest;

    public void Store(AiOrderDraftSuggestion suggestion, AiDraftLaunchMode launchMode = AiDraftLaunchMode.Editor)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        var request = new AiDraftHandoffRequest
        {
            Suggestion = suggestion,
            LaunchMode = launchMode
        };

        lock (_syncRoot)
        {
            switch (suggestion.Operation)
            {
                case AiDraftOperation.Inbound:
                    _pendingInboundRequest = request;
                    break;
                case AiDraftOperation.Outbound:
                    _pendingOutboundRequest = request;
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported AI draft operation: {suggestion.Operation}");
            }
        }
    }

    public AiDraftHandoffRequest? Consume(AiDraftOperation operation)
    {
        lock (_syncRoot)
        {
            return operation switch
            {
                AiDraftOperation.Inbound => ConsumeInbound(),
                AiDraftOperation.Outbound => ConsumeOutbound(),
                _ => null
            };
        }
    }

    private AiDraftHandoffRequest? ConsumeInbound()
    {
        var request = _pendingInboundRequest;
        _pendingInboundRequest = null;
        return request;
    }

    private AiDraftHandoffRequest? ConsumeOutbound()
    {
        var request = _pendingOutboundRequest;
        _pendingOutboundRequest = null;
        return request;
    }
}
