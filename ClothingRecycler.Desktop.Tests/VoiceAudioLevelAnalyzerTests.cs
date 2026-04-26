using ClothingRecycler.Desktop.Services;

namespace ClothingRecycler.Desktop.Tests;

public sealed class VoiceAudioLevelAnalyzerTests
{
    [Fact]
    public void AnalyzeFile_SilentWav_ReturnsNoSpeech()
    {
        using var workspace = new TemporaryAudioWorkspace();
        var audioPath = workspace.CreatePcmWav(TimeSpan.FromSeconds(1), amplitude: 0);

        var snapshot = VoiceAudioLevelAnalyzer.AnalyzeFile(audioPath);

        Assert.True(snapshot.IsAvailable);
        Assert.False(snapshot.HasSpeech);
        Assert.Equal(0, snapshot.LevelPercent, precision: 1);
    }

    [Fact]
    public void AnalyzeFile_AudibleWav_ReturnsSpeechLevel()
    {
        using var workspace = new TemporaryAudioWorkspace();
        var audioPath = workspace.CreatePcmWav(TimeSpan.FromSeconds(1), amplitude: 2400);

        var snapshot = VoiceAudioLevelAnalyzer.AnalyzeFile(audioPath);

        Assert.True(snapshot.IsAvailable);
        Assert.True(snapshot.HasSpeech);
        Assert.True(snapshot.LevelPercent > 0);
        Assert.NotNull(snapshot.Duration);
    }

    [Fact]
    public void AnalyzeFile_MissingFile_ReturnsUnavailable()
    {
        var snapshot = VoiceAudioLevelAnalyzer.AnalyzeFile(Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.wav"));

        Assert.False(snapshot.IsAvailable);
        Assert.False(snapshot.HasSpeech);
    }

    private sealed class TemporaryAudioWorkspace : IDisposable
    {
        private readonly string _directory;

        public TemporaryAudioWorkspace()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"cr-voice-level-tests-{Guid.NewGuid():N}");
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
