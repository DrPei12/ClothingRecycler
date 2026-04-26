using System.Runtime.InteropServices;
using System.Text;

namespace ClothingRecycler.Desktop.Services;

public sealed class AiVoiceRecordingService : IDisposable
{
    private const int WaveMapper = -1;
    private const int CallbackFunction = 0x00030000;
    private const int WimData = 0x3C0;
    private const ushort WaveFormatPcm = 1;
    private const int SampleRate = 16000;
    private const ushort Channels = 1;
    private const ushort BitsPerSample = 16;
    private const int BufferMilliseconds = 100;
    private const int BufferCount = 4;
    private const double PeakSpeechThreshold = 0.015;
    private const double RmsSpeechThreshold = 0.004;

    private readonly object _syncRoot = new();
    private readonly List<WaveBuffer> _buffers = [];
    private WaveInProc? _waveInProc;
    private IntPtr _waveInHandle;
    private FileStream? _recordingStream;
    private string? _recordingPath;
    private long _dataBytesWritten;
    private double _latestPeak;
    private double _latestRms;
    private double _latestLevelPercent;
    private bool _latestHasSpeech;

    public bool IsRecording { get; private set; }

    public string? CurrentRecordingPath => _recordingPath;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (IsRecording)
        {
            return Task.CompletedTask;
        }

        Directory.CreateDirectory(AppDataPaths.VoiceInputDirectory);
        _recordingPath = Path.Combine(AppDataPaths.VoiceInputDirectory, $"ai-voice-{DateTime.Now:yyyyMMdd-HHmmss}.wav");
        _recordingStream = new FileStream(_recordingPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        WriteWavHeader(_recordingStream, 0);

        try
        {
            _waveInProc = OnWaveInData;
            var format = CreateWaveFormat();
            var openResult = waveInOpen(out _waveInHandle, WaveMapper, ref format, _waveInProc, IntPtr.Zero, CallbackFunction);
            ThrowIfWaveError(openResult, "打开默认麦克风失败");

            PrepareBuffers();
            IsRecording = true;
            var startResult = waveInStart(_waveInHandle);
            ThrowIfWaveError(startResult, "启动麦克风录音失败");
        }
        catch
        {
            CleanupCapture(finalizeHeader: false);
            throw;
        }

        return Task.CompletedTask;
    }

    public Task<string> StopAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!IsRecording || string.IsNullOrWhiteSpace(_recordingPath))
        {
            throw new InvalidOperationException("Voice recording is not active.");
        }

        var path = _recordingPath;
        IsRecording = false;
        CleanupCapture(finalizeHeader: true);
        return Task.FromResult(path);
    }

    public Task<VoiceAudioLevelSnapshot> GetCurrentLevelAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        lock (_syncRoot)
        {
            if (!IsRecording)
            {
                return Task.FromResult(VoiceAudioLevelSnapshot.Unavailable("Voice recording is not active."));
            }

            var duration = SampleRate <= 0
                ? (TimeSpan?)null
                : TimeSpan.FromSeconds(_dataBytesWritten / (double)GetBytesPerSecond());

            return Task.FromResult(new VoiceAudioLevelSnapshot
            {
                IsAvailable = true,
                HasSpeech = _latestHasSpeech,
                Peak = _latestPeak,
                Rms = _latestRms,
                LevelPercent = _latestLevelPercent,
                Duration = duration,
                Detail = $"live waveIn; peak={_latestPeak:0.000}; rms={_latestRms:0.000}"
            });
        }
    }

    public Task CancelAsync()
    {
        IsRecording = false;
        CleanupCapture(finalizeHeader: false);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        IsRecording = false;
        CleanupCapture(finalizeHeader: false);
    }

    private void PrepareBuffers()
    {
        var bufferSize = GetBytesPerSecond() * BufferMilliseconds / 1000;
        bufferSize -= bufferSize % 2;
        bufferSize = Math.Max(bufferSize, 1024);

        for (var index = 0; index < BufferCount; index++)
        {
            var buffer = new WaveBuffer(bufferSize);
            _buffers.Add(buffer);
            ThrowIfWaveError(waveInPrepareHeader(_waveInHandle, buffer.HeaderPointer, Marshal.SizeOf<WaveHeader>()), "准备麦克风缓冲区失败");
            ThrowIfWaveError(waveInAddBuffer(_waveInHandle, buffer.HeaderPointer, Marshal.SizeOf<WaveHeader>()), "提交麦克风缓冲区失败");
        }
    }

    private void OnWaveInData(IntPtr hwi, int message, IntPtr instance, IntPtr headerPointer, IntPtr reserved)
    {
        if (message != WimData || headerPointer == IntPtr.Zero)
        {
            return;
        }

        var header = Marshal.PtrToStructure<WaveHeader>(headerPointer);
        var bytesRecorded = (int)Math.Min(header.dwBytesRecorded, header.dwBufferLength);
        if (bytesRecorded > 1 && header.lpData != IntPtr.Zero)
        {
            var buffer = new byte[bytesRecorded - (bytesRecorded % 2)];
            Marshal.Copy(header.lpData, buffer, 0, buffer.Length);
            ProcessRecordedBuffer(buffer);
        }

        if (IsRecording && _waveInHandle != IntPtr.Zero)
        {
            _ = waveInAddBuffer(_waveInHandle, headerPointer, Marshal.SizeOf<WaveHeader>());
        }
    }

    private void ProcessRecordedBuffer(byte[] buffer)
    {
        lock (_syncRoot)
        {
            _recordingStream?.Write(buffer, 0, buffer.Length);
            _dataBytesWritten += buffer.Length;

            var sampleCount = 0;
            var peak = 0;
            var sumSquares = 0.0;
            for (var index = 0; index + 1 < buffer.Length; index += 2)
            {
                var sample = BitConverter.ToInt16(buffer, index);
                var absolute = Math.Abs(sample);
                peak = Math.Max(peak, absolute);
                sumSquares += sample * (double)sample;
                sampleCount++;
            }

            if (sampleCount == 0)
            {
                _latestPeak = 0;
                _latestRms = 0;
                _latestLevelPercent = 0;
                _latestHasSpeech = false;
                return;
            }

            _latestPeak = Math.Clamp(peak / 32768.0, 0, 1);
            _latestRms = Math.Clamp(Math.Sqrt(sumSquares / sampleCount) / 32768.0, 0, 1);
            _latestLevelPercent = Math.Clamp(Math.Max(_latestPeak * 100, Math.Sqrt(_latestRms) * 100), 0, 100);
            _latestHasSpeech = _latestPeak >= PeakSpeechThreshold || _latestRms >= RmsSpeechThreshold;
        }
    }

    private void CleanupCapture(bool finalizeHeader)
    {
        if (_waveInHandle != IntPtr.Zero)
        {
            _ = waveInStop(_waveInHandle);
            _ = waveInReset(_waveInHandle);
        }

        foreach (var buffer in _buffers)
        {
            if (_waveInHandle != IntPtr.Zero)
            {
                _ = waveInUnprepareHeader(_waveInHandle, buffer.HeaderPointer, Marshal.SizeOf<WaveHeader>());
            }

            buffer.Dispose();
        }

        _buffers.Clear();

        if (_waveInHandle != IntPtr.Zero)
        {
            _ = waveInClose(_waveInHandle);
            _waveInHandle = IntPtr.Zero;
        }

        lock (_syncRoot)
        {
            if (_recordingStream is not null)
            {
                if (finalizeHeader)
                {
                    _recordingStream.Seek(0, SeekOrigin.Begin);
                    WriteWavHeader(_recordingStream, _dataBytesWritten);
                }

                _recordingStream.Dispose();
                _recordingStream = null;
            }

            _dataBytesWritten = 0;
            _latestPeak = 0;
            _latestRms = 0;
            _latestLevelPercent = 0;
            _latestHasSpeech = false;
        }

        _waveInProc = null;
        IsRecording = false;
    }

    private static WaveFormat CreateWaveFormat()
    {
        return new WaveFormat
        {
            wFormatTag = WaveFormatPcm,
            nChannels = Channels,
            nSamplesPerSec = (uint)SampleRate,
            nAvgBytesPerSec = (uint)GetBytesPerSecond(),
            nBlockAlign = (ushort)(Channels * BitsPerSample / 8),
            wBitsPerSample = BitsPerSample,
            cbSize = 0
        };
    }

    private static int GetBytesPerSecond()
    {
        return SampleRate * Channels * BitsPerSample / 8;
    }

    private static void WriteWavHeader(Stream stream, long dataSize)
    {
        using var writer = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write((uint)Math.Min(uint.MaxValue, 36 + dataSize));
        writer.Write(Encoding.ASCII.GetBytes("WAVE"));
        writer.Write(Encoding.ASCII.GetBytes("fmt "));
        writer.Write((uint)16);
        writer.Write(WaveFormatPcm);
        writer.Write(Channels);
        writer.Write((uint)SampleRate);
        writer.Write((uint)GetBytesPerSecond());
        writer.Write((ushort)(Channels * BitsPerSample / 8));
        writer.Write(BitsPerSample);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write((uint)Math.Min(uint.MaxValue, dataSize));
        stream.Flush();
    }

    private static void ThrowIfWaveError(int result, string action)
    {
        if (result == 0)
        {
            return;
        }

        throw new InvalidOperationException($"{action}。{GetWaveErrorText(result)}");
    }

    private static string GetWaveErrorText(int errorCode)
    {
        var builder = new StringBuilder(256);
        return waveInGetErrorText(errorCode, builder, builder.Capacity) == 0
            ? builder.ToString()
            : $"WinMM error {errorCode}";
    }

    [DllImport("winmm.dll")]
    private static extern int waveInOpen(
        out IntPtr waveInHandle,
        int deviceId,
        ref WaveFormat format,
        WaveInProc callback,
        IntPtr instance,
        int openFlags);

    [DllImport("winmm.dll")]
    private static extern int waveInPrepareHeader(IntPtr waveInHandle, IntPtr waveHeader, int waveHeaderSize);

    [DllImport("winmm.dll")]
    private static extern int waveInUnprepareHeader(IntPtr waveInHandle, IntPtr waveHeader, int waveHeaderSize);

    [DllImport("winmm.dll")]
    private static extern int waveInAddBuffer(IntPtr waveInHandle, IntPtr waveHeader, int waveHeaderSize);

    [DllImport("winmm.dll")]
    private static extern int waveInStart(IntPtr waveInHandle);

    [DllImport("winmm.dll")]
    private static extern int waveInStop(IntPtr waveInHandle);

    [DllImport("winmm.dll")]
    private static extern int waveInReset(IntPtr waveInHandle);

    [DllImport("winmm.dll")]
    private static extern int waveInClose(IntPtr waveInHandle);

    [DllImport("winmm.dll", CharSet = CharSet.Unicode)]
    private static extern int waveInGetErrorText(int errorCode, StringBuilder errorText, int errorTextSize);

    private delegate void WaveInProc(IntPtr waveInHandle, int message, IntPtr instance, IntPtr parameter1, IntPtr parameter2);

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveFormat
    {
        public ushort wFormatTag;
        public ushort nChannels;
        public uint nSamplesPerSec;
        public uint nAvgBytesPerSec;
        public ushort nBlockAlign;
        public ushort wBitsPerSample;
        public ushort cbSize;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WaveHeader
    {
        public IntPtr lpData;
        public uint dwBufferLength;
        public uint dwBytesRecorded;
        public IntPtr dwUser;
        public uint dwFlags;
        public uint dwLoops;
        public IntPtr lpNext;
        public IntPtr reserved;
    }

    private sealed class WaveBuffer : IDisposable
    {
        private readonly GCHandle _dataHandle;
        private readonly GCHandle _headerHandle;

        public WaveBuffer(int bufferSize)
        {
            Data = new byte[bufferSize];
            _dataHandle = GCHandle.Alloc(Data, GCHandleType.Pinned);
            Header = new WaveHeader
            {
                lpData = _dataHandle.AddrOfPinnedObject(),
                dwBufferLength = (uint)Data.Length
            };
            _headerHandle = GCHandle.Alloc(Header, GCHandleType.Pinned);
        }

        public byte[] Data { get; }

        public WaveHeader Header { get; }

        public IntPtr HeaderPointer => _headerHandle.AddrOfPinnedObject();

        public void Dispose()
        {
            if (_headerHandle.IsAllocated)
            {
                _headerHandle.Free();
            }

            if (_dataHandle.IsAllocated)
            {
                _dataHandle.Free();
            }
        }
    }
}
