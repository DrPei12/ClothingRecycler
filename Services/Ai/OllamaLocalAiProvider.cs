using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace ClothingRecycler.Desktop.Services;

public sealed class OllamaLocalAiProvider : ILocalAiProvider
{
    private static readonly Uri BaseUri = new("http://127.0.0.1:11434");
    private readonly HttpClient _httpClient;

    public OllamaLocalAiProvider()
        : this(new HttpClient { BaseAddress = BaseUri })
    {
    }

    internal OllamaLocalAiProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public AiLocalProviderKind Kind => AiLocalProviderKind.Ollama;

    public async Task<AiProviderProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var response = await _httpClient.GetAsync("/api/tags", cancellationToken);
            var payload = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                return new AiProviderProbeResult
                {
                    Kind = Kind,
                    IsAvailable = false,
                    Summary = "Ollama server did not respond successfully.",
                    Detail = payload
                };
            }

            return new AiProviderProbeResult
            {
                Kind = Kind,
                IsAvailable = true,
                Summary = "Ollama server is reachable on localhost:11434.",
                Detail = payload
            };
        }
        catch (Exception ex)
        {
            return new AiProviderProbeResult
            {
                Kind = Kind,
                IsAvailable = false,
                Summary = "Ollama is not available on this machine.",
                Detail = ex.Message
            };
        }
    }

    public async Task<string> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var model = Environment.GetEnvironmentVariable("CLOTHING_RECYCLER_OLLAMA_MODEL");
        if (string.IsNullOrWhiteSpace(model))
        {
            model = "llama3.1";
        }

        var prompt = BuildPrompt(request.Messages);
        var payload = new
        {
            model,
            prompt,
            stream = false,
            format = "json"
        };

        using var response = await _httpClient.PostAsJsonAsync("/api/generate", payload, cancellationToken);
        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Ollama request failed: {content}");
        }

        using var document = JsonDocument.Parse(content);
        if (!document.RootElement.TryGetProperty("response", out var responseElement))
        {
            throw new InvalidOperationException("Ollama response did not contain a response field.");
        }

        return responseElement.GetString() ?? string.Empty;
    }

    private static string BuildPrompt(IReadOnlyList<AiChatMessage> messages)
    {
        var builder = new StringBuilder();
        foreach (var message in messages)
        {
            builder.AppendLine($"[{message.Role}]");
            builder.AppendLine(message.Content);
            builder.AppendLine();
        }

        return builder.ToString();
    }
}
