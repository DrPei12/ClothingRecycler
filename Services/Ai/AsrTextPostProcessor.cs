using System.Text.Json;
using System.Text.RegularExpressions;

namespace ClothingRecycler.Desktop.Services;

public static class AsrTextPostProcessor
{
    public static string NormalizeForAgent(string rawText)
    {
        var text = ExtractPlainText(rawText);
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = StripKnownPrefixes(text.Trim());
        text = CollapseWhitespace(text);
        text = RemoveSpacesBetweenCjk(text);
        text = RemoveSpacesAroundBusinessTokens(text);
        return text.Trim(' ', '\t', '\r', '\n', '"', '\'', '\u201c', '\u201d');
    }

    private static string ExtractPlainText(string rawText)
    {
        var text = rawText.Trim();
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        text = text
            .Replace("```json", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace("```", string.Empty, StringComparison.Ordinal)
            .Trim();

        if (TryExtractJsonText(text, out var jsonText))
        {
            return jsonText.Trim();
        }

        return text.Trim();
    }

    private static bool TryExtractJsonText(string text, out string extracted)
    {
        extracted = string.Empty;

        try
        {
            using var document = JsonDocument.Parse(text);
            extracted = ExtractTranscriptFromJson(document.RootElement) ?? string.Empty;
            return !string.IsNullOrWhiteSpace(extracted);
        }
        catch
        {
            return false;
        }
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

    private static string StripKnownPrefixes(string text)
    {
        foreach (var prefix in new[] { "转写：", "转写:", "识别：", "识别:", "用户说：", "用户说:" })
        {
            if (text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return text[prefix.Length..].Trim();
            }
        }

        return text;
    }

    private static string CollapseWhitespace(string text)
    {
        return WhitespaceRegex.Replace(text, " ").Trim();
    }

    private static string RemoveSpacesBetweenCjk(string text)
    {
        return CjkWhitespaceRegex.Replace(text, "$1$2");
    }

    private static string RemoveSpacesAroundBusinessTokens(string text)
    {
        string previous;
        do
        {
            previous = text;
            text = CjkNumberUnitWhitespaceRegex.Replace(text, "$1$2");
        }
        while (!string.Equals(previous, text, StringComparison.Ordinal));

        return text;
    }

    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    private static readonly Regex CjkWhitespaceRegex = new(@"([\u4e00-\u9fff])\s+([\u4e00-\u9fff])", RegexOptions.Compiled);

    private static readonly Regex CjkNumberUnitWhitespaceRegex = new(@"([\u4e00-\u9fff\d])\s+([\u4e00-\u9fff\d])", RegexOptions.Compiled);
}
