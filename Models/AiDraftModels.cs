namespace ClothingRecycler.Desktop.Models;

public enum AiDraftOperation
{
    Inbound,
    Outbound
}

public sealed class AiChatMessage
{
    public required string Role { get; init; }

    public required string Content { get; init; }
}

public sealed class AiCompletionRequest
{
    public IReadOnlyList<AiChatMessage> Messages { get; init; } = Array.Empty<AiChatMessage>();

    public string? AudioPath { get; init; }

    public int MaxNewTokens { get; init; } = 512;
}

public sealed class AiProviderProbeResult
{
    public AiLocalProviderKind Kind { get; init; }

    public bool IsAvailable { get; init; }

    public string Summary { get; init; } = string.Empty;

    public string Detail { get; init; } = string.Empty;
}

public sealed class AiOrderDraftLineSuggestion
{
    public string CategoryName { get; init; } = string.Empty;

    public long? ExistingCategoryId { get; set; }

    public double? Quantity { get; init; }

    public WeightUnit? InputUnitType { get; init; }

    public double? UnitPrice { get; init; }

    public IReadOnlyList<string> Warnings { get; set; } = Array.Empty<string>();
}

public sealed class AiOrderDraftSuggestion
{
    public AiDraftOperation Operation { get; init; }

    public AiLocalProviderKind ProviderKind { get; init; }

    public double Confidence { get; init; }

    public string CustomerName { get; init; } = string.Empty;

    public long? ExistingCustomerId { get; set; }

    public IReadOnlyList<AiOrderDraftLineSuggestion> Lines { get; init; } = Array.Empty<AiOrderDraftLineSuggestion>();

    public IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public string AssistantMessage { get; init; } = string.Empty;

    public string RawModelText { get; init; } = string.Empty;
}
