namespace ClothingRecycler.Desktop.Models;

public static class AiWorkflowToolNames
{
    public const string AskForOperation = "ask_for_operation";
    public const string CreateInboundDraft = "create_inbound_draft";
    public const string PreviewInboundConfirmation = "preview_inbound_confirmation";
    public const string OpenInboundEditor = "open_inbound_editor";
    public const string CreateOutboundDraft = "create_outbound_draft";
    public const string CreateOutboundOrder = "create_outbound_order";
    public const string OpenOutboundEditor = "open_outbound_editor";
}

public sealed class AiWorkflowToolDefinition
{
    public required string Name { get; init; }

    public required string Description { get; init; }

    public required AiDraftOperation Operation { get; init; }

    public bool RequiresConfirmation { get; init; }

    public bool WritesData { get; init; }
}

public sealed class AiWorkflowExecutionResult
{
    public required string ToolName { get; init; }

    public bool Success { get; init; }

    public string Summary { get; init; } = string.Empty;

    public AiOrderDraftSuggestion? Suggestion { get; init; }

    public AiDraftLaunchMode? RecommendedLaunchMode { get; init; }

    public bool CanAutoExecute { get; init; }

    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public IReadOnlyList<AiWorkflowTraceStep> TraceSteps { get; init; } = Array.Empty<AiWorkflowTraceStep>();
}

public sealed class AiWorkflowTraceStep
{
    public int StepNumber { get; init; }

    public required string Title { get; init; }

    public required string Status { get; init; }

    public string Detail { get; init; } = string.Empty;

    public bool IsSuccess { get; init; } = true;

    public string StepLabel => StepNumber.ToString("00", CultureInfo.InvariantCulture);

    public Visibility DetailVisibility => string.IsNullOrWhiteSpace(Detail) ? Visibility.Collapsed : Visibility.Visible;
}
