namespace ClothingRecycler.Desktop.Models;

public sealed class AiConversationMessageModel
{
    public required string Header { get; init; }

    public required string Body { get; init; }

    public string SupportingText { get; init; } = string.Empty;

    public string TimestampText { get; init; } = DateTime.Now.ToString("HH:mm");

    public bool IsUser { get; init; }

    public Visibility UserBubbleVisibility => IsUser ? Visibility.Visible : Visibility.Collapsed;

    public Visibility AssistantBubbleVisibility => IsUser ? Visibility.Collapsed : Visibility.Visible;

    public Visibility SupportingTextVisibility => string.IsNullOrWhiteSpace(SupportingText) ? Visibility.Collapsed : Visibility.Visible;
}

public sealed class AiProviderOptionModel
{
    public required AiLocalProviderKind Kind { get; init; }

    public required string DisplayName { get; init; }

    public required string Description { get; init; }
}

public sealed class AiOperationOptionModel
{
    public required AiDraftOperation Operation { get; init; }

    public required string DisplayName { get; init; }

    public required string Description { get; init; }
}
