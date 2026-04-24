using ClothingRecycler.Desktop.Models;
using System.Text.RegularExpressions;

namespace ClothingRecycler.Desktop.Services;

public sealed class AiWorkflowAgentService
{
    private readonly LocalAiDraftAgentService _localAiDraftAgentService;
    private readonly AiDraftNormalizationToolService _draftNormalizationToolService;
    private readonly AiWorkflowToolService _workflowToolService;

    public AiWorkflowAgentService(
        LocalAiDraftAgentService localAiDraftAgentService,
        AiDraftNormalizationToolService draftNormalizationToolService,
        AiWorkflowToolService workflowToolService)
    {
        _localAiDraftAgentService = localAiDraftAgentService;
        _draftNormalizationToolService = draftNormalizationToolService;
        _workflowToolService = workflowToolService;
    }

    public async Task<AiWorkflowExecutionResult> RunAutoAsync(
        string userInput,
        AiLocalProviderKind providerKind,
        AiOrderDraftSuggestion? conversationDraft = null,
        CancellationToken cancellationToken = default)
    {
        var traceSteps = new List<AiWorkflowTraceStep>
        {
            CreateTraceStep(
                1,
                "\u63a5\u6536\u6307\u4ee4",
                "\u5df2\u63a5\u6536",
                $"operation=auto; provider={providerKind}; input={userInput.Trim()}")
        };

        var explicitOperation = DetectOperation(userInput);
        var operation = explicitOperation ?? conversationDraft?.Operation;
        traceSteps.Add(CreateTraceStep(
            2,
            "\u610f\u56fe\u8bc6\u522b",
            operation.HasValue ? "\u5df2\u786e\u8ba4" : "\u9700\u8981\u8865\u5145",
            $"explicit_operation={explicitOperation?.ToString() ?? "missing"}; memory_operation={conversationDraft?.Operation.ToString() ?? "none"}; resolved_operation={operation?.ToString() ?? "missing"}",
            operation.HasValue));

        if (!operation.HasValue)
        {
            return new AiWorkflowExecutionResult
            {
                ToolName = AiWorkflowToolNames.AskForOperation,
                Success = false,
                Summary = "\u8bf7\u5148\u544a\u8bc9\u6211\u8fd9\u662f\u5165\u5e93\u8fd8\u662f\u51fa\u5e93\uff0c\u4f8b\u5982\uff1a\u201c\u5218\u5efa\u658c\u5165\u5e93\u7fbd\u7ed2\u670d33\u516c\u65a4\uff0c2\u5143\u6bcf\u516c\u65a4\u201d\u3002",
                Suggestion = null,
                RecommendedLaunchMode = null,
                CanAutoExecute = false,
                Warnings = Array.Empty<string>(),
                TraceSteps = traceSteps
            };
        }

        var draftForMerge = conversationDraft is not null && conversationDraft.Operation == operation.Value
            ? conversationDraft
            : null;

        return await RunResolvedOperationAsync(
            operation.Value,
            userInput,
            providerKind,
            draftForMerge,
            traceSteps,
            nextStepNumber: 3,
            cancellationToken);
    }

    public async Task<AiWorkflowExecutionResult> RunAsync(
        AiDraftOperation operation,
        string userInput,
        AiLocalProviderKind providerKind,
        AiOrderDraftSuggestion? conversationDraft = null,
        CancellationToken cancellationToken = default)
    {
        var traceSteps = new List<AiWorkflowTraceStep>
        {
            CreateTraceStep(
                1,
                "\u63a5\u6536\u6307\u4ee4",
                "\u5df2\u63a5\u6536",
                $"operation={operation}; provider={providerKind}; input={userInput.Trim()}")
        };

        return await RunResolvedOperationAsync(
            operation,
            userInput,
            providerKind,
            conversationDraft,
            traceSteps,
            nextStepNumber: 2,
            cancellationToken);
    }

    private async Task<AiWorkflowExecutionResult> RunResolvedOperationAsync(
        AiDraftOperation operation,
        string userInput,
        AiLocalProviderKind providerKind,
        AiOrderDraftSuggestion? conversationDraft,
        List<AiWorkflowTraceStep> traceSteps,
        int nextStepNumber,
        CancellationToken cancellationToken)
    {
        var suggestion = operation switch
        {
            AiDraftOperation.Outbound => await _localAiDraftAgentService.SuggestOutboundDraftAsync(userInput, providerKind, cancellationToken),
            _ => await _localAiDraftAgentService.SuggestInboundDraftAsync(userInput, providerKind, cancellationToken)
        };

        traceSteps.Add(CreateTraceStep(
            nextStepNumber++,
            "\u6a21\u578b\u8349\u7a3f",
            "\u5df2\u751f\u6210",
            SummarizeSuggestion(suggestion)));

        var normalizedSuggestion = await _draftNormalizationToolService.NormalizeAsync(userInput, suggestion, cancellationToken);
        traceSteps.Add(CreateTraceStep(
            nextStepNumber++,
            "\u89c4\u8303\u5316\u5de5\u5177",
            "\u5df2\u6267\u884c",
            $"before: {SummarizeSuggestion(suggestion)}{Environment.NewLine}after: {SummarizeSuggestion(normalizedSuggestion)}"));

        suggestion = normalizedSuggestion;

        if (conversationDraft is not null && conversationDraft.Operation == operation)
        {
            var mergedSuggestion = MergeWithConversationDraft(conversationDraft, suggestion);
            traceSteps.Add(CreateTraceStep(
                nextStepNumber++,
                "\u4f1a\u8bdd\u8bb0\u5fc6\u5408\u5e76",
                "\u5df2\u6267\u884c",
                $"memory: {SummarizeSuggestion(conversationDraft)}{Environment.NewLine}current: {SummarizeSuggestion(suggestion)}{Environment.NewLine}merged: {SummarizeSuggestion(mergedSuggestion)}"));
            suggestion = mergedSuggestion;
        }

        var toolName = _workflowToolService.GetRecommendedToolName(suggestion);
        var canCreate = _workflowToolService.CanCreateOrder(suggestion);
        traceSteps.Add(CreateTraceStep(
            nextStepNumber++,
            "\u5de5\u5177\u9009\u62e9",
            canCreate ? "\u53ef\u8fdb\u5165\u786e\u8ba4\u6d41" : "\u4ec5\u4fdd\u7559\u8349\u7a3f",
            $"selected_tool={toolName}; can_create_order={canCreate}; missing_fields={FormatList(suggestion.MissingFields)}",
            canCreate));

        var result = _workflowToolService.Execute(toolName, suggestion);
        traceSteps.Add(CreateTraceStep(
            nextStepNumber,
            "\u5de5\u5177\u6267\u884c",
            result.Success ? "\u6210\u529f" : "\u5931\u8d25",
            $"tool={result.ToolName}; auto_execute={result.CanAutoExecute}; launch_mode={result.RecommendedLaunchMode?.ToString() ?? "none"}; summary={result.Summary}",
            result.Success));

        return new AiWorkflowExecutionResult
        {
            ToolName = result.ToolName,
            Success = result.Success,
            Summary = result.Summary,
            Suggestion = result.Suggestion,
            RecommendedLaunchMode = result.RecommendedLaunchMode,
            CanAutoExecute = result.CanAutoExecute,
            Warnings = result.Warnings,
            TraceSteps = traceSteps
        };
    }

    private static AiDraftOperation? DetectOperation(string userInput)
    {
        var inboundMatch = InboundIntentRegex.IsMatch(userInput);
        var outboundMatch = OutboundIntentRegex.IsMatch(userInput);

        return (inboundMatch, outboundMatch) switch
        {
            (true, false) => AiDraftOperation.Inbound,
            (false, true) => AiDraftOperation.Outbound,
            _ => null
        };
    }

    private static AiOrderDraftSuggestion MergeWithConversationDraft(
        AiOrderDraftSuggestion memory,
        AiOrderDraftSuggestion current)
    {
        var mergedLines = MergeLines(memory.Lines, current.Lines);
        var customerName = !string.IsNullOrWhiteSpace(current.CustomerName)
            ? current.CustomerName.Trim()
            : memory.CustomerName.Trim();

        var missingFields = RecalculateMissingFields(customerName, mergedLines);
        var warnings = memory.Warnings
            .Concat(current.Warnings)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new AiOrderDraftSuggestion
        {
            Operation = current.Operation,
            ProviderKind = current.ProviderKind,
            Confidence = Math.Max(memory.Confidence, current.Confidence),
            CustomerName = customerName,
            ExistingCustomerId = current.ExistingCustomerId ?? memory.ExistingCustomerId,
            Lines = mergedLines,
            MissingFields = missingFields,
            Warnings = warnings,
            AssistantMessage = current.AssistantMessage,
            RawModelText = current.RawModelText
        };
    }

    private static IReadOnlyList<AiOrderDraftLineSuggestion> MergeLines(
        IReadOnlyList<AiOrderDraftLineSuggestion> memoryLines,
        IReadOnlyList<AiOrderDraftLineSuggestion> currentLines)
    {
        if (memoryLines.Count == 0)
        {
            return currentLines;
        }

        if (currentLines.Count == 0)
        {
            return memoryLines;
        }

        var mergedLines = new List<AiOrderDraftLineSuggestion>();
        var lineCount = Math.Max(memoryLines.Count, currentLines.Count);
        for (var index = 0; index < lineCount; index++)
        {
            var memoryLine = index < memoryLines.Count ? memoryLines[index] : null;
            var currentLine = index < currentLines.Count ? currentLines[index] : null;

            if (memoryLine is null)
            {
                mergedLines.Add(currentLine!);
                continue;
            }

            if (currentLine is null)
            {
                mergedLines.Add(memoryLine);
                continue;
            }

            var hasCurrentCategory = HasCategory(currentLine);
            var hasCurrentQuantity = currentLine.Quantity.HasValue && currentLine.Quantity.Value > 0;
            var hasCurrentUnitPrice = currentLine.UnitPrice.HasValue && currentLine.UnitPrice.Value > 0;

            mergedLines.Add(new AiOrderDraftLineSuggestion
            {
                CategoryName = hasCurrentCategory ? currentLine.CategoryName : memoryLine.CategoryName,
                ExistingCategoryId = hasCurrentCategory ? currentLine.ExistingCategoryId : memoryLine.ExistingCategoryId,
                Quantity = hasCurrentQuantity ? currentLine.Quantity : memoryLine.Quantity,
                InputUnitType = hasCurrentQuantity
                    ? currentLine.InputUnitType ?? memoryLine.InputUnitType
                    : memoryLine.InputUnitType ?? currentLine.InputUnitType,
                UnitPrice = hasCurrentUnitPrice ? currentLine.UnitPrice : memoryLine.UnitPrice,
                Warnings = memoryLine.Warnings
                    .Concat(currentLine.Warnings)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray()
            });
        }

        return mergedLines;
    }

    private static IReadOnlyList<string> RecalculateMissingFields(
        string customerName,
        IReadOnlyList<AiOrderDraftLineSuggestion> lines)
    {
        var missingFields = new List<string>();

        if (string.IsNullOrWhiteSpace(customerName))
        {
            missingFields.Add("customer_name");
        }

        if (lines.Count == 0 || lines.Any(line => !HasCategory(line)))
        {
            missingFields.Add("category_name");
        }

        if (lines.Count == 0 || lines.Any(line => !line.Quantity.HasValue || line.Quantity.Value <= 0))
        {
            missingFields.Add("quantity");
        }

        if (lines.Count == 0 || lines.Any(line => !line.UnitPrice.HasValue || line.UnitPrice.Value <= 0))
        {
            missingFields.Add("unit_price");
        }

        return missingFields;
    }

    private static bool HasCategory(AiOrderDraftLineSuggestion line)
    {
        return line.ExistingCategoryId.HasValue || !string.IsNullOrWhiteSpace(line.CategoryName);
    }

    private static AiWorkflowTraceStep CreateTraceStep(
        int stepNumber,
        string title,
        string status,
        string detail,
        bool isSuccess = true)
    {
        return new AiWorkflowTraceStep
        {
            StepNumber = stepNumber,
            Title = title,
            Status = status,
            Detail = detail,
            IsSuccess = isSuccess
        };
    }

    private static string SummarizeSuggestion(AiOrderDraftSuggestion suggestion)
    {
        var lines = suggestion.Lines.Count == 0
            ? "lines=0"
            : string.Join("; ", suggestion.Lines.Select(line =>
            {
                var quantity = line.Quantity.HasValue ? line.Quantity.Value.ToString("0.##", CultureInfo.InvariantCulture) : "missing";
                var unit = line.InputUnitType?.ToString() ?? "missing";
                var price = line.UnitPrice.HasValue ? line.UnitPrice.Value.ToString("0.##", CultureInfo.InvariantCulture) : "missing";
                var category = string.IsNullOrWhiteSpace(line.CategoryName) ? "missing" : line.CategoryName;
                var categoryId = line.ExistingCategoryId?.ToString(CultureInfo.InvariantCulture) ?? "missing";
                return $"category={category}#{categoryId}, quantity={quantity}, unit={unit}, price={price}";
            }));

        var customer = string.IsNullOrWhiteSpace(suggestion.CustomerName) ? "missing" : suggestion.CustomerName.Trim();
        var customerId = suggestion.ExistingCustomerId?.ToString(CultureInfo.InvariantCulture) ?? "new-or-missing";
        return $"customer={customer}#{customerId}; {lines}; missing_fields={FormatList(suggestion.MissingFields)}; warnings={FormatList(suggestion.Warnings)}";
    }

    private static string FormatList(IReadOnlyList<string> values)
    {
        return values.Count == 0 ? "none" : string.Join("|", values);
    }

    private static readonly Regex InboundIntentRegex = new(
        "\u5165\u5e93|\u5165\u4ed3|\u8fdb\u8d27|\u6536\u8d27|\u6536\u5165|\u91c7\u8d2d|\u56de\u6536",
        RegexOptions.Compiled);

    private static readonly Regex OutboundIntentRegex = new(
        "\u51fa\u5e93|\u51fa\u4ed3|\u51fa\u8d27|\u53d1\u8d27|\u552e\u51fa|\u5356\u51fa|\u9500\u552e",
        RegexOptions.Compiled);
}
