using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace ClothingRecycler.Desktop.Services;

public sealed class HuggingFaceUltravoxPythonProvider : ILocalAiProvider
{
    private const string DefaultModelId = "fixie-ai/ultravox-v0_5-llama-3_2-1b";
    private const string ServerPrefix = "__CR_JSON__:";
    private readonly SemaphoreSlim _bridgeLock = new(1, 1);
    private readonly ConcurrentQueue<string> _bridgeErrorLines = new();
    private readonly AppLogger _logger;
    private Process? _bridgeProcess;
    private StreamWriter? _bridgeInput;
    private StreamReader? _bridgeOutput;

    public HuggingFaceUltravoxPythonProvider(AppLogger logger)
    {
        _logger = logger;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => DisposeBridgeProcess();
    }

    public AiLocalProviderKind Kind => AiLocalProviderKind.HuggingFaceUltravoxPython;

    public async Task<AiProviderProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        var bundleStatus = GetLocalBundleStatus();
        if (!bundleStatus.IsReady)
        {
            return new AiProviderProbeResult
            {
                Kind = Kind,
                IsAvailable = false,
                Summary = "Local Ultravox bundle is not ready yet.",
                Detail = BuildBundleGuidance(bundleStatus)
            };
        }

        var result = await RunScriptAsync(["--probe", "--model-id", ResolveModelId()], cancellationToken);
        var jsonText = ExtractJsonPayload(result.StdOut);
        if (string.IsNullOrWhiteSpace(jsonText))
        {
            return new AiProviderProbeResult
            {
                Kind = Kind,
                IsAvailable = false,
                Summary = "Probe script returned no JSON output.",
                Detail = string.IsNullOrWhiteSpace(result.StdErr) ? result.StdOut : result.StdErr
            };
        }

        using var document = JsonDocument.Parse(jsonText);
        var root = document.RootElement;
        var ok = root.TryGetProperty("ok", out var okElement) && okElement.GetBoolean();
        var summary = root.TryGetProperty("summary", out var summaryElement)
            ? summaryElement.GetString() ?? string.Empty
            : string.Empty;

        return new AiProviderProbeResult
        {
            Kind = Kind,
            IsAvailable = ok && result.ExitCode == 0,
            Summary = summary,
            Detail = jsonText
        };
    }

    public async Task WarmAsync(CancellationToken cancellationToken = default)
    {
        var bundleStatus = GetLocalBundleStatus();
        if (!bundleStatus.IsReady)
        {
            throw new InvalidOperationException(BuildBundleGuidance(bundleStatus));
        }

        await _bridgeLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureBridgeProcessAsync(cancellationToken);
            var response = await SendBridgeCommandAsync(
                new
                {
                    op = "warm",
                    model_id = ResolveModelId()
                },
                allowRawTextFallback: false,
                cancellationToken);

            EnsureBridgeResponseSucceeded(response);
        }
        finally
        {
            _bridgeLock.Release();
        }
    }

    public async Task<string> CompleteAsync(AiCompletionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var bundleStatus = GetLocalBundleStatus();
        if (!bundleStatus.IsReady)
        {
            throw new InvalidOperationException(BuildBundleGuidance(bundleStatus));
        }

        var payload = new
        {
            model_id = ResolveModelId(),
            audio_path = request.AudioPath,
            max_new_tokens = request.MaxNewTokens,
            messages = request.Messages.Select(message => new
            {
                role = message.Role,
                content = message.Content
            })
        };

        await _bridgeLock.WaitAsync(cancellationToken);
        try
        {
            await EnsureBridgeProcessAsync(cancellationToken);
            var response = await SendBridgeCommandAsync(
                new
                {
                    op = "complete",
                    model_id = ResolveModelId(),
                    request = payload
                },
                allowRawTextFallback: true,
                cancellationToken);

            EnsureBridgeResponseSucceeded(response);
            return response.RootElement.TryGetProperty("text", out var textElement)
                ? textElement.GetString() ?? string.Empty
                : string.Empty;
        }
        finally
        {
            _bridgeLock.Release();
        }
    }

    private async Task EnsureBridgeProcessAsync(CancellationToken cancellationToken)
    {
        if (_bridgeProcess is { HasExited: false } && _bridgeInput is not null && _bridgeOutput is not null)
        {
            return;
        }

        DisposeBridgeProcess();
        while (_bridgeErrorLines.TryDequeue(out _))
        {
        }

        var scriptPath = ResolveScriptPath();
        var startInfo = new ProcessStartInfo
        {
            FileName = "python",
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.Environment["CLOTHING_RECYCLER_HF_LOCAL_BUNDLE_ROOT"] = AppDataPaths.AiModelDirectory;
        startInfo.Environment["HF_HUB_DISABLE_XET"] = "1";
        startInfo.Environment["PYTHONUTF8"] = "1";
        startInfo.ArgumentList.Add(scriptPath);
        startInfo.ArgumentList.Add("--stdio-server");
        startInfo.ArgumentList.Add("--model-id");
        startInfo.ArgumentList.Add(ResolveModelId());

        var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the Hugging Face bridge server.");

        _bridgeProcess = process;
        _bridgeInput = process.StandardInput;
        _bridgeOutput = process.StandardOutput;
        _ = Task.Run(() => PumpBridgeErrorsAsync(process.StandardError, process));

        using var response = await ReadServerResponseAsync(false, cancellationToken);
        EnsureBridgeResponseSucceeded(response);
    }

    private async Task<JsonDocument> SendBridgeCommandAsync(object command, bool allowRawTextFallback, CancellationToken cancellationToken)
    {
        if (_bridgeProcess is null || _bridgeInput is null || _bridgeOutput is null)
        {
            throw new InvalidOperationException("The Hugging Face bridge server is not running.");
        }

        var commandJson = JsonSerializer.Serialize(command);
        await _bridgeInput.WriteLineAsync(commandJson.AsMemory(), cancellationToken);
        await _bridgeInput.FlushAsync();

        return await ReadServerResponseAsync(allowRawTextFallback, cancellationToken);
    }

    private async Task<JsonDocument> ReadServerResponseAsync(bool allowRawTextFallback, CancellationToken cancellationToken)
    {
        if (_bridgeProcess is null || _bridgeOutput is null)
        {
            throw new InvalidOperationException("The Hugging Face bridge server is not running.");
        }

        while (true)
        {
            var line = await _bridgeOutput.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                var errorText = GetBridgeErrorText();
                throw new InvalidOperationException(string.IsNullOrWhiteSpace(errorText)
                    ? "The Hugging Face bridge server closed unexpectedly."
                    : errorText);
            }

            if (!line.StartsWith(ServerPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            var jsonText = line[ServerPrefix.Length..];
            if (TryParseServerPayload(jsonText, out var document))
            {
                return document!;
            }

            await _logger.LogWarningAsync($"HF bridge returned a malformed server payload. Raw: {TruncateForLog(jsonText)}");
            if (allowRawTextFallback)
            {
                return JsonDocument.Parse(JsonSerializer.Serialize(new
                {
                    ok = true,
                    text = UnescapeJsonLikeText(jsonText)
                }));
            }

            throw new JsonException($"HF bridge returned malformed JSON. Raw: {TruncateForLog(jsonText)}");
        }
    }

    private static void EnsureBridgeResponseSucceeded(JsonDocument response)
    {
        var root = response.RootElement;
        if (root.TryGetProperty("ok", out var okElement) && okElement.GetBoolean())
        {
            return;
        }

        var error = root.TryGetProperty("error", out var errorElement)
            ? errorElement.GetString()
            : root.TryGetProperty("summary", out var summaryElement)
                ? summaryElement.GetString()
                : "Hugging Face bridge reported a failure.";

        throw new InvalidOperationException(string.IsNullOrWhiteSpace(error)
            ? "Hugging Face bridge reported a failure."
            : error);
    }

    private async Task PumpBridgeErrorsAsync(StreamReader errorReader, Process process)
    {
        try
        {
            while (!process.HasExited)
            {
                var line = await errorReader.ReadLineAsync();
                if (line is null)
                {
                    break;
                }

                _bridgeErrorLines.Enqueue(line);
                while (_bridgeErrorLines.Count > 40 && _bridgeErrorLines.TryDequeue(out _))
                {
                }
            }
        }
        catch
        {
            // Ignore background stderr pump failures and rely on foreground operations to surface the main error.
        }
    }

    private string GetBridgeErrorText()
    {
        if (_bridgeErrorLines.IsEmpty)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, _bridgeErrorLines.ToArray());
    }

    private static async Task<ScriptRunResult> RunScriptAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var scriptPath = ResolveScriptPath();
        var startInfo = new ProcessStartInfo
        {
            FileName = "python",
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };
        startInfo.Environment["CLOTHING_RECYCLER_HF_LOCAL_BUNDLE_ROOT"] = AppDataPaths.AiModelDirectory;
        startInfo.Environment["HF_HUB_DISABLE_XET"] = "1";
        startInfo.Environment["PYTHONUTF8"] = "1";

        startInfo.ArgumentList.Add(scriptPath);
        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start the Hugging Face bridge script.");

        var stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        await process.WaitForExitAsync(cancellationToken);

        return new ScriptRunResult(
            process.ExitCode,
            await stdOutTask,
            await stdErrTask);
    }

    private static string ResolveScriptPath()
    {
        return ResolveScriptFilePath("hf_ultravox_bridge.py");
    }

    private static string ResolveModelId()
    {
        var configured = Environment.GetEnvironmentVariable("CLOTHING_RECYCLER_HF_MODEL_ID");
        return string.IsNullOrWhiteSpace(configured) ? DefaultModelId : configured.Trim();
    }

    private static LocalBundleStatus GetLocalBundleStatus()
    {
        var modelId = ResolveModelId();
        var bundleRoot = Path.Combine(AppDataPaths.AiModelDirectory, modelId[(modelId.LastIndexOf('/') + 1)..]);
        var missingFiles = new List<string>();
        AddIfMissing(missingFiles, bundleRoot, "adapter", "config.json");
        AddIfMissing(missingFiles, bundleRoot, "adapter", "model.safetensors");
        AddIfMissing(missingFiles, bundleRoot, "text", "config.json");
        AddIfMissing(missingFiles, bundleRoot, "text", "model.safetensors");
        AddIfMissing(missingFiles, bundleRoot, "audio", "config.json");
        AddIfMissing(missingFiles, bundleRoot, "audio", "model.safetensors");

        return new LocalBundleStatus(bundleRoot, missingFiles);
    }

    private static void AddIfMissing(List<string> missingFiles, string bundleRoot, string componentName, string fileName)
    {
        var path = Path.Combine(bundleRoot, componentName, fileName);
        if (!File.Exists(path))
        {
            missingFiles.Add(path);
        }
    }

    private static string BuildBundleGuidance(LocalBundleStatus bundleStatus)
    {
        var scriptPath = ResolveScriptPath();
        var prepareScriptPath = ResolvePrepareScriptPath();
        var command = $"python {scriptPath} --probe --model-id {ResolveModelId()}";
        var downloadCommand = $"python {prepareScriptPath}";
        var missingSummary = string.Join(Environment.NewLine, bundleStatus.MissingFiles.Take(6).Select(path => $"- {path}"));

        return
            $"The local Ultravox bundle is incomplete at {bundleStatus.BundleRoot}.{Environment.NewLine}" +
            $"Missing files:{Environment.NewLine}{missingSummary}{Environment.NewLine}" +
            $"Download models first, then try AI (Beta) again.{Environment.NewLine}" +
            $"Suggested command: {downloadCommand}{Environment.NewLine}" +
            $"Bridge probe command: {command}";
    }

    private static string ResolvePrepareScriptPath()
    {
        return ResolveScriptFilePath("prepare_ultravox_bundle.py");
    }

    private static string ResolveScriptFilePath(string fileName)
    {
        foreach (var root in EnumerateCandidateRoots())
        {
            var candidate = Path.Combine(root, "scripts", "ai", fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        throw new FileNotFoundException($"Could not locate scripts/ai/{fileName}.");
    }

    private static IEnumerable<string> EnumerateCandidateRoots()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var seed in new[] { AppContext.BaseDirectory, Directory.GetCurrentDirectory() })
        {
            if (string.IsNullOrWhiteSpace(seed))
            {
                continue;
            }

            var directory = new DirectoryInfo(Path.GetFullPath(seed));
            while (directory is not null)
            {
                if (seen.Add(directory.FullName))
                {
                    yield return directory.FullName;
                }

                directory = directory.Parent;
            }
        }
    }

    private static string ExtractJsonPayload(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return string.Empty;
        }

        foreach (var line in rawText.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.StartsWith("{", StringComparison.Ordinal) && line.EndsWith("}", StringComparison.Ordinal))
            {
                return line;
            }
        }

        return string.Empty;
    }

    private static bool TryParseServerPayload(string rawText, out JsonDocument? document)
    {
        document = null;
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

        var extracted = ExtractJsonPayload(trimmed);
        if (!string.IsNullOrWhiteSpace(extracted) && seen.Add(extracted))
        {
            yield return extracted;
        }

        var unescaped = UnescapeJsonLikeText(trimmed);
        if (!string.IsNullOrWhiteSpace(unescaped) && seen.Add(unescaped))
        {
            yield return unescaped;
        }

        var unescapedExtracted = ExtractJsonPayload(unescaped);
        if (!string.IsNullOrWhiteSpace(unescapedExtracted) && seen.Add(unescapedExtracted))
        {
            yield return unescapedExtracted;
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
            return false;
        }
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

    private static string TruncateForLog(string rawText)
    {
        const int maxLength = 1200;
        var trimmed = rawText.Trim();
        return trimmed.Length <= maxLength
            ? trimmed
            : trimmed[..maxLength] + " ...[truncated]";
    }

    private void DisposeBridgeProcess()
    {
        try
        {
            _bridgeInput?.Dispose();
            _bridgeOutput?.Dispose();
        }
        catch
        {
        }

        try
        {
            if (_bridgeProcess is { HasExited: false })
            {
                _bridgeProcess.Kill(true);
                _bridgeProcess.WaitForExit(2000);
            }
        }
        catch
        {
        }
        finally
        {
            _bridgeProcess?.Dispose();
            _bridgeProcess = null;
            _bridgeInput = null;
            _bridgeOutput = null;
        }
    }

    private readonly record struct LocalBundleStatus(string BundleRoot, IReadOnlyList<string> MissingFiles)
    {
        public bool IsReady => MissingFiles.Count == 0;
    }

    private readonly record struct ScriptRunResult(int ExitCode, string StdOut, string StdErr);
}
