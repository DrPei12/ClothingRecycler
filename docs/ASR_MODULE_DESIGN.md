# ASR Module Design

This document describes the ASR (Automatic Speech Recognition) module used by the AI (Beta) workflow. The module converts user voice input into text, then passes normalized text to the existing Agent workflow.

## Position In The System

```text
User Voice Input
-> Audio Capture / Audio File Upload
-> AiSpeechRecognitionService
-> IAudioTranscriptionProvider adapter
-> AsrTextPostProcessor
-> Agent input text
-> AiWorkflowAgentService
-> LLM provider and workflow tools
-> Agent response / order confirmation
```

The ASR module only owns audio-to-text. It does not decide whether an order should be created, which tool should run, or how the Agent memory works.

## Directory Structure

```text
Models/
  AsrModels.cs

Services/Ai/
  IAudioTranscriptionProvider.cs
  AiSpeechRecognitionService.cs
  AsrTextPostProcessor.cs
  UltravoxLocalAsrProvider.cs

Pages/
  AiAssistantPage.xaml
  AiAssistantPage.xaml.cs

ViewModels/
  AiAssistantViewModel.cs

ClothingRecycler.Desktop.Tests/
  AiSpeechRecognitionServiceTests.cs
```

Future providers should be added under `Services/Ai/` as independent adapters, for example `OpenAiWhisperAsrProvider`, `WhisperCppAsrProvider`, or `AzureSpeechAsrProvider`.

## Core Interface

```csharp
public interface IAudioTranscriptionProvider
{
    AsrProviderKind Kind { get; }

    string DisplayName { get; }

    IReadOnlySet<string> SupportedFileExtensions { get; }

    Task<AsrProviderProbeResult> ProbeAsync(CancellationToken cancellationToken = default);

    Task<TranscriptionResult> TranscribeAsync(
        AudioTranscriptionInput input,
        CancellationToken cancellationToken = default);
}
```

`AiSpeechRecognitionService` selects the configured provider, validates the audio input, runs the adapter, normalizes text, and returns a structured result.

## Result Model

```csharp
public sealed record TranscriptionResult
{
    public bool Succeeded { get; init; }
    public string Text { get; init; } = string.Empty;
    public string AgentInputText { get; init; } = string.Empty;
    public string Language { get; init; } = "zh-CN";
    public double? Confidence { get; init; }
    public TimeSpan? Duration { get; init; }
    public IReadOnlyList<TranscriptionSegment> Segments { get; init; } = [];
    public AsrErrorCode ErrorCode { get; init; } = AsrErrorCode.None;
    public string? Error { get; init; }
    public AsrProviderKind Provider { get; init; }
}
```

The Agent should consume `AgentInputText`, not raw ASR output. This keeps provider-specific formatting away from business workflow code.

## Provider Adapter Strategy

The business UI and Agent never call a concrete ASR service directly.

Current default provider:

```text
UltravoxLocalAsrProvider
-> wraps HuggingFaceUltravoxPythonProvider
-> supports local 16-bit PCM WAV transcription
```

Planned provider adapters:

```text
WhisperCppLocalAsrProvider
-> local executable or native library

FasterWhisperLocalAsrProvider
-> local Python runtime with faster-whisper

OpenAiWhisperAsrProvider
-> cloud API with API key from config or environment

AzureSpeechAsrProvider
-> Azure Speech SDK or REST API

BrowserWebSpeechAsrProvider
-> browser-only implementation if a web frontend is added
```

## Audio Input Handling

Supported input types:

```text
MicrophoneRecording
UploadedFile
```

Current minimum runnable version:

```text
Format: .wav
Encoding: 16-bit PCM
Recommended sample rate: 16000 Hz
Recommended channels: mono
```

The microphone recorder already creates a WAV file for the ASR service. File upload currently accepts WAV only, so unsupported formats fail early with a clear error. Format conversion is intentionally left behind an extension point instead of being mixed into Agent logic.

Validation handled by `AiSpeechRecognitionService`:

```text
missing file
empty file
unsupported extension
file too large
audio too short
audio too long
silent 16-bit PCM WAV
provider timeout
provider recognition failure
empty transcript
```

Long audio is currently rejected by duration limit. A future `IAudioPreprocessor` can split long audio into chunks and merge segment results.

## Text Postprocessing

`AsrTextPostProcessor.NormalizeForAgent` currently handles:

```text
extracting text from simple JSON provider responses
removing markdown code fences
removing known transcription prefixes
collapsing extra whitespace
removing unnecessary CJK / number / unit spacing
trimming wrapping quotes
```

Reserved extension points:

```text
punctuation restoration
Chinese / English mixed text normalization
Simplified / Traditional Chinese conversion
sensitive word detection
command word detection
domain vocabulary replacement
```

## Configuration

Runtime config is read from `AppUiSettingsService`. Local settings are stored outside the repository:

```text
%LOCALAPPDATA%\ClothingRecycler\ui-settings.json
```

Environment variable overrides:

```text
CLOTHING_RECYCLER_ASR_PROVIDER
CLOTHING_RECYCLER_ASR_LANGUAGE
CLOTHING_RECYCLER_ASR_MAX_AUDIO_MB
CLOTHING_RECYCLER_ASR_MIN_SECONDS
CLOTHING_RECYCLER_ASR_MAX_SECONDS
CLOTHING_RECYCLER_ASR_TIMEOUT_SECONDS
```

Default values:

```text
provider: HuggingFaceUltravoxLocal
language: zh-CN
max audio size: 25 MB
min duration: 0.6 seconds
max duration: 300 seconds
timeout: 90 seconds
```

## Agent Call Chain Example

```csharp
var transcription = await speechRecognitionService.TranscribeAudioFileAsync(
    audioPath,
    AudioInputKind.UploadedFile,
    cancellationToken);

if (!transcription.Succeeded)
{
    // Show structured ASR error to the user.
    return;
}

await aiAssistantViewModel.RunPromptWorkflowAsync(transcription.AgentInputText);
```

The Agent still receives normal text. From the Agent perspective, voice input and typed input are equivalent after ASR normalization.

## Tests

Current test coverage:

```text
valid WAV returns normalized Agent input
silent WAV returns NoSpeechDetected
too-short WAV returns AudioTooShort
unsupported extension returns UnsupportedFormat
empty provider text returns EmptyTranscript
```

The tests use a stub ASR provider, so they verify the module contract without requiring a real model download or network access.

## Extension Points

Next practical steps:

```text
add IAudioPreprocessor for mp3/m4a conversion and WAV normalization
add long-audio chunking and segment merge
add OpenAI Whisper or DeepSeek-compatible cloud ASR provider
add provider selection UI
add ASR confidence display in the Agent step log
add structured command-word detection before Agent execution
add Android API-mode ASR adapter with the same result model
```

