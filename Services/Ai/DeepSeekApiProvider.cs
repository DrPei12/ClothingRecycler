using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace ClothingRecycler.Desktop.Services;

public sealed class DeepSeekApiProvider : ILocalAiProvider
{
    private readonly AppUiSettingsService _uiSettingsService;
    private readonly HttpClient _httpClient;

    public DeepSeekApiProvider(AppUiSettingsService uiSettingsService)
        : this(uiSettingsService, new HttpClient())
    {
    }

    public DeepSeekApiProvider(AppUiSettingsService uiSettingsService, HttpClient httpClient)
    {
        _uiSettingsService = uiSettingsService;
        _httpClient = httpClient;
    }

    public AiLocalProviderKind Kind => AiLocalProviderKind.DeepSeekApi;

    public async Task<AiProviderProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var settings = _uiSettingsService.GetDeepSeekApiRuntimeSettings();
        if (!settings.IsConfigured)
        {
            return new AiProviderProbeResult
            {
                Kind = Kind,
                IsAvailable = false,
                Summary = "DeepSeek API key is not configured yet.",
                Detail = "Open AI (Beta), switch provider to DeepSeek API (Beta), then save the local API key before testing."
            };
        }

        try
        {
            using var request = CreateRequest(HttpMethod.Get, settings, "/models");
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new AiProviderProbeResult
                {
                    Kind = Kind,
                    IsAvailable = false,
                    Summary = "DeepSeek API probe failed.",
                    Detail = content
                };
            }

            var modelListed = TryModelListed(content, settings.Model);
            return new AiProviderProbeResult
            {
                Kind = Kind,
                IsAvailable = true,
                Summary = modelListed
                    ? $"DeepSeek API is reachable. Model {settings.Model} is ready."
                    : $"DeepSeek API is reachable. Requests will use {settings.Model}.",
                Detail = $"base_url={settings.BaseUrl}; model={settings.Model}; key_source={settings.SourceDescription}"
            };
        }
        catch (Exception ex)
        {
            return new AiProviderProbeResult
            {
                Kind = Kind,
                IsAvailable = false,
                Summary = "DeepSeek API is not reachable from this machine.",
                Detail = ex.Message
            };
        }
    }

    public async Task<string> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!string.IsNullOrWhiteSpace(request.AudioPath))
        {
            throw new InvalidOperationException("DeepSeek API beta currently supports text requests only. Voice input still needs the local Hugging Face runtime.");
        }

        var settings = _uiSettingsService.GetDeepSeekApiRuntimeSettings();
        if (!settings.IsConfigured)
        {
            throw new InvalidOperationException("DeepSeek API key is not configured yet.");
        }

        var payload = new Dictionary<string, object?>
        {
            ["model"] = settings.Model,
            ["messages"] = request.Messages.Select(message => new
            {
                role = message.Role,
                content = message.Content
            }).ToArray(),
            ["stream"] = false,
            ["max_tokens"] = Math.Max(64, request.MaxNewTokens),
            ["temperature"] = 0.1,
            ["thinking"] = new { type = "disabled" }
        };

        if (request.ResponseFormat == AiCompletionResponseFormat.JsonObject)
        {
            payload["response_format"] = new { type = "json_object" };
        }

        using var httpRequest = CreateRequest(HttpMethod.Post, settings, "/chat/completions");
        httpRequest.Content = JsonContent.Create(payload);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"DeepSeek API request failed: {content}");
        }

        using var document = JsonDocument.Parse(content);
        if (!document.RootElement.TryGetProperty("choices", out var choicesElement)
            || choicesElement.ValueKind != JsonValueKind.Array
            || choicesElement.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("DeepSeek API response did not contain choices.");
        }

        var firstChoice = choicesElement[0];
        if (!firstChoice.TryGetProperty("message", out var messageElement))
        {
            throw new InvalidOperationException("DeepSeek API response did not contain a message.");
        }

        var messageContent = messageElement.TryGetProperty("content", out var contentElement)
            ? contentElement.GetString()
            : null;

        if (string.IsNullOrWhiteSpace(messageContent))
        {
            throw new InvalidOperationException("DeepSeek API returned an empty message.");
        }

        return messageContent.Trim();
    }

    private static HttpRequestMessage CreateRequest(
        HttpMethod method,
        AppUiSettingsService.DeepSeekApiRuntimeSettings settings,
        string relativePath)
    {
        var endpoint = BuildEndpoint(settings.BaseUrl, relativePath);
        var request = new HttpRequestMessage(method, endpoint);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.ApiKey);
        return request;
    }

    private static Uri BuildEndpoint(string baseUrl, string relativePath)
    {
        var normalizedBaseUrl = baseUrl.EndsWith("/", StringComparison.Ordinal) ? baseUrl : baseUrl + "/";
        return new Uri(new Uri(normalizedBaseUrl), relativePath.TrimStart('/'));
    }

    private static bool TryModelListed(string content, string model)
    {
        try
        {
            using var document = JsonDocument.Parse(content);
            if (!document.RootElement.TryGetProperty("data", out var dataElement)
                || dataElement.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            foreach (var item in dataElement.EnumerateArray())
            {
                if (item.TryGetProperty("id", out var idElement)
                    && string.Equals(idElement.GetString(), model, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
        }
        catch
        {
        }

        return false;
    }
}
