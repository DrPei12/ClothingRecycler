using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Globalization;

namespace ClothingRecycler.Desktop.Services;

public sealed class LocalAiDraftAgentService
{
    private readonly IAiBusinessContextSource _businessContextSource;
    private readonly IReadOnlyDictionary<AiLocalProviderKind, ILocalAiProvider> _providers;
    private readonly AppLogger _logger;

    public LocalAiDraftAgentService(
        IAiBusinessContextSource businessContextSource,
        IEnumerable<ILocalAiProvider> providers,
        AppLogger logger)
    {
        _businessContextSource = businessContextSource;
        _providers = providers.ToDictionary(provider => provider.Kind);
        _logger = logger;
    }

    public async Task<IReadOnlyList<AiProviderProbeResult>> ProbeProvidersAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<AiProviderProbeResult>();
        foreach (var provider in _providers.Values.OrderBy(provider => provider.Kind))
        {
            results.Add(await provider.ProbeAsync(cancellationToken));
        }

        return results;
    }

    public Task WarmProviderAsync(AiLocalProviderKind providerKind, CancellationToken cancellationToken = default)
    {
        return GetProvider(providerKind).WarmAsync(cancellationToken);
    }

    public async Task<string> TranscribeAudioAsync(
        string audioPath,
        AiLocalProviderKind providerKind,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
        {
            throw new FileNotFoundException("Voice input file does not exist.", audioPath);
        }

        var provider = GetProvider(providerKind);
        var rawText = await provider.CompleteAsync(BuildTranscriptionRequest(audioPath), cancellationToken);
        var transcript = CleanTranscript(rawText);
        if (string.IsNullOrWhiteSpace(transcript))
        {
            throw new InvalidOperationException("The local AI model did not return a usable voice transcript.");
        }

        await _logger.LogInfoAsync($"AI voice transcript via {providerKind}: {TruncateForLog(transcript)}");
        return transcript;
    }

    public async Task<AiOrderDraftSuggestion> SuggestInboundDraftAsync(
        string userInput,
        AiLocalProviderKind providerKind,
        CancellationToken cancellationToken = default)
    {
        var categories = await _businessContextSource.GetActiveCategoriesAsync();
        var customers = await _businessContextSource.GetCustomersAsync();
        return await SuggestDraftAsync(AiDraftOperation.Inbound, userInput, categories, customers, providerKind, cancellationToken);
    }

    public async Task<AiOrderDraftSuggestion> SuggestOutboundDraftAsync(
        string userInput,
        AiLocalProviderKind providerKind,
        CancellationToken cancellationToken = default)
    {
        var categories = await _businessContextSource.GetActiveCategoriesAsync();
        var customers = await _businessContextSource.GetCustomersAsync();
        return await SuggestDraftAsync(AiDraftOperation.Outbound, userInput, categories, customers, providerKind, cancellationToken);
    }

    public async Task<AiOrderDraftSuggestion> SuggestDraftAsync(
        AiDraftOperation operation,
        string userInput,
        IReadOnlyList<CategoryModel> categories,
        IReadOnlyList<CustomerModel> customers,
        AiLocalProviderKind providerKind,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userInput))
        {
            throw new InvalidOperationException("AI draft input cannot be empty.");
        }

        var provider = GetProvider(providerKind);
        var initialRequest = BuildInitialRequest(operation, userInput, categories, customers);
        var initialRaw = await provider.CompleteAsync(initialRequest, cancellationToken);
        await _logger.LogInfoAsync($"AI draft agent received initial response from {providerKind}.");

        if (TryParseToolCall(initialRaw, out var toolCall))
        {
            var toolPayload = ResolveEntities(toolCall!, categories, customers);
            var followUpRequest = BuildFollowUpRequest(operation, userInput, initialRaw, toolPayload);
            var finalRaw = await provider.CompleteAsync(followUpRequest, cancellationToken);
            await _logger.LogInfoAsync($"AI draft agent completed tool-assisted response via {providerKind}.");
            return await BuildReliableSuggestionAsync(
                operation,
                userInput,
                providerKind,
                finalRaw,
                categories,
                customers,
                "The AI follow-up response could not be used directly, so the app built a best-effort local draft.");
        }

        return await BuildReliableSuggestionAsync(
            operation,
            userInput,
            providerKind,
            initialRaw,
            categories,
            customers,
            "The AI response could not be used directly, so the app built a best-effort local draft.");
    }

    private async Task<AiOrderDraftSuggestion> BuildReliableSuggestionAsync(
        AiDraftOperation operation,
        string userInput,
        AiLocalProviderKind providerKind,
        string rawText,
        IReadOnlyList<CategoryModel> categories,
        IReadOnlyList<CustomerModel> customers,
        string fallbackWarning)
    {
        try
        {
            var suggestion = ParseFinalSuggestion(operation, providerKind, rawText, categories, customers);
            if (IsSuggestionObviouslyBroken(suggestion))
            {
                await _logger.LogWarningAsync(
                    $"AI draft agent produced a low-quality draft for {providerKind}. Falling back to deterministic parsing. Raw response: {TruncateForLog(rawText)}");

                return BuildFallbackSuggestion(operation, providerKind, userInput, categories, customers, rawText, fallbackWarning);
            }

            return suggestion;
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync(
                $"AI draft agent failed to parse provider response from {providerKind}. Raw response: {TruncateForLog(rawText)}",
                ex);

            return BuildFallbackSuggestion(operation, providerKind, userInput, categories, customers, rawText, fallbackWarning);
        }
    }

    private static AiCompletionRequest BuildInitialRequest(
        AiDraftOperation operation,
        string userInput,
        IReadOnlyList<CategoryModel> categories,
        IReadOnlyList<CustomerModel> customers)
    {
        var systemPrompt =
            """
            You are a local order drafting agent for a clothing recycler business app.
            Convert the user's request into a SAFE draft only. Never confirm or execute an order.

            Return ONLY one JSON object.

            If you need help resolving category or customer names, return:
            {
              "message_type": "tool_call",
              "tool_name": "resolve_entities",
              "arguments": {
                "customer_name": "optional string",
                "category_names": ["optional category names"]
              }
            }

            Otherwise return:
            {
              "message_type": "final",
              "payload": {
                "intent": "fill_inbound_order or fill_outbound_order",
                "confidence": 0.0,
                "customer_name": "string",
                "lines": [
                  {
                    "category_name": "string",
                    "quantity": 0,
                    "unit_type": "Kilogram or Jin or Piece",
                    "unit_price": 0
                  }
                ],
                "missing_fields": [],
                "warnings": [],
                "assistant_message": "short human-readable note"
              }
            }

            Rules:
            - Do not invent IDs.
            - Use names only.
            - Keep quantities and prices numeric.
            - If something is unclear, keep it null or omit it and add a warning.
            - Allowed unit_type values are Kilogram, Jin, Piece.
            """;

        var userPromptBuilder = new StringBuilder();
        userPromptBuilder.AppendLine($"Operation: {operation}");
        userPromptBuilder.AppendLine("User request:");
        userPromptBuilder.AppendLine(userInput.Trim());
        userPromptBuilder.AppendLine();
        userPromptBuilder.AppendLine("Known categories:");
        foreach (var category in categories)
        {
            userPromptBuilder.AppendLine(
                $"- name={category.Name}; unit={category.UnitType}; buy_price={category.BuyPrice:0.##}; sell_price={category.SellPrice:0.##}; stock={category.DisplayStock:0.##} {category.UnitLabel}");
        }

        userPromptBuilder.AppendLine();
        userPromptBuilder.AppendLine("Known customers:");
        foreach (var customer in customers)
        {
            userPromptBuilder.AppendLine($"- {customer.Name}");
        }

        return new AiCompletionRequest
        {
            MaxNewTokens = 220,
            Messages = new[]
            {
                new AiChatMessage { Role = "system", Content = systemPrompt },
                new AiChatMessage { Role = "user", Content = userPromptBuilder.ToString() }
            }
        };
    }

    private static AiCompletionRequest BuildTranscriptionRequest(string audioPath)
    {
        var systemPrompt =
            """
            You are a speech transcription assistant for a Chinese clothing recycler order-entry app.
            Listen to the user's audio and return only the spoken sentence as plain Chinese text.
            Do not explain. Do not create JSON. Preserve business words such as 入库, 出库, 公斤, 斤, 件, 单价, 每公斤.
            Normalize currency symbols to 元 when useful, but do not invent missing quantities, prices, customers, or categories.
            """;

        return new AiCompletionRequest
        {
            AudioPath = audioPath,
            MaxNewTokens = 96,
            Messages = new[]
            {
                new AiChatMessage { Role = "system", Content = systemPrompt },
                new AiChatMessage { Role = "user", Content = "请转写这段语音，只返回用户说的话。" }
            }
        };
    }

    private static AiCompletionRequest BuildFollowUpRequest(
        AiDraftOperation operation,
        string userInput,
        string modelToolCall,
        string toolPayload)
    {
        var systemPrompt =
            """
            You are a local order drafting agent for a clothing recycler business app.
            The local tool output is authoritative. Use it to resolve names and return the final draft only.

            Return ONLY one JSON object in this shape:
            {
              "intent": "fill_inbound_order or fill_outbound_order",
              "confidence": 0.0,
              "customer_name": "string",
              "lines": [
                {
                  "category_name": "string",
                  "quantity": 0,
                  "unit_type": "Kilogram or Jin or Piece",
                  "unit_price": 0
                }
              ],
              "missing_fields": [],
              "warnings": [],
              "assistant_message": "short human-readable note"
            }

            Never return another tool call in this step.
            """;

        var userPrompt =
            $"""
            Operation: {operation}
            Original user request:
            {userInput.Trim()}

            Previous model response:
            {modelToolCall}

            Local tool result:
            {toolPayload}
            """;

        return new AiCompletionRequest
        {
            MaxNewTokens = 180,
            Messages = new[]
            {
                new AiChatMessage { Role = "system", Content = systemPrompt },
                new AiChatMessage { Role = "user", Content = userPrompt }
            }
        };
    }

    private static bool TryParseToolCall(string rawText, out ToolCallRequest? toolCall)
    {
        toolCall = null;
        if (!TryParseJsonDocument(rawText, out var document))
        {
            return false;
        }

        using var jsonDocument = document!;
        var root = jsonDocument.RootElement;
        if (!root.TryGetProperty("message_type", out var messageTypeElement)
            || !string.Equals(messageTypeElement.GetString(), "tool_call", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!root.TryGetProperty("tool_name", out var toolNameElement)
            || !string.Equals(toolNameElement.GetString(), "resolve_entities", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var customerName = string.Empty;
        var categoryNames = new List<string>();

        if (root.TryGetProperty("arguments", out var argumentsElement))
        {
            if (argumentsElement.TryGetProperty("customer_name", out var customerElement))
            {
                customerName = customerElement.GetString() ?? string.Empty;
            }

            if (argumentsElement.TryGetProperty("category_names", out var categoryNamesElement)
                && categoryNamesElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in categoryNamesElement.EnumerateArray())
                {
                    var value = item.GetString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        categoryNames.Add(value.Trim());
                    }
                }
            }
        }

        toolCall = new ToolCallRequest(customerName, categoryNames);
        return true;
    }

    private static AiOrderDraftSuggestion ParseFinalSuggestion(
        AiDraftOperation operation,
        AiLocalProviderKind providerKind,
        string rawText,
        IReadOnlyList<CategoryModel> categories,
        IReadOnlyList<CustomerModel> customers)
    {
        if (!TryParseJsonDocument(rawText, out var document))
        {
            throw new InvalidOperationException("AI provider did not return valid JSON.");
        }

        using var jsonDocument = document!;
        var root = jsonDocument.RootElement;
        if (root.TryGetProperty("message_type", out var messageTypeElement)
            && string.Equals(messageTypeElement.GetString(), "final", StringComparison.OrdinalIgnoreCase)
            && root.TryGetProperty("payload", out var payloadElement))
        {
            root = payloadElement;
        }

        var customerName = GetString(root, "customer_name");
        var warnings = ReadStringArray(root, "warnings");
        var missingFields = ReadStringArray(root, "missing_fields");
        var linesElement = root.TryGetProperty("lines", out var candidateLines) ? candidateLines
            : root.TryGetProperty("items", out var candidateItems) ? candidateItems
            : default;

        var lines = new List<AiOrderDraftLineSuggestion>();
        if (linesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in linesElement.EnumerateArray())
            {
                var categoryName = GetString(item, "category_name");
                var lineWarnings = ReadStringArray(item, "warnings").ToList();
                var resolvedCategory = ResolveCategory(categoryName, categories, lineWarnings);

                lines.Add(new AiOrderDraftLineSuggestion
                {
                    CategoryName = categoryName,
                    ExistingCategoryId = resolvedCategory?.Id,
                    Quantity = GetDouble(item, "quantity"),
                    InputUnitType = ParseWeightUnit(GetString(item, "unit_type")),
                    UnitPrice = GetDouble(item, "unit_price"),
                    Warnings = lineWarnings
                });
            }
        }

        var resolvedCustomer = ResolveCustomer(customerName, customers, warnings);
        return new AiOrderDraftSuggestion
        {
            Operation = operation,
            ProviderKind = providerKind,
            Confidence = GetDouble(root, "confidence") ?? 0,
            CustomerName = customerName,
            ExistingCustomerId = resolvedCustomer?.Id,
            Lines = lines,
            MissingFields = missingFields,
            Warnings = warnings,
            AssistantMessage = GetString(root, "assistant_message"),
            RawModelText = rawText
        };
    }

    private static string ResolveEntities(
        ToolCallRequest toolCall,
        IReadOnlyList<CategoryModel> categories,
        IReadOnlyList<CustomerModel> customers)
    {
        var matchedCategories = new List<object>();
        foreach (var categoryName in toolCall.CategoryNames.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var candidates = FindCategoryCandidates(categoryName, categories)
                .Select(category => new
                {
                    id = category.Id,
                    name = category.Name,
                    unit_type = category.UnitType.ToString(),
                    buy_price = category.BuyPrice,
                    sell_price = category.SellPrice,
                    stock = category.DisplayStock,
                    stock_unit = category.UnitLabel
                })
                .ToArray();

            matchedCategories.Add(new
            {
                query = categoryName,
                candidates
            });
        }

        var matchedCustomers = FindCustomerCandidates(toolCall.CustomerName, customers)
            .Select(customer => new
            {
                id = customer.Id,
                name = customer.Name
            })
            .ToArray();

        return JsonSerializer.Serialize(new
        {
            requested_customer = toolCall.CustomerName,
            matched_customers = matchedCustomers,
            matched_categories = matchedCategories
        }, JsonOptions);
    }

    private static CategoryModel? ResolveCategory(
        string categoryName,
        IReadOnlyList<CategoryModel> categories,
        List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(categoryName))
        {
            warnings.Add("Missing category name.");
            return null;
        }

        var matches = FindCategoryCandidates(categoryName, categories).ToList();
        if (matches.Count == 0)
        {
            warnings.Add($"Unknown category '{categoryName}'.");
            return null;
        }

        if (matches.Count > 1)
        {
            warnings.Add($"Ambiguous category '{categoryName}'.");
        }

        return matches[0];
    }

    private static CustomerModel? ResolveCustomer(
        string customerName,
        IReadOnlyList<CustomerModel> customers,
        List<string> warnings)
    {
        if (string.IsNullOrWhiteSpace(customerName))
        {
            return null;
        }

        var matches = FindCustomerCandidates(customerName, customers).ToList();
        if (matches.Count == 0)
        {
            warnings.Add($"Unknown customer '{customerName}'.");
            return null;
        }

        if (matches.Count > 1)
        {
            warnings.Add($"Ambiguous customer '{customerName}'.");
        }

        return matches[0];
    }

    private static IEnumerable<CategoryModel> FindCategoryCandidates(string query, IReadOnlyList<CategoryModel> categories)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<CategoryModel>();
        }

        var normalizedQuery = NormalizeLookupToken(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Array.Empty<CategoryModel>();
        }

        var exactMatches = categories.Where(category => NormalizeLookupToken(category.Name) == normalizedQuery).ToList();
        if (exactMatches.Count > 0)
        {
            return exactMatches;
        }

        return categories.Where(category => NormalizeLookupToken(category.Name).Contains(normalizedQuery, StringComparison.Ordinal)).ToList();
    }

    private static IEnumerable<CustomerModel> FindCustomerCandidates(string query, IReadOnlyList<CustomerModel> customers)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return Array.Empty<CustomerModel>();
        }

        var normalizedQuery = NormalizeLookupToken(query);
        if (string.IsNullOrWhiteSpace(normalizedQuery))
        {
            return Array.Empty<CustomerModel>();
        }

        var exactMatches = customers.Where(customer => NormalizeLookupToken(customer.Name) == normalizedQuery).ToList();
        if (exactMatches.Count > 0)
        {
            return exactMatches;
        }

        return customers.Where(customer => NormalizeLookupToken(customer.Name).Contains(normalizedQuery, StringComparison.Ordinal)).ToList();
    }

    private static string NormalizeLookupToken(string value)
    {
        return new string(value
            .Trim()
            .ToLowerInvariant()
            .Where(char.IsLetterOrDigit)
            .ToArray());
    }

    private static WeightUnit? ParseWeightUnit(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "kilogram" or "kg" or "公斤" => WeightUnit.Kilogram,
            "jin" or "斤" => WeightUnit.Jin,
            "piece" or "pieces" or "件" => WeightUnit.Piece,
            _ => Enum.TryParse<WeightUnit>(value, ignoreCase: true, out var parsed)
                ? parsed
                : null
        };
    }

    private ILocalAiProvider GetProvider(AiLocalProviderKind providerKind)
    {
        if (_providers.TryGetValue(providerKind, out var provider))
        {
            return provider;
        }

        throw new InvalidOperationException($"No local AI provider is registered for {providerKind}.");
    }

    private static bool TryParseJsonDocument(string rawText, out JsonDocument? document)
    {
        document = null;
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return false;
        }

        foreach (var candidate in EnumerateJsonCandidates(rawText))
        {
            if (!TryParseJsonCandidate(candidate, out document))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private static IEnumerable<string> EnumerateJsonCandidates(string rawText)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var trimmed = rawText.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            yield break;
        }

        if (seen.Add(trimmed))
        {
            yield return trimmed;
        }

        var unfenced = StripMarkdownCodeFence(trimmed);
        if (!string.Equals(unfenced, trimmed, StringComparison.Ordinal) && seen.Add(unfenced))
        {
            yield return unfenced;
        }

        var extractedFromTrimmed = ExtractJsonPayload(trimmed);
        if (!string.IsNullOrWhiteSpace(extractedFromTrimmed) && seen.Add(extractedFromTrimmed))
        {
            yield return extractedFromTrimmed;
        }

        var extractedFromUnfenced = ExtractJsonPayload(unfenced);
        if (!string.IsNullOrWhiteSpace(extractedFromUnfenced) && seen.Add(extractedFromUnfenced))
        {
            yield return extractedFromUnfenced;
        }
    }

    private static bool TryParseJsonCandidate(string candidate, out JsonDocument? document)
    {
        document = null;

        try
        {
            var parsed = JsonDocument.Parse(candidate);
            if (parsed.RootElement.ValueKind == JsonValueKind.String)
            {
                var inner = parsed.RootElement.GetString();
                parsed.Dispose();
                return !string.IsNullOrWhiteSpace(inner) && TryParseJsonCandidate(inner, out document);
            }

            document = parsed;
            return true;
        }
        catch
        {
        }

        var unescaped = UnescapeJsonLikeText(candidate);
        if (string.Equals(unescaped, candidate, StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            document = JsonDocument.Parse(unescaped);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string StripMarkdownCodeFence(string rawText)
    {
        var trimmed = rawText.Trim();
        if (!trimmed.StartsWith("```", StringComparison.Ordinal))
        {
            return trimmed;
        }

        var firstLineBreak = trimmed.IndexOf('\n');
        if (firstLineBreak < 0)
        {
            return trimmed;
        }

        var lastFence = trimmed.LastIndexOf("```", StringComparison.Ordinal);
        if (lastFence <= firstLineBreak)
        {
            return trimmed;
        }

        return trimmed[(firstLineBreak + 1)..lastFence].Trim();
    }

    private static string? ExtractJsonPayload(string rawText)
    {
        var start = rawText.IndexOf('{');
        var end = rawText.LastIndexOf('}');
        return start >= 0 && end > start
            ? rawText[start..(end + 1)]
            : null;
    }

    private static string UnescapeJsonLikeText(string value)
    {
        return value
            .Replace("\\\"", "\"", StringComparison.Ordinal)
            .Replace("\\n", "\n", StringComparison.Ordinal)
            .Replace("\\r", "\r", StringComparison.Ordinal)
            .Replace("\\t", "\t", StringComparison.Ordinal)
            .Replace("\\\\", "\\", StringComparison.Ordinal);
    }

    private static string GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property)
            ? property.GetString() ?? string.Empty
            : string.Empty;
    }

    private static double? GetDouble(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
        {
            return null;
        }

        return property.ValueKind switch
        {
            JsonValueKind.Number => property.GetDouble(),
            JsonValueKind.String when double.TryParse(property.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) => parsed,
            _ => null
        };
    }

    private static List<string> ReadStringArray(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property) || property.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return property.EnumerateArray()
            .Select(item => item.GetString())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value!.Trim())
            .ToList();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private static AiOrderDraftSuggestion BuildFallbackSuggestion(
        AiDraftOperation operation,
        AiLocalProviderKind providerKind,
        string userInput,
        IReadOnlyList<CategoryModel> categories,
        IReadOnlyList<CustomerModel> customers,
        string rawModelText,
        string fallbackWarning)
    {
        var warnings = new List<string> { fallbackWarning };
        var missingFields = new List<string>();
        var customer = FindBestCustomerMatch(userInput, customers);
        var category = FindBestCategoryMatch(userInput, categories);
        var customerName = customer?.Name ?? ExtractPotentialCustomerName(userInput);
        var quantity = ExtractQuantity(userInput, category?.UnitType, out var detectedUnit);
        var unitType = detectedUnit ?? category?.UnitType;
        var unitPrice = ExtractUnitPrice(userInput);

        if (string.IsNullOrWhiteSpace(customerName))
        {
            missingFields.Add("customer_name");
            warnings.Add("Could not confidently match a customer from the input.");
        }

        if (category is null)
        {
            missingFields.Add("category_name");
            warnings.Add("Could not confidently match a category from the input.");
        }

        if (quantity is null)
        {
            missingFields.Add("quantity");
            warnings.Add("Could not confidently parse the quantity from the input.");
        }

        if (unitPrice is null)
        {
            missingFields.Add("unit_price");
            warnings.Add("Could not confidently parse the unit price from the input.");
        }

        var lines = new List<AiOrderDraftLineSuggestion>();
        if (category is not null)
        {
            lines.Add(new AiOrderDraftLineSuggestion
            {
                CategoryName = category.Name,
                ExistingCategoryId = category.Id,
                Quantity = quantity,
                InputUnitType = unitType,
                UnitPrice = unitPrice,
                Warnings = warnings
            });
        }

        var resolvedCount = 0;
        if (customer is not null) resolvedCount++;
        if (category is not null) resolvedCount++;
        if (quantity is not null) resolvedCount++;
        if (unitPrice is not null) resolvedCount++;

        return new AiOrderDraftSuggestion
        {
            Operation = operation,
            ProviderKind = providerKind,
            Confidence = resolvedCount / 4.0,
            CustomerName = customerName ?? string.Empty,
            ExistingCustomerId = customer?.Id,
            Lines = lines,
            MissingFields = missingFields,
            Warnings = warnings,
            AssistantMessage = "Built from local fallback rules. Please review before applying.",
            RawModelText = rawModelText
        };
    }

    private static bool IsSuggestionObviouslyBroken(AiOrderDraftSuggestion suggestion)
    {
        if (suggestion.Lines.Count == 0)
        {
            return true;
        }

        if (suggestion.Lines.All(line => string.IsNullOrWhiteSpace(line.CategoryName)))
        {
            return true;
        }

        return suggestion.Lines.Any(line => BrokenCategoryNames.Contains(line.CategoryName.Trim(), StringComparer.OrdinalIgnoreCase));
    }

    private static CustomerModel? FindBestCustomerMatch(string userInput, IReadOnlyList<CustomerModel> customers)
    {
        return customers
            .Where(customer => !string.IsNullOrWhiteSpace(customer.Name) && userInput.Contains(customer.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(customer => customer.Name.Length)
            .FirstOrDefault();
    }

    private static string? ExtractPotentialCustomerName(string userInput)
    {
        var match = LeadingCustomerRegex.Match(userInput);
        if (!match.Success)
        {
            return null;
        }

        var candidate = match.Groups["customer"].Value.Trim();
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        candidate = candidate.TrimStart('给');
        candidate = candidate.Trim();
        return string.IsNullOrWhiteSpace(candidate) ? null : candidate;
    }

    private static CategoryModel? FindBestCategoryMatch(string userInput, IReadOnlyList<CategoryModel> categories)
    {
        return categories
            .Where(category => !string.IsNullOrWhiteSpace(category.Name) && userInput.Contains(category.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(category => category.Name.Length)
            .FirstOrDefault();
    }

    private static double? ExtractQuantity(string userInput, WeightUnit? defaultUnit, out WeightUnit? unitType)
    {
        unitType = null;
        var quantityMatch = FallbackQuantityRegex.Match(userInput);
        if (!quantityMatch.Success)
        {
            unitType = defaultUnit;
            return null;
        }

        if (!double.TryParse(quantityMatch.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var quantity))
        {
            unitType = defaultUnit;
            return null;
        }

        unitType = ParseWeightUnit(quantityMatch.Groups["unit"].Value) ?? defaultUnit;
        return quantity;
    }

    private static double? ExtractUnitPrice(string userInput)
    {
        var compactMatch = FallbackCompactPriceRegex.Match(userInput);
        if (compactMatch.Success
            && double.TryParse(compactMatch.Groups["yuan"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var yuan))
        {
            var jiaoText = compactMatch.Groups["jiao"].Value;
            if (double.TryParse($"0.{jiaoText}", NumberStyles.Float, CultureInfo.InvariantCulture, out var fraction))
            {
                return yuan + fraction;
            }

            return yuan;
        }

        foreach (var pattern in FallbackExplicitPriceRegexes)
        {
            var explicitMatch = pattern.Match(userInput);
            if (!explicitMatch.Success)
            {
                continue;
            }

            if (double.TryParse(explicitMatch.Groups["price"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var price))
            {
                return price;
            }
        }

        return null;
    }

    private static string TruncateForLog(string rawText)
    {
        const int maxLength = 1200;
        var trimmed = rawText.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength] + " ...[truncated]";
    }

    private static string CleanTranscript(string rawText)
    {
        var text = rawText.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        if (TryParseJsonDocument(text, out var document))
        {
            using var jsonDocument = document!;
            text = ExtractTranscriptFromJson(jsonDocument.RootElement) ?? text;
        }

        text = text
            .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.Ordinal)
            .Trim();

        foreach (var prefix in new[] { "转写：", "转写:", "识别：", "识别:", "用户说：", "用户说:" })
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                text = text[prefix.Length..].Trim();
            }
        }

        return text.Trim(' ', '\t', '\r', '\n', '"', '\'', '\u201c', '\u201d');
    }

    private static string? ExtractTranscriptFromJson(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return element.GetString();
        }

        if (element.ValueKind == JsonValueKind.Array && element.GetArrayLength() > 0)
        {
            return ExtractTranscriptFromJson(element[0]);
        }

        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        foreach (var propertyName in new[] { "transcript", "text", "generated_text", "output_text" })
        {
            if (element.TryGetProperty(propertyName, out var propertyValue))
            {
                var transcript = ExtractTranscriptFromJson(propertyValue);
                if (!string.IsNullOrWhiteSpace(transcript))
                {
                    return transcript;
                }
            }
        }

        return null;
    }

    private static readonly Regex QuantityRegex = new(
        @"(?<value>\d+(?:\.\d+)?)\s*(?<unit>公斤|公斥|kg|KG|斤|件|个|piece|pieces|jin|kilogram)?",
        RegexOptions.Compiled);

    private static readonly Regex CompactPriceRegex = new(
        @"(?<yuan>\d+(?:\.\d+)?)\s*块\s*(?<jiao>\d+)",
        RegexOptions.Compiled);

    private static readonly Regex ExplicitPriceRegex = new(
        @"(?:单价|价格|每(?:公斤|斤|件|个)?)?\s*(?<price>\d+(?:\.\d+)?)\s*(?:元|块)",
        RegexOptions.Compiled);

    private static readonly Regex FallbackQuantityRegex = new(
        "(?<value>\\d+(?:\\.\\d+)?)\\s*(?<unit>\\u516c\\u65a4|kg|KG|\\u65a4|\\u4ef6|\\u4e2a|piece|pieces|jin|kilogram)",
        RegexOptions.Compiled);

    private static readonly Regex FallbackCompactPriceRegex = new(
        "(?<yuan>\\d+(?:\\.\\d+)?)\\s*\\u5757\\s*(?<jiao>\\d+)",
        RegexOptions.Compiled);

    private static readonly Regex[] FallbackExplicitPriceRegexes =
    [
        new Regex(
            "(?:\\u5355\\u4ef7|\\u4ef7\\u683c)\\s*[:：]?\\s*(?:￥|¥)?\\s*(?<price>\\d+(?:\\.\\d+)?)\\s*(?:￥|¥|\\u5143|\\u5757)?",
            RegexOptions.Compiled),
        new Regex(
            "(?:￥|¥)\\s*(?<price>\\d+(?:\\.\\d+)?)\\s*(?:(?:/|\\u6bcf)\\s*(?:\\u516c\\u65a4|kg|KG|\\u65a4|\\u4ef6|\\u4e2a|piece|pieces))?",
            RegexOptions.Compiled),
        new Regex(
            "(?<price>\\d+(?:\\.\\d+)?)\\s*(?:￥|¥|\\u5143|\\u5757)\\s*(?:(?:/|\\u6bcf)\\s*(?:\\u516c\\u65a4|kg|KG|\\u65a4|\\u4ef6|\\u4e2a|piece|pieces))?",
            RegexOptions.Compiled),
        new Regex(
            "(?:\\u6bcf\\s*(?:\\u516c\\u65a4|kg|KG|\\u65a4|\\u4ef6|\\u4e2a|piece|pieces))\\s*(?:￥|¥)?\\s*(?<price>\\d+(?:\\.\\d+)?)",
            RegexOptions.Compiled)
    ];

    private static readonly Regex LeadingCustomerRegex = new(
        "^\\s*(?:\\u7ed9)?(?<customer>[^0-9,\\uff0c\\u3002:：]{1,20}?)(?:\\u5165\\u5e93|\\u51fa\\u5e93)",
        RegexOptions.Compiled);

    private static readonly string[] BrokenCategoryNames =
    [
        "name",
        "unit",
        "unit_type",
        "buy_price",
        "sell_price",
        "stock",
        "stock_unit"
    ];

    private sealed record ToolCallRequest(string CustomerName, IReadOnlyList<string> CategoryNames);
}
