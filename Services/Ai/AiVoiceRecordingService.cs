using Windows.Media.Capture;
using Windows.Media.MediaProperties;
using Windows.Storage;

namespace ClothingRecycler.Desktop.Services;

public sealed class AiVoiceRecordingService : IDisposable
{
    private MediaCapture? _mediaCapture;
    private StorageFile? _recordingFile;

    public bool IsRecording { get; private set; }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (IsRecording)
        {
            return;
        }

        Directory.CreateDirectory(AppDataPaths.VoiceInputDirectory);

        var folder = await StorageFolder.GetFolderFromPathAsync(AppDataPaths.VoiceInputDirectory);
        var fileName = $"ai-voice-{DateTime.Now:yyyyMMdd-HHmmss}.wav";
        _recordingFile = await folder.CreateFileAsync(fileName, CreationCollisionOption.GenerateUniqueName);

        _mediaCapture = new MediaCapture();
        var settings = new MediaCaptureInitializationSettings
        {
            StreamingCaptureMode = StreamingCaptureMode.Audio
        };
        await _mediaCapture.InitializeAsync(settings).AsTask(cancellationToken);

        var profile = MediaEncodingProfile.CreateWav(AudioEncodingQuality.Medium);
        profile.Audio = AudioEncodingProperties.CreatePcm(16000, 1, 16);

        await _mediaCapture.StartRecordToStorageFileAsync(profile, _recordingFile).AsTask(cancellationToken);
        IsRecording = true;
    }

    public async Task<string> StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRecording || _mediaCapture is null || _recordingFile is null)
        {
            throw new InvalidOperationException("Voice recording is not active.");
        }

        try
        {
            await _mediaCapture.StopRecordAsync().AsTask(cancellationToken);
            return _recordingFile.Path;
        }
        finally
        {
            IsRecording = false;
            DisposeMediaCapture();
        }
    }

    public async Task CancelAsync()
    {
        if (!IsRecording || _mediaCapture is null)
        {
            DisposeMediaCapture();
            return;
        }

        try
        {
            await _mediaCapture.StopRecordAsync();
        }
        catch
        {
            // Cancellation is best-effort; callers only need recording resources released.
        }
        finally
        {
            IsRecording = false;
            DisposeMediaCapture();
        }
    }

    public void Dispose()
    {
        DisposeMediaCapture();
    }

    private void DisposeMediaCapture()
    {
        _mediaCapture?.Dispose();
        _mediaCapture = null;
        _recordingFile = null;
    }
}
