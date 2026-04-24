using ClothingRecycler.Desktop.Models;

namespace ClothingRecycler.Desktop.Services;

public sealed class AiWorkflowToolService
{
    private readonly AiDraftHandoffService _draftHandoffService;
    private readonly ShellNavigationService _shellNavigationService;
    private readonly IReadOnlyDictionary<string, AiWorkflowToolDefinition> _definitions;

    public AiWorkflowToolService(
        AiDraftHandoffService draftHandoffService,
        ShellNavigationService shellNavigationService)
    {
        _draftHandoffService = draftHandoffService;
        _shellNavigationService = shellNavigationService;
        _definitions = BuildDefinitions();
    }

    public IReadOnlyList<AiWorkflowToolDefinition> GetDefinitions()
    {
        return _definitions.Values
            .OrderBy(definition => definition.Operation)
            .ThenBy(definition => definition.Name, StringComparer.Ordinal)
            .ToArray();
    }

    public string GetRecommendedToolName(AiOrderDraftSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        if (CanCreateOrder(suggestion))
        {
            return GetCreateOrderToolName(suggestion.Operation);
        }

        return suggestion.Operation switch
        {
            AiDraftOperation.Outbound => AiWorkflowToolNames.CreateOutboundDraft,
            _ => AiWorkflowToolNames.CreateInboundDraft
        };
    }

    public string GetCreateOrderToolName(AiDraftOperation operation)
    {
        return operation switch
        {
            AiDraftOperation.Outbound => AiWorkflowToolNames.CreateOutboundOrder,
            _ => AiWorkflowToolNames.PreviewInboundConfirmation
        };
    }

    public string GetOpenEditorToolName(AiDraftOperation operation)
    {
        return operation switch
        {
            AiDraftOperation.Outbound => AiWorkflowToolNames.OpenOutboundEditor,
            _ => AiWorkflowToolNames.OpenInboundEditor
        };
    }

    public bool CanCreateOrder(AiOrderDraftSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        if (string.IsNullOrWhiteSpace(suggestion.CustomerName))
        {
            return false;
        }

        if (suggestion.Lines.Count == 0)
        {
            return false;
        }

        if (suggestion.Operation == AiDraftOperation.Outbound && suggestion.Lines.Count != 1)
        {
            return false;
        }

        return suggestion.Lines.All(line =>
            line.ExistingCategoryId.HasValue
            && line.Quantity.HasValue
            && line.Quantity.Value > 0
            && line.UnitPrice.HasValue
            && line.UnitPrice.Value > 0);
    }

    public bool CanOpenEditor(AiOrderDraftSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);
        return !string.IsNullOrWhiteSpace(suggestion.CustomerName) || suggestion.Lines.Count > 0;
    }

    public AiWorkflowExecutionResult Execute(string toolName, AiOrderDraftSuggestion suggestion)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        if (!_definitions.TryGetValue(toolName, out var definition))
        {
            throw new InvalidOperationException($"Unknown AI workflow tool: {toolName}");
        }

        return toolName switch
        {
            AiWorkflowToolNames.CreateInboundDraft => BuildDraftOnlyResult(definition, suggestion),
            AiWorkflowToolNames.CreateOutboundDraft => BuildDraftOnlyResult(definition, suggestion),
            AiWorkflowToolNames.OpenInboundEditor => OpenEditor(definition, suggestion, "inbound", "入库页"),
            AiWorkflowToolNames.OpenOutboundEditor => OpenEditor(definition, suggestion, "outbound", "出库页"),
            AiWorkflowToolNames.PreviewInboundConfirmation => StartConfirmationPreview(definition, suggestion, "inbound", "入库确认流程"),
            AiWorkflowToolNames.CreateOutboundOrder => StartConfirmationPreview(definition, suggestion, "outbound", "出库创建流程"),
            _ => throw new InvalidOperationException($"Unsupported AI workflow tool: {toolName}")
        };
    }

    private static IReadOnlyDictionary<string, AiWorkflowToolDefinition> BuildDefinitions()
    {
        var definitions = new[]
        {
            new AiWorkflowToolDefinition
            {
                Name = AiWorkflowToolNames.CreateInboundDraft,
                Description = "整理自然语言并生成一张待确认的入库草稿，不直接跳转页面。",
                Operation = AiDraftOperation.Inbound,
                RequiresConfirmation = false,
                WritesData = false
            },
            new AiWorkflowToolDefinition
            {
                Name = AiWorkflowToolNames.PreviewInboundConfirmation,
                Description = "把完整的入库草稿送入入库确认流程，并弹出确认窗口等待人工确认。",
                Operation = AiDraftOperation.Inbound,
                RequiresConfirmation = true,
                WritesData = false
            },
            new AiWorkflowToolDefinition
            {
                Name = AiWorkflowToolNames.OpenInboundEditor,
                Description = "把入库草稿送入入库页，继续人工编辑和确认。",
                Operation = AiDraftOperation.Inbound,
                RequiresConfirmation = false,
                WritesData = false
            },
            new AiWorkflowToolDefinition
            {
                Name = AiWorkflowToolNames.CreateOutboundDraft,
                Description = "整理自然语言并生成一张待确认的出库草稿，不直接跳转页面。",
                Operation = AiDraftOperation.Outbound,
                RequiresConfirmation = false,
                WritesData = false
            },
            new AiWorkflowToolDefinition
            {
                Name = AiWorkflowToolNames.CreateOutboundOrder,
                Description = "把完整的出库草稿送入出库创建流程，并显示出库确认单。",
                Operation = AiDraftOperation.Outbound,
                RequiresConfirmation = true,
                WritesData = true
            },
            new AiWorkflowToolDefinition
            {
                Name = AiWorkflowToolNames.OpenOutboundEditor,
                Description = "把出库草稿送入出库页，继续人工编辑和确认。",
                Operation = AiDraftOperation.Outbound,
                RequiresConfirmation = false,
                WritesData = false
            }
        };

        return definitions.ToDictionary(definition => definition.Name, StringComparer.Ordinal);
    }

    private static AiWorkflowExecutionResult BuildDraftOnlyResult(
        AiWorkflowToolDefinition definition,
        AiOrderDraftSuggestion suggestion)
    {
        var summary = suggestion.Operation == AiDraftOperation.Outbound
            ? "出库草稿已生成，等待你继续确认或打开出库页编辑。"
            : "入库草稿已生成，等待你继续确认或打开入库页编辑。";

        return new AiWorkflowExecutionResult
        {
            ToolName = definition.Name,
            Success = true,
            Summary = summary,
            Suggestion = suggestion,
            RecommendedLaunchMode = null,
            CanAutoExecute = false,
            Warnings = suggestion.Warnings
        };
    }

    private AiWorkflowExecutionResult OpenEditor(
        AiWorkflowToolDefinition definition,
        AiOrderDraftSuggestion suggestion,
        string navigationTag,
        string targetName)
    {
        if (!CanOpenEditor(suggestion))
        {
            return BuildFailureResult(definition.Name, suggestion, $"当前草稿还不够完整，暂时不能直接打开{targetName}。");
        }

        _draftHandoffService.Store(suggestion, AiDraftLaunchMode.Editor);
        var navigated = _shellNavigationService.Navigate(navigationTag);

        var summary = navigated
            ? $"workflow agent 已把草稿送入{targetName}。"
            : $"workflow agent 已准备好草稿，等你打开{targetName}时会自动带入。";

        return new AiWorkflowExecutionResult
        {
            ToolName = definition.Name,
            Success = true,
            Summary = summary,
            Suggestion = suggestion,
            RecommendedLaunchMode = AiDraftLaunchMode.Editor,
            CanAutoExecute = navigated,
            Warnings = suggestion.Warnings
        };
    }

    private AiWorkflowExecutionResult StartConfirmationPreview(
        AiWorkflowToolDefinition definition,
        AiOrderDraftSuggestion suggestion,
        string navigationTag,
        string targetName)
    {
        if (!CanCreateOrder(suggestion))
        {
            return BuildFailureResult(definition.Name, suggestion, $"当前草稿还不能直接进入{targetName}，请先补齐客户、分类、数量和单价。");
        }

        _draftHandoffService.Store(suggestion, AiDraftLaunchMode.ConfirmationPreview);
        var navigated = _shellNavigationService.Navigate(navigationTag);

        var summary = navigated
            ? $"workflow agent 已把草稿送入{targetName}。"
            : $"workflow agent 已准备好{targetName}所需草稿，等你打开目标页时会自动继续。";

        return new AiWorkflowExecutionResult
        {
            ToolName = definition.Name,
            Success = true,
            Summary = summary,
            Suggestion = suggestion,
            RecommendedLaunchMode = AiDraftLaunchMode.ConfirmationPreview,
            CanAutoExecute = navigated,
            Warnings = suggestion.Warnings
        };
    }

    private static AiWorkflowExecutionResult BuildFailureResult(
        string toolName,
        AiOrderDraftSuggestion suggestion,
        string summary)
    {
        return new AiWorkflowExecutionResult
        {
            ToolName = toolName,
            Success = false,
            Summary = summary,
            Suggestion = suggestion,
            RecommendedLaunchMode = null,
            CanAutoExecute = false,
            Warnings = suggestion.Warnings
        };
    }
}
