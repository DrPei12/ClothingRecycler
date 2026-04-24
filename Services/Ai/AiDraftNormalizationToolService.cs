using System.Globalization;
using System.Text.RegularExpressions;

using ClothingRecycler.Desktop.Models;

namespace ClothingRecycler.Desktop.Services;

public sealed class AiDraftNormalizationToolService
{
    private readonly IAiBusinessContextSource _businessContextSource;

    public AiDraftNormalizationToolService(IAiBusinessContextSource businessContextSource)
    {
        _businessContextSource = businessContextSource;
    }

    public async Task<AiOrderDraftSuggestion> NormalizeAsync(
        string userInput,
        AiOrderDraftSuggestion suggestion,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(suggestion);

        var categories = await _businessContextSource.GetActiveCategoriesAsync();
        var customers = await _businessContextSource.GetCustomersAsync();

        var missingFields = new HashSet<string>(suggestion.MissingFields, StringComparer.OrdinalIgnoreCase);
        var warnings = suggestion.Warnings.ToList();

        var resolvedCustomer = ResolveCustomer(suggestion, userInput, customers);
        var normalizedCustomerName = resolvedCustomer?.Name
            ?? ExtractCustomerName(userInput, categories)
            ?? suggestion.CustomerName?.Trim()
            ?? string.Empty;

        if (!string.IsNullOrWhiteSpace(normalizedCustomerName))
        {
            missingFields.Remove("customer_name");
        }

        var normalizedLines = NormalizeLines(userInput, suggestion, categories, missingFields);
        if (normalizedLines.Count > 0)
        {
            if (normalizedLines.All(line => line.ExistingCategoryId.HasValue))
            {
                missingFields.Remove("category_name");
            }

            if (normalizedLines.All(line => line.Quantity.HasValue && line.Quantity.Value > 0))
            {
                missingFields.Remove("quantity");
            }

            if (normalizedLines.All(line => line.UnitPrice.HasValue && line.UnitPrice.Value > 0))
            {
                missingFields.Remove("unit_price");
            }
        }

        warnings = PruneResolvedWarnings(warnings, missingFields);

        return new AiOrderDraftSuggestion
        {
            Operation = suggestion.Operation,
            ProviderKind = suggestion.ProviderKind,
            Confidence = suggestion.Confidence,
            CustomerName = normalizedCustomerName,
            ExistingCustomerId = resolvedCustomer?.Id ?? suggestion.ExistingCustomerId,
            Lines = normalizedLines,
            MissingFields = missingFields.ToArray(),
            Warnings = warnings,
            AssistantMessage = suggestion.AssistantMessage,
            RawModelText = suggestion.RawModelText
        };
    }

    private static List<AiOrderDraftLineSuggestion> NormalizeLines(
        string userInput,
        AiOrderDraftSuggestion suggestion,
        IReadOnlyList<CategoryModel> categories,
        HashSet<string> missingFields)
    {
        var normalizedLines = new List<AiOrderDraftLineSuggestion>();
        var fallbackCategory = FindBestCategoryMatch(userInput, categories);
        var verifiedQuantity = ExtractQuantity(userInput, fallbackCategory?.UnitType, out var verifiedUnitType);
        var fallbackUnitPrice = ExtractUnitPrice(userInput);

        var sourceLines = suggestion.Lines.Count > 0
            ? suggestion.Lines
            : new[]
            {
                new AiOrderDraftLineSuggestion
                {
                    CategoryName = fallbackCategory?.Name ?? string.Empty
                }
            };

        foreach (var line in sourceLines)
        {
            var resolvedCategory = ResolveCategory(line, userInput, categories) ?? fallbackCategory;
            var quantity = verifiedQuantity;
            var unitType = verifiedUnitType ?? line.InputUnitType ?? resolvedCategory?.UnitType;
            var unitPrice = line.UnitPrice.HasValue && line.UnitPrice.Value > 0 ? line.UnitPrice : fallbackUnitPrice;

            normalizedLines.Add(new AiOrderDraftLineSuggestion
            {
                CategoryName = !string.IsNullOrWhiteSpace(line.CategoryName) ? line.CategoryName : resolvedCategory?.Name ?? string.Empty,
                ExistingCategoryId = line.ExistingCategoryId ?? resolvedCategory?.Id,
                Quantity = quantity,
                InputUnitType = unitType,
                UnitPrice = unitPrice,
                Warnings = line.Warnings
            });
        }

        if (normalizedLines.All(line => !line.ExistingCategoryId.HasValue))
        {
            missingFields.Add("category_name");
        }

        if (normalizedLines.All(line => !line.Quantity.HasValue || line.Quantity.Value <= 0))
        {
            missingFields.Add("quantity");
        }

        if (normalizedLines.All(line => !line.UnitPrice.HasValue || line.UnitPrice.Value <= 0))
        {
            missingFields.Add("unit_price");
        }

        return normalizedLines;
    }

    private static CustomerModel? ResolveCustomer(
        AiOrderDraftSuggestion suggestion,
        string userInput,
        IReadOnlyList<CustomerModel> customers)
    {
        if (suggestion.ExistingCustomerId.HasValue)
        {
            return customers.FirstOrDefault(customer => customer.Id == suggestion.ExistingCustomerId.Value);
        }

        if (!string.IsNullOrWhiteSpace(suggestion.CustomerName))
        {
            var exact = customers.FirstOrDefault(customer =>
                string.Equals(customer.Name, suggestion.CustomerName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }
        }

        return FindBestCustomerMatch(userInput, customers);
    }

    private static string? ExtractCustomerName(string userInput, IReadOnlyList<CategoryModel> categories)
    {
        var firstSegment = userInput
            .Split(new[] { '\uff0c', ',', '\u3002', ';', '\uff1b' }, StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()
            ?.Trim();

        if (IsPlausibleCustomerName(firstSegment))
        {
            return CleanCustomerName(firstSegment!);
        }

        var operationMatch = LeadingCustomerRegex.Match(userInput);
        if (!operationMatch.Success)
        {
            return null;
        }

        var candidate = operationMatch.Groups["candidate"].Value;
        foreach (var category in categories.OrderByDescending(category => category.Name.Length))
        {
            if (!string.IsNullOrWhiteSpace(category.Name))
            {
                candidate = candidate.Replace(category.Name, string.Empty, StringComparison.OrdinalIgnoreCase);
            }
        }

        candidate = CleanCustomerName(candidate);
        return IsPlausibleCustomerName(candidate) ? candidate : null;
    }

    private static CategoryModel? ResolveCategory(
        AiOrderDraftLineSuggestion line,
        string userInput,
        IReadOnlyList<CategoryModel> categories)
    {
        if (line.ExistingCategoryId.HasValue)
        {
            return categories.FirstOrDefault(category => category.Id == line.ExistingCategoryId.Value);
        }

        if (!string.IsNullOrWhiteSpace(line.CategoryName))
        {
            var exact = categories.FirstOrDefault(category =>
                string.Equals(category.Name, line.CategoryName.Trim(), StringComparison.OrdinalIgnoreCase));
            if (exact is not null)
            {
                return exact;
            }
        }

        return FindBestCategoryMatch(userInput, categories);
    }

    private static List<string> PruneResolvedWarnings(
        List<string> warnings,
        HashSet<string> missingFields)
    {
        var filtered = warnings.Where(warning =>
        {
            if (!missingFields.Contains("customer_name")
                && warning.Contains("customer", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!missingFields.Contains("category_name")
                && warning.Contains("category", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!missingFields.Contains("quantity")
                && warning.Contains("quantity", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!missingFields.Contains("unit_price")
                && warning.Contains("unit price", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return true;
        }).ToList();

        return filtered;
    }

    private static CustomerModel? FindBestCustomerMatch(string userInput, IReadOnlyList<CustomerModel> customers)
    {
        return customers
            .Where(customer => !string.IsNullOrWhiteSpace(customer.Name) && userInput.Contains(customer.Name, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(customer => customer.Name.Length)
            .FirstOrDefault();
    }

    private static string CleanCustomerName(string value)
    {
        return value
            .Trim()
            .TrimStart('\u7ed9')
            .Trim(' ', '\t', '\r', '\n', ',', '\uff0c', ':', '\uff1a', '\u3002', ';', '\uff1b');
    }

    private static bool IsPlausibleCustomerName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = CleanCustomerName(value);
        return candidate.Length is > 0 and <= 20
            && !candidate.Any(char.IsDigit)
            && !candidate.Contains('\u5165')
            && !candidate.Contains('\u51fa');
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
        foreach (var quantityPattern in QuantityRegexes)
        {
            var quantityMatch = quantityPattern.Match(userInput);
            if (!quantityMatch.Success)
            {
                continue;
            }

            if (!double.TryParse(quantityMatch.Groups["value"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var quantity))
            {
                continue;
            }

            unitType = ParseWeightUnit(quantityMatch.Groups["unit"].Value) ?? defaultUnit;
            return quantity;
        }

        unitType = defaultUnit;
        return null;
    }

    private static double? ExtractUnitPrice(string userInput)
    {
        var compactMatch = CompactPriceRegex.Match(userInput);
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

        foreach (var pattern in FixedExplicitPriceRegexes)
        {
            var match = pattern.Match(userInput);
            if (!match.Success)
            {
                continue;
            }

            if (double.TryParse(match.Groups["price"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var price))
            {
                return price;
            }
        }

        return null;
    }

    private static WeightUnit? ParseWeightUnit(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "kilogram" or "kg" or "\u516c\u65a4" => WeightUnit.Kilogram,
            "jin" or "\u65a4" => WeightUnit.Jin,
            "piece" or "pieces" or "\u4ef6" or "\u4e2a" => WeightUnit.Piece,
            _ => Enum.TryParse<WeightUnit>(value, ignoreCase: true, out var parsed)
                ? parsed
                : null
        };
    }

    private static readonly Regex[] QuantityRegexes =
    [
        new Regex(
            "(?:\\u6570\\u91cf|\\u5165\\u5e93|\\u51fa\\u5e93)\\s*(?<value>\\d+(?:\\.\\d+)?)\\s*(?<unit>\\u516c\\u65a4|kg|KG|\\u65a4|\\u4ef6|\\u4e2a|piece|pieces|jin|kilogram)",
            RegexOptions.Compiled),
        new Regex(
            "(?<value>\\d+(?:\\.\\d+)?)\\s*(?<unit>\\u516c\\u65a4|kg|KG|\\u65a4|\\u4ef6|\\u4e2a|piece|pieces|jin|kilogram)",
            RegexOptions.Compiled)
    ];

    private static readonly Regex CompactPriceRegex = new(
        "(?<yuan>\\d+(?:\\.\\d+)?)\\s*\\u5757\\s*(?<jiao>\\d+)",
        RegexOptions.Compiled);

    private static readonly Regex[] ExplicitPriceRegexes =
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

    private static readonly Regex[] FixedExplicitPriceRegexes =
    [
        new Regex(
            "(?:\\u5355\\u4ef7|\\u4ef7\\u683c)\\s*[:\\uff1a]?\\s*(?:\\u00a5|\\uffe5)?\\s*(?<price>\\d+(?:\\.\\d+)?)\\s*(?:\\u00a5|\\uffe5|\\u5143|\\u5757)?",
            RegexOptions.Compiled),
        new Regex(
            "(?:\\u00a5|\\uffe5)\\s*(?<price>\\d+(?:\\.\\d+)?)\\s*(?:(?:/|\\u6bcf)\\s*(?:\\u516c\\u65a4|kg|KG|\\u65a4|\\u4ef6|\\u4e2a|piece|pieces))?",
            RegexOptions.Compiled),
        new Regex(
            "(?<price>\\d+(?:\\.\\d+)?)\\s*(?:\\u00a5|\\uffe5|\\u5143|\\u5757)\\s*(?:(?:/|\\u6bcf)\\s*(?:\\u516c\\u65a4|kg|KG|\\u65a4|\\u4ef6|\\u4e2a|piece|pieces))?",
            RegexOptions.Compiled),
        new Regex(
            "(?:\\u6bcf\\s*(?:\\u516c\\u65a4|kg|KG|\\u65a4|\\u4ef6|\\u4e2a|piece|pieces))\\s*(?:\\u00a5|\\uffe5)?\\s*(?<price>\\d+(?:\\.\\d+)?)",
            RegexOptions.Compiled)
    ];

    private static readonly Regex LeadingCustomerRegex = new(
        "^\\s*(?:\\u7ed9)?(?<candidate>[^0-9\\uff0c,\\u3002:\\uff1a;\\uff1b]{1,40}?)(?:\\u5165\\u5e93|\\u51fa\\u5e93)",
        RegexOptions.Compiled);
}
