namespace ClothingRecycler.Desktop.Services;

public sealed class VoiceAudioLevelSnapshot
{
    public bool IsAvailable { get; init; }

    public bool HasSpeech { get; init; }

    public double Peak { get; init; }

    public double Rms { get; init; }

    public double LevelPercent { get; init; }

    public TimeSpan? Duration { get; init; }

    public string Detail { get; init; } = string.Empty;

    public static VoiceAudioLevelSnapshot Unavailable(string detail)
    {
        return new VoiceAudioLevelSnapshot
        {
            IsAvailable = false,
            Detail = detail
        };
    }
}

public static class VoiceAudioLevelAnalyzer
{
    private const int MaxBytesToScan = 64 * 1024;
    private const double PeakSpeechThreshold = 0.015;
    private const double RmsSpeechThreshold = 0.004;

    public static VoiceAudioLevelSnapshot AnalyzeFile(string audioPath)
    {
        if (string.IsNullOrWhiteSpace(audioPath) || !File.Exists(audioPath))
        {
            return VoiceAudioLevelSnapshot.Unavailable("Recording file is not ready yet.");
        }

        try
        {
            var fileInfo = new FileInfo(audioPath);
            if (fileInfo.Length < 44)
            {
                return VoiceAudioLevelSnapshot.Unavailable("Waiting for audio header.");
            }

            using var stream = new FileStream(audioPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var wavInfo = TryReadWavAudioInfo(stream);
            if (wavInfo is null)
            {
                return VoiceAudioLevelSnapshot.Unavailable("Only 16-bit PCM WAV level analysis is supported.");
            }

            var availableDataSize = Math.Max(0, Math.Min(wavInfo.DataSize, stream.Length - wavInfo.DataOffset));
            if (availableDataSize < 2)
            {
                return VoiceAudioLevelSnapshot.Unavailable("Waiting for audio samples.");
            }

            var bytesToScan = (int)Math.Min(availableDataSize, MaxBytesToScan);
            bytesToScan -= bytesToScan % 2;
            if (bytesToScan < 2)
            {
                return VoiceAudioLevelSnapshot.Unavailable("Waiting for audio samples.");
            }

            stream.Position = wavInfo.DataOffset + availableDataSize - bytesToScan;
            var buffer = new byte[bytesToScan];
            var read = stream.Read(buffer, 0, buffer.Length);
            if (read < 2)
            {
                return VoiceAudioLevelSnapshot.Unavailable("Waiting for audio samples.");
            }

            var sampleCount = 0;
            var peak = 0;
            var sumSquares = 0.0;
            for (var index = 0; index + 1 < read; index += 2)
            {
                var sample = BitConverter.ToInt16(buffer, index);
                var absolute = Math.Abs(sample);
                peak = Math.Max(peak, absolute);
                sumSquares += sample * (double)sample;
                sampleCount++;
            }

            if (sampleCount == 0)
            {
                return VoiceAudioLevelSnapshot.Unavailable("Waiting for audio samples.");
            }

            var peakRatio = Math.Clamp(peak / 32768.0, 0, 1);
            var rmsRatio = Math.Clamp(Math.Sqrt(sumSquares / sampleCount) / 32768.0, 0, 1);
            var levelPercent = Math.Clamp(Math.Max(peakRatio * 100, Math.Sqrt(rmsRatio) * 100), 0, 100);
            TimeSpan? duration = wavInfo.BytesPerSecond <= 0
                ? null
                : TimeSpan.FromSeconds(availableDataSize / wavInfo.BytesPerSecond);

            return new VoiceAudioLevelSnapshot
            {
                IsAvailable = true,
                HasSpeech = peakRatio >= PeakSpeechThreshold || rmsRatio >= RmsSpeechThreshold,
                Peak = peakRatio,
                Rms = rmsRatio,
                LevelPercent = levelPercent,
                Duration = duration,
                Detail = $"peak={peakRatio:0.000}; rms={rmsRatio:0.000}"
            };
        }
        catch (IOException)
        {
            return VoiceAudioLevelSnapshot.Unavailable("Recording file is busy.");
        }
        catch (UnauthorizedAccessException)
        {
            return VoiceAudioLevelSnapshot.Unavailable("Recording file is not readable yet.");
        }
        catch (Exception ex)
        {
            return VoiceAudioLevelSnapshot.Unavailable(ex.Message);
        }
    }

    private static WavAudioInfo? TryReadWavAudioInfo(Stream stream)
    {
        using var reader = new BinaryReader(stream, System.Text.Encoding.ASCII, leaveOpen: true);
        stream.Position = 0;
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

            var nextPosition = chunkStart + chunkSize + (chunkSize % 2);
            if (nextPosition <= chunkStart || nextPosition > stream.Length)
            {
                break;
            }

            stream.Position = nextPosition;
        }

        if (audioFormat != 1
            || channels is null
            || sampleRate is null
            || bitsPerSample != 16
            || dataOffset is null)
        {
            return null;
        }

        var bytesPerSecond = sampleRate.Value * channels.Value * (bitsPerSample.Value / 8.0);
        var declaredDataSize = dataSize is > 0
            ? (long)dataSize.Value
            : Math.Max(0, stream.Length - dataOffset.Value);

        return new WavAudioInfo(dataOffset.Value, declaredDataSize, bytesPerSecond);
    }

    private sealed record WavAudioInfo(long DataOffset, long DataSize, double BytesPerSecond);
}
