using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

using ClothingRecycler.Desktop.Models;
using ClothingRecycler.Desktop.Services;

namespace ClothingRecycler.Desktop.Tests;

public sealed class DeepSeekApiProviderTests
{
    [Fact]
    public async Task CompleteAsync_JsonObjectRequest_UsesExpectedDeepSeekPayload()
    {
        using var workspace = new TemporaryAppDataWorkspace();
        var settingsService = new AppUiSettingsService();
        await settingsService.SaveDeepSeekApiSettingsAsync("https://api.deepseek.com", "deepseek-v4-flash", "test-api-key");

        var handler = new StubHttpMessageHandler(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("https://api.deepseek.com/chat/completions", request.RequestUri?.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-api-key", request.Headers.Authorization?.Parameter);

            var payload = JsonDocument.Parse(request.Content!.ReadAsStringAsync().Result);
            Assert.Equal("deepseek-v4-flash", payload.RootElement.GetProperty("model").GetString());
            Assert.False(payload.RootElement.GetProperty("stream").GetBoolean());
            Assert.Equal("disabled", payload.RootElement.GetProperty("thinking").GetProperty("type").GetString());
            Assert.Equal("json_object", payload.RootElement.GetProperty("response_format").GetProperty("type").GetString());
            Assert.Equal(220, payload.RootElement.GetProperty("max_tokens").GetInt32());
            Assert.Equal("system", payload.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
            Assert.Equal("user", payload.RootElement.GetProperty("messages")[1].GetProperty("role").GetString());

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """
                    {
                      "choices": [
                        {
                          "message": {
                            "content": "{\"message_type\":\"final\",\"payload\":{\"intent\":\"fill_inbound_order\"}}"
                          }
                        }
                      ]
                    }
                    """,
                    Encoding.UTF8,
                    "application/json")
            };
        });

        var provider = new DeepSeekApiProvider(settingsService, new HttpClient(handler));
        var result = await provider.CompleteAsync(new AiCompletionRequest
        {
            MaxNewTokens = 220,
            ResponseFormat = AiCompletionResponseFormat.JsonObject,
            Messages =
            [
                new AiChatMessage { Role = "system", Content = "Return JSON." },
                new AiChatMessage { Role = "user", Content = "Create a draft." }
            ]
        });

        Assert.Contains("\"message_type\":\"final\"", result, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CompleteAsync_AudioRequest_ThrowsHelpfulError()
    {
        using var workspace = new TemporaryAppDataWorkspace();
        var settingsService = new AppUiSettingsService();
        await settingsService.SaveDeepSeekApiSettingsAsync("https://api.deepseek.com", "deepseek-v4-flash", "test-api-key");
        var provider = new DeepSeekApiProvider(settingsService, new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called for audio requests."))));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => provider.CompleteAsync(new AiCompletionRequest
        {
            AudioPath = "voice.wav",
            Messages = [new AiChatMessage { Role = "user", Content = "test" }]
        }));

        Assert.Contains("text requests only", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ProbeAsync_WithoutApiKey_ReturnsUnavailable()
    {
        using var workspace = new TemporaryAppDataWorkspace();
        var settingsService = new AppUiSettingsService();
        var provider = new DeepSeekApiProvider(settingsService, new HttpClient(new StubHttpMessageHandler(_ => throw new InvalidOperationException("HTTP should not be called without API key."))));

        var result = await provider.ProbeAsync();

        Assert.False(result.IsAvailable);
        Assert.Contains("not configured", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }

    private sealed class TemporaryAppDataWorkspace : IDisposable
    {
        private readonly string? _previousRootOverride;
        private readonly string _workspacePath;

        public TemporaryAppDataWorkspace()
        {
            _previousRootOverride = AppDataPaths.RootDirectoryOverride;
            _workspacePath = Path.Combine(Path.GetTempPath(), $"cr-deepseek-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_workspacePath);
            AppDataPaths.RootDirectoryOverride = _workspacePath;
        }

        public void Dispose()
        {
            AppDataPaths.RootDirectoryOverride = _previousRootOverride;
            try
            {
                Directory.Delete(_workspacePath, recursive: true);
            }
            catch
            {
            }
        }
    }
}
