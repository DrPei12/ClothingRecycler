using ClothingRecycler.Desktop.Models;
using ClothingRecycler.Desktop.Services;

namespace ClothingRecycler.Desktop.Tests;

public sealed class AiSpeechRecognitionServiceTests
{
    [Fact]
    public async Task TranscribeAudioFileAsync_ValidWav_ReturnsNormalizedAgentInput()
    {
        using var workspace = new TemporaryAudioWorkspace();
        var audioPath = workspace.CreatePcmWav(TimeSpan.FromSeconds(1), amplitude: 1200);
        var provider = new StubAsrProvider("""{"text":"  转写：王姐 入库 夏装 12 斤，3 块 8  "}""");
        var service = CreateService(provider);

        var result = await service.TranscribeAudioFileAsync(audioPath, AudioInputKind.UploadedFile);

        Assert.True(result.Success);
        Assert.Equal("王姐入库夏装12斤，3块8", result.AgentInputText);
        Assert.Equal(AsrProviderKind.HuggingFaceUltravoxLocal, result.ProviderKind);
        Assert.Equal("zh-CN", result.Language);
        Assert.NotNull(result.Duration);
    }

    [Fact]
    public async Task TranscribeAudioFileAsync_SilentWav_ReturnsNoSpeechDetected()
    {
        using var workspace = new TemporaryAudioWorkspace();
        var audioPath = workspace.CreatePcmWav(TimeSpan.FromSeconds(1), amplitude: 0);
        var service = CreateService(new StubAsrProvider("should not be called"));

        var result = await service.TranscribeAudioFileAsync(audioPath, AudioInputKind.MicrophoneRecording);

        Assert.False(result.Success);
        Assert.Equal(AsrErrorCode.NoSpeechDetected, result.ErrorCode);
    }

    [Fact]
    public async Task TranscribeAudioFileAsync_TooShortWav_ReturnsAudioTooShort()
    {
        using var workspace = new TemporaryAudioWorkspace();
        var audioPath = workspace.CreatePcmWav(TimeSpan.FromMilliseconds(200), amplitude: 1200);
        var service = CreateService(new StubAsrProvider("should not be called"));

        var result = await service.TranscribeAudioFileAsync(audioPath, AudioInputKind.MicrophoneRecording);

        Assert.False(result.Success);
        Assert.Equal(AsrErrorCode.AudioTooShort, result.ErrorCode);
    }

    [Fact]
    public async Task TranscribeAudioFileAsync_UnsupportedExtension_ReturnsUnsupportedFormat()
    {
        using var workspace = new TemporaryAudioWorkspace();
        var audioPath = workspace.CreateTextAudioPlaceholder("voice.mp3");
        var service = CreateService(new StubAsrProvider("should not be called"));

        var result = await service.TranscribeAudioFileAsync(audioPath, AudioInputKind.UploadedFile);

        Assert.False(result.Success);
        Assert.Equal(AsrErrorCode.UnsupportedFormat, result.ErrorCode);
    }

    [Fact]
    public async Task TranscribeAudioFileAsync_ProviderReturnsEmptyText_ReturnsEmptyTranscript()
    {
        using var workspace = new TemporaryAudioWorkspace();
        var audioPath = workspace.CreatePcmWav(TimeSpan.FromSeconds(1), amplitude: 1200);
        var service = CreateService(new StubAsrProvider("   "));

        var result = await service.TranscribeAudioFileAsync(audioPath, AudioInputKind.UploadedFile);

        Assert.False(result.Success);
        Assert.Equal(AsrErrorCode.EmptyTranscript, result.ErrorCode);
    }

    private static AiSpeechRecognitionService CreateService(params IAudioTranscriptionProvider[] providers)
    {
        return new AiSpeechRecognitionService(
            providers,
            new AppUiSettingsService(),
            new AppLogger());
    }

    private sealed class StubAsrProvider : IAudioTranscriptionProvider
    {
        private readonly string _transcript;

        public StubAsrProvider(string transcript)
        {
            _transcript = transcript;
        }

        public AsrProviderKind Kind => AsrProviderKind.HuggingFaceUltravoxLocal;

        public string DisplayName => "Stub ASR";

        public IReadOnlySet<string> SupportedFileExtensions { get; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            ".wav"
        };

        public Task<AsrProviderProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AsrProviderProbeResult
            {
                Kind = Kind,
                IsAvailable = true,
                Summary = "Stub ASR is ready.",
                Detail = string.Empty
            });
        }

        public Task<TranscriptionResult> TranscribeAsync(AudioTranscriptionInput input, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TranscriptionResult
            {
                Success = true,
                ProviderKind = Kind,
                ProviderName = DisplayName,
                Text = _transcript,
                Language = input.LanguageHint,
                Duration = input.Duration
            });
        }
    }

    private sealed class TemporaryAudioWorkspace : IDisposable
    {
        private readonly string _directory;

        public TemporaryAudioWorkspace()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"cr-asr-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);
        }

        public string CreatePcmWav(TimeSpan duration, short amplitude)
        {
            var path = Path.Combine(_directory, $"voice-{Guid.NewGuid():N}.wav");
            var sampleRate = 16000;
            var channels = 1;
            var bitsPerSample = 16;
            var sampleCount = Math.Max(1, (int)Math.Round(duration.TotalSeconds * sampleRate));
            var dataSize = sampleCount * channels * bitsPerSample / 8;

            using var stream = File.Create(path);
            using var writer = new BinaryWriter(stream);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + dataSize);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
            writer.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(sampleRate * channels * bitsPerSample / 8);
            writer.Write((short)(channels * bitsPerSample / 8));
            writer.Write((short)bitsPerSample);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data"));
            writer.Write(dataSize);

            for (var index = 0; index < sampleCount; index++)
            {
                writer.Write(amplitude);
            }

            return path;
        }

        public string CreateTextAudioPlaceholder(string fileName)
        {
            var path = Path.Combine(_directory, fileName);
            File.WriteAllText(path, "not an audio file");
            return path;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch
            {
            }
        }
    }
}
