namespace ClothingRecycler.Desktop.Services;

public sealed class AiSpeechRecognitionService
{
    private const string WildcardExtension = "*";
    private const int SilenceThreshold = 128;
    private readonly IReadOnlyDictionary<AsrProviderKind, IAudioTranscriptionProvider> _providers;
    private readonly AppUiSettingsService _uiSettingsService;
    private readonly AppLogger _logger;

    public AiSpeechRecognitionService(
        IEnumerable<IAudioTranscriptionProvider> providers,
        AppUiSettingsService uiSettingsService,
        AppLogger logger)
    {
        _providers = providers.ToDictionary(provider => provider.Kind);
        _uiSettingsService = uiSettingsService;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AsrProviderProbeResult>> ProbeProvidersAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<AsrProviderProbeResult>();
        foreach (var provider in _providers.Values.OrderBy(provider => provider.Kind))
        {
            results.Add(await provider.ProbeAsync(cancellationToken));
        }

        return results;
    }

    public async Task<TranscriptionResult> TranscribeAudioFileAsync(
        string audioPath,
        AudioInputKind inputKind,
        CancellationToken cancellationToken = default)
    {
        var settings = _uiSettingsService.GetAsrRuntimeSettings();
        if (!_providers.TryGetValue(settings.ProviderKind, out var provider))
        {
            return TranscriptionResult.Failed(
                settings.ProviderKind,
                settings.ProviderKind.ToString(),
                AsrErrorCode.ProviderUnavailable,
                $"No ASR provider is registered for {settings.ProviderKind}.");
        }

        var validation = ValidateAudioInput(audioPath, inputKind, provider, settings);
        if (validation.Error is not null)
        {
            return validation.Error;
        }

        var input = validation.Input!;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(settings.Timeout);

            var providerResult = await provider.TranscribeAsync(input, timeoutCts.Token);
            if (!providerResult.Success)
            {
                return providerResult;
            }

            var normalizedText = AsrTextPostProcessor.NormalizeForAgent(providerResult.Text);
            if (string.IsNullOrWhiteSpace(normalizedText))
            {
                return TranscriptionResult.Failed(
                    provider.Kind,
                    provider.DisplayName,
                    AsrErrorCode.EmptyTranscript,
                    "ASR provider returned an empty transcript.",
                    input.Duration);
            }

            var result = new TranscriptionResult
            {
                Success = true,
                ProviderKind = provider.Kind,
                ProviderName = provider.DisplayName,
                Text = providerResult.Text,
                NormalizedText = normalizedText,
                AgentInputText = normalizedText,
                Language = string.IsNullOrWhiteSpace(providerResult.Language) ? input.LanguageHint : providerResult.Language,
                Confidence = providerResult.Confidence,
                Duration = providerResult.Duration ?? input.Duration,
                Segments = providerResult.Segments
            };

            await _logger.LogInfoAsync($"ASR transcript via {provider.Kind}: {TruncateForLog(result.AgentInputText)}");
            return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return TranscriptionResult.Failed(
                provider.Kind,
                provider.DisplayName,
                AsrErrorCode.ProviderTimeout,
                $"ASR provider timed out after {settings.Timeout.TotalSeconds:0.#} seconds.",
                input.Duration);
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"ASR provider {provider.Kind} failed.", ex);
            return TranscriptionResult.Failed(
                provider.Kind,
                provider.DisplayName,
                AsrErrorCode.RecognitionFailed,
                ex.Message,
                input.Duration);
        }
    }

    private static AudioValidationResult ValidateAudioInput(
        string audioPath,
        AudioInputKind inputKind,
        IAudioTranscriptionProvider provider,
        AppUiSettingsService.AsrRuntimeSettings settings)
    {
        if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
        {
            return AudioValidationResult.Fail(TranscriptionResult.Failed(
                provider.Kind,
                provider.DisplayName,
                AsrErrorCode.MissingFile,
                "Audio file does not exist."));
        }

        var fileInfo = new FileInfo(audioPath);
        if (fileInfo.Length == 0)
        {
            return AudioValidationResult.Fail(TranscriptionResult.Failed(
                provider.Kind,
                provider.DisplayName,
                AsrErrorCode.EmptyFile,
                "Audio file is empty."));
        }

        if (fileInfo.Length > settings.MaxAudioBytes)
        {
            return AudioValidationResult.Fail(TranscriptionResult.Failed(
                provider.Kind,
                provider.DisplayName,
                AsrErrorCode.FileTooLarge,
                $"Audio file is larger than the configured ASR limit ({settings.MaxAudioBytes / 1024 / 1024} MB)."));
        }

        var extension = Path.GetExtension(audioPath);
        if (!SupportsExtension(provider, extension))
        {
            return AudioValidationResult.Fail(TranscriptionResult.Failed(
                provider.Kind,
                provider.DisplayName,
                AsrErrorCode.UnsupportedFormat,
                $"ASR provider {provider.DisplayName} does not support {extension}. Current beta supports 16-bit PCM WAV for the default local provider."));
        }

        var wavInfo = string.Equals(extension, ".wav", StringComparison.OrdinalIgnoreCase)
            ? TryReadWavAudioInfo(audioPath)
            : null;

        if (wavInfo is not null)
        {
            if (wavInfo.Duration < settings.MinDuration)
            {
                return AudioValidationResult.Fail(TranscriptionResult.Failed(
                    provider.Kind,
                    provider.DisplayName,
                    AsrErrorCode.AudioTooShort,
                    $"Audio is shorter than the configured ASR minimum ({settings.MinDuration.TotalSeconds:0.#} seconds).",
                    wavInfo.Duration));
            }

            if (wavInfo.Duration > settings.MaxDuration)
            {
                return AudioValidationResult.Fail(TranscriptionResult.Failed(
                    provider.Kind,
                    provider.DisplayName,
                    AsrErrorCode.AudioTooLong,
                    $"Audio is longer than the configured ASR maximum ({settings.MaxDuration.TotalSeconds:0.#} seconds). Long audio segmentation is reserved for the next ASR provider step.",
                    wavInfo.Duration));
            }

            if (wavInfo.BitsPerSample == 16 && LooksSilent(audioPath, wavInfo))
            {
                return AudioValidationResult.Fail(TranscriptionResult.Failed(
                    provider.Kind,
                    provider.DisplayName,
                    AsrErrorCode.NoSpeechDetected,
                    "Audio appears to contain no speech signal.",
                    wavInfo.Duration));
            }
        }

        return AudioValidationResult.Success(new AudioTranscriptionInput
        {
            FilePath = audioPath,
            InputKind = inputKind,
            LanguageHint = settings.Language,
            Duration = wavInfo?.Duration,
            SizeBytes = fileInfo.Length,
            SampleRate = wavInfo?.SampleRate,
            Channels = wavInfo?.Channels,
            Format = extension.TrimStart('.').ToUpperInvariant()
        });
    }

    private static bool SupportsExtension(IAudioTranscriptionProvider provider, string extension)
    {
        return provider.SupportedFileExtensions.Contains(WildcardExtension)
            || provider.SupportedFileExtensions.Contains(extension);
    }

    private static WavAudioInfo? TryReadWavAudioInfo(string audioPath)
    {
        try
        {
            using var stream = File.OpenRead(audioPath);
            using var reader = new BinaryReader(stream);
            if (new string(reader.ReadChars(4)) != "RIFF")
            {
                return null;
            }

            _ = reader.ReadUInt32();
            if (new string(reader.ReadChars(4)) != "WAVE")
            {
                return null;
            }

            ushort? audioFormat = null;
            ushort? channels = null;
            uint? sampleRate = null;
            ushort? bitsPerSample = null;
            long? dataOffset = null;
            uint? dataSize = null;

            while (stream.Position + 8 <= stream.Length)
            {
                var chunkId = new string(reader.ReadChars(4));
                var chunkSize = reader.ReadUInt32();
                var chunkStart = stream.Position;

                if (chunkId == "fmt ")
                {
                    audioFormat = reader.ReadUInt16();
                    channels = reader.ReadUInt16();
                    sampleRate = reader.ReadUInt32();
                    _ = reader.ReadUInt32();
                    _ = reader.ReadUInt16();
                    bitsPerSample = reader.ReadUInt16();
                }
                else if (chunkId == "data")
                {
                    dataOffset = stream.Position;
                    dataSize = chunkSize;
                }

                stream.Position = chunkStart + chunkSize + (chunkSize % 2);
            }

            if (audioFormat != 1
                || channels is null
                || sampleRate is null
                || bitsPerSample is null
                || dataOffset is null
                || dataSize is null)
            {
                return null;
            }

            var bytesPerSecond = sampleRate.Value * channels.Value * (bitsPerSample.Value / 8.0);
            var duration = bytesPerSecond <= 0
                ? TimeSpan.Zero
                : TimeSpan.FromSeconds(dataSize.Value / bytesPerSecond);

            return new WavAudioInfo(
                Duration: duration,
                SampleRate: (int)sampleRate.Value,
                Channels: channels.Value,
                BitsPerSample: bitsPerSample.Value,
                DataOffset: dataOffset.Value,
                DataSize: dataSize.Value);
        }
        catch
        {
            return null;
        }
    }

    private static bool LooksSilent(string audioPath, WavAudioInfo wavInfo)
    {
        const int maxBytesToScan = 1024 * 1024;
        var bytesToScan = (int)Math.Min(wavInfo.DataSize, maxBytesToScan);
        if (bytesToScan < 2)
        {
            return true;
        }

        using var stream = File.OpenRead(audioPath);
        stream.Position = wavInfo.DataOffset;
        var buffer = new byte[bytesToScan - (bytesToScan % 2)];
        var read = stream.Read(buffer, 0, buffer.Length);
        if (read < 2)
        {
            return true;
        }

        for (var index = 0; index + 1 < read; index += 2)
        {
            var sample = BitConverter.ToInt16(buffer, index);
            if (Math.Abs(sample) > SilenceThreshold)
            {
                return false;
            }
        }

        return true;
    }

    private static string TruncateForLog(string value)
    {
        const int maxLength = 600;
        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength] + " ...[truncated]";
    }

    private sealed record AudioValidationResult(AudioTranscriptionInput? Input, TranscriptionResult? Error)
    {
        public static AudioValidationResult Success(AudioTranscriptionInput input) => new(input, null);

        public static AudioValidationResult Fail(TranscriptionResult error) => new(null, error);
    }

    private sealed record WavAudioInfo(
        TimeSpan Duration,
        int SampleRate,
        int Channels,
        int BitsPerSample,
        long DataOffset,
        long DataSize);
}
