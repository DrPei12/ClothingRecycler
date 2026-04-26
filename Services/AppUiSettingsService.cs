using System.Text;
using System.Text.Json;

namespace ClothingRecycler.Desktop.Services
{
    public sealed class AppUiSettingsService
    {
        private const string DeepSeekApiKeyEnvVar = "CLOTHING_RECYCLER_DEEPSEEK_API_KEY";
        private const string DeepSeekApiBaseUrlEnvVar = "CLOTHING_RECYCLER_DEEPSEEK_BASE_URL";
        private const string DeepSeekApiModelEnvVar = "CLOTHING_RECYCLER_DEEPSEEK_MODEL";
        private const string AsrProviderEnvVar = "CLOTHING_RECYCLER_ASR_PROVIDER";
        private const string AsrLanguageEnvVar = "CLOTHING_RECYCLER_ASR_LANGUAGE";
        private const string AsrMaxAudioMbEnvVar = "CLOTHING_RECYCLER_ASR_MAX_AUDIO_MB";
        private const string AsrMinDurationSecondsEnvVar = "CLOTHING_RECYCLER_ASR_MIN_SECONDS";
        private const string AsrMaxDurationSecondsEnvVar = "CLOTHING_RECYCLER_ASR_MAX_SECONDS";
        private const string AsrTimeoutSecondsEnvVar = "CLOTHING_RECYCLER_ASR_TIMEOUT_SECONDS";
        private const string DefaultDeepSeekApiBaseUrl = "https://api.deepseek.com";
        private const string DefaultDeepSeekApiModel = "deepseek-v4-flash";
        private const AsrProviderKind DefaultAsrProvider = AsrProviderKind.HuggingFaceUltravoxLocal;
        private const string DefaultAsrLanguage = "zh-CN";
        private const int DefaultAsrMaxAudioMegabytes = 25;
        private const double DefaultAsrMinDurationSeconds = 0.6;
        private const double DefaultAsrMaxDurationSeconds = 300;
        private const int DefaultAsrTimeoutSeconds = 90;

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        private UiSettingsDocument _document = new();

        public event EventHandler<AppFontSizePreset>? FontSizePresetChanged;

        public AppFontSizePreset CurrentFontSizePreset { get; private set; } = AppFontSizePreset.Standard;

        public string DeepSeekApiBaseUrl { get; private set; } = DefaultDeepSeekApiBaseUrl;

        public string DeepSeekApiModel { get; private set; } = DefaultDeepSeekApiModel;

        public string DeepSeekApiKey { get; private set; } = string.Empty;

        public AsrProviderKind AsrProviderKind { get; private set; } = DefaultAsrProvider;

        public string AsrLanguage { get; private set; } = DefaultAsrLanguage;

        public int AsrMaxAudioMegabytes { get; private set; } = DefaultAsrMaxAudioMegabytes;

        public double AsrMinDurationSeconds { get; private set; } = DefaultAsrMinDurationSeconds;

        public double AsrMaxDurationSeconds { get; private set; } = DefaultAsrMaxDurationSeconds;

        public int AsrTimeoutSeconds { get; private set; } = DefaultAsrTimeoutSeconds;

        public string SettingsFilePath => AppDataPaths.UiSettingsPath;

        public void Initialize()
        {
            AppDataPaths.EnsureDirectories();

            _document = new UiSettingsDocument();
            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    _document = JsonSerializer.Deserialize<UiSettingsDocument>(json, JsonOptions) ?? new UiSettingsDocument();
                }
                catch
                {
                    _document = new UiSettingsDocument();
                }
            }

            if (Enum.TryParse<AppFontSizePreset>(_document.FontSizePreset, ignoreCase: true, out var preset))
            {
                CurrentFontSizePreset = preset;
            }

            DeepSeekApiBaseUrl = NormalizeDeepSeekBaseUrl(_document.DeepSeekApiBaseUrl);
            DeepSeekApiModel = NormalizeDeepSeekModel(_document.DeepSeekApiModel);
            DeepSeekApiKey = ResolveStoredDeepSeekApiKey(_document);
            AsrProviderKind = NormalizeAsrProvider(_document.AsrProvider);
            AsrLanguage = NormalizeAsrLanguage(_document.AsrLanguage);
            AsrMaxAudioMegabytes = NormalizePositiveInt(_document.AsrMaxAudioMegabytes, DefaultAsrMaxAudioMegabytes);
            AsrMinDurationSeconds = NormalizePositiveDouble(_document.AsrMinDurationSeconds, DefaultAsrMinDurationSeconds);
            AsrMaxDurationSeconds = NormalizePositiveDouble(_document.AsrMaxDurationSeconds, DefaultAsrMaxDurationSeconds);
            AsrTimeoutSeconds = NormalizePositiveInt(_document.AsrTimeoutSeconds, DefaultAsrTimeoutSeconds);

            ApplyCurrentFontSize();
        }

        public DeepSeekApiRuntimeSettings GetDeepSeekApiRuntimeSettings()
        {
            var envApiKey = Environment.GetEnvironmentVariable(DeepSeekApiKeyEnvVar)?.Trim();
            var envBaseUrl = Environment.GetEnvironmentVariable(DeepSeekApiBaseUrlEnvVar)?.Trim();
            var envModel = Environment.GetEnvironmentVariable(DeepSeekApiModelEnvVar)?.Trim();

            var apiKey = string.IsNullOrWhiteSpace(envApiKey) ? DeepSeekApiKey : envApiKey;
            var baseUrl = NormalizeDeepSeekBaseUrl(string.IsNullOrWhiteSpace(envBaseUrl) ? DeepSeekApiBaseUrl : envBaseUrl);
            var model = NormalizeDeepSeekModel(string.IsNullOrWhiteSpace(envModel) ? DeepSeekApiModel : envModel);

            var sourceDescription = !string.IsNullOrWhiteSpace(envApiKey)
                ? $"environment variable {DeepSeekApiKeyEnvVar}"
                : "local UI settings";

            return new DeepSeekApiRuntimeSettings(
                BaseUrl: baseUrl,
                Model: model,
                ApiKey: apiKey,
                IsConfigured: !string.IsNullOrWhiteSpace(apiKey),
                SourceDescription: sourceDescription);
        }

        public AsrRuntimeSettings GetAsrRuntimeSettings()
        {
            var envProvider = Environment.GetEnvironmentVariable(AsrProviderEnvVar)?.Trim();
            var envLanguage = Environment.GetEnvironmentVariable(AsrLanguageEnvVar)?.Trim();
            var envMaxAudioMb = Environment.GetEnvironmentVariable(AsrMaxAudioMbEnvVar)?.Trim();
            var envMinSeconds = Environment.GetEnvironmentVariable(AsrMinDurationSecondsEnvVar)?.Trim();
            var envMaxSeconds = Environment.GetEnvironmentVariable(AsrMaxDurationSecondsEnvVar)?.Trim();
            var envTimeoutSeconds = Environment.GetEnvironmentVariable(AsrTimeoutSecondsEnvVar)?.Trim();

            var provider = string.IsNullOrWhiteSpace(envProvider)
                ? AsrProviderKind
                : NormalizeAsrProvider(envProvider);
            var language = NormalizeAsrLanguage(string.IsNullOrWhiteSpace(envLanguage) ? AsrLanguage : envLanguage);
            var maxAudioMegabytes = NormalizePositiveInt(envMaxAudioMb, AsrMaxAudioMegabytes);
            var minDurationSeconds = NormalizePositiveDouble(envMinSeconds, AsrMinDurationSeconds);
            var maxDurationSeconds = NormalizePositiveDouble(envMaxSeconds, AsrMaxDurationSeconds);
            var timeoutSeconds = NormalizePositiveInt(envTimeoutSeconds, AsrTimeoutSeconds);

            if (minDurationSeconds > maxDurationSeconds)
            {
                minDurationSeconds = DefaultAsrMinDurationSeconds;
                maxDurationSeconds = DefaultAsrMaxDurationSeconds;
            }

            return new AsrRuntimeSettings(
                ProviderKind: provider,
                Language: language,
                MaxAudioBytes: maxAudioMegabytes * 1024L * 1024L,
                MinDuration: TimeSpan.FromSeconds(minDurationSeconds),
                MaxDuration: TimeSpan.FromSeconds(maxDurationSeconds),
                Timeout: TimeSpan.FromSeconds(timeoutSeconds));
        }

        public async Task SetFontSizePresetAsync(AppFontSizePreset preset)
        {
            CurrentFontSizePreset = preset;
            _document.FontSizePreset = preset.ToString();
            ApplyCurrentFontSize();
            FontSizePresetChanged?.Invoke(this, CurrentFontSizePreset);

            await SaveDocumentAsync();
        }

        public async Task SaveDeepSeekApiSettingsAsync(string baseUrl, string model, string apiKey)
        {
            DeepSeekApiBaseUrl = NormalizeDeepSeekBaseUrl(baseUrl);
            DeepSeekApiModel = NormalizeDeepSeekModel(model);
            DeepSeekApiKey = apiKey?.Trim() ?? string.Empty;

            _document.DeepSeekApiBaseUrl = DeepSeekApiBaseUrl;
            _document.DeepSeekApiModel = DeepSeekApiModel;
            _document.DeepSeekApiKey = DeepSeekApiKey;

            await SaveDocumentAsync();
        }

        public void ApplyCurrentFontSize()
        {
            var tokens = GetTokens(CurrentFontSizePreset);
            var resources = Application.Current.Resources;

            resources["AppBodyFontSize"] = tokens.BodyFontSize;
            resources["AppTableHeaderFontSize"] = tokens.TableHeaderFontSize;
            resources["AppBadgeFontSize"] = tokens.BadgeFontSize;
            resources["AppCompactListTitleFontSize"] = tokens.CompactListTitleFontSize;
            resources["AppListTitleFontSize"] = tokens.ListTitleFontSize;
            resources["AppSectionTitleFontSize"] = tokens.SectionTitleFontSize;
            resources["AppPageTitleFontSize"] = tokens.PageTitleFontSize;
            resources["AppMetricFontSize"] = tokens.MetricFontSize;
            resources["AppHeroMetricFontSize"] = tokens.HeroMetricFontSize;
            resources["AppDetailMetricFontSize"] = tokens.DetailMetricFontSize;
        }

        private async Task SaveDocumentAsync()
        {
            var json = JsonSerializer.Serialize(_document, JsonOptions);
            await File.WriteAllTextAsync(SettingsFilePath, json, Encoding.UTF8);
        }

        private static string ResolveStoredDeepSeekApiKey(UiSettingsDocument document)
        {
            return document.DeepSeekApiKey?.Trim() ?? string.Empty;
        }

        private static string NormalizeDeepSeekBaseUrl(string? value)
        {
            var candidate = string.IsNullOrWhiteSpace(value) ? DefaultDeepSeekApiBaseUrl : value.Trim();
            if (!candidate.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
                && !candidate.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                candidate = "https://" + candidate;
            }

            return Uri.TryCreate(candidate, UriKind.Absolute, out var uri)
                ? uri.ToString().TrimEnd('/')
                : DefaultDeepSeekApiBaseUrl;
        }

        private static string NormalizeDeepSeekModel(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? DefaultDeepSeekApiModel
                : value.Trim();
        }

        private static AsrProviderKind NormalizeAsrProvider(string? value)
        {
            return Enum.TryParse<AsrProviderKind>(value, ignoreCase: true, out var provider)
                ? provider
                : DefaultAsrProvider;
        }

        private static string NormalizeAsrLanguage(string? value)
        {
            return string.IsNullOrWhiteSpace(value)
                ? DefaultAsrLanguage
                : value.Trim();
        }

        private static int NormalizePositiveInt(string? value, int fallback)
        {
            return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? parsed
                : fallback;
        }

        private static double NormalizePositiveDouble(string? value, double fallback)
        {
            return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
                ? parsed
                : fallback;
        }

        private static FontSizeTokens GetTokens(AppFontSizePreset preset) =>
            preset switch
            {
                AppFontSizePreset.Small => new FontSizeTokens(
                    BodyFontSize: 13,
                    TableHeaderFontSize: 11,
                    BadgeFontSize: 11,
                    CompactListTitleFontSize: 14,
                    ListTitleFontSize: 15,
                    SectionTitleFontSize: 17,
                    PageTitleFontSize: 26,
                    MetricFontSize: 22,
                    HeroMetricFontSize: 24,
                    DetailMetricFontSize: 18),
                AppFontSizePreset.Large => new FontSizeTokens(
                    BodyFontSize: 16,
                    TableHeaderFontSize: 14,
                    BadgeFontSize: 14,
                    CompactListTitleFontSize: 17,
                    ListTitleFontSize: 18,
                    SectionTitleFontSize: 20,
                    PageTitleFontSize: 31,
                    MetricFontSize: 27,
                    HeroMetricFontSize: 30,
                    DetailMetricFontSize: 23),
                _ => new FontSizeTokens(
                    BodyFontSize: 14,
                    TableHeaderFontSize: 12,
                    BadgeFontSize: 12,
                    CompactListTitleFontSize: 15,
                    ListTitleFontSize: 16,
                    SectionTitleFontSize: 18,
                    PageTitleFontSize: 28,
                    MetricFontSize: 24,
                    HeroMetricFontSize: 26,
                    DetailMetricFontSize: 20)
            };

        public sealed record DeepSeekApiRuntimeSettings(
            string BaseUrl,
            string Model,
            string ApiKey,
            bool IsConfigured,
            string SourceDescription);

        public sealed record AsrRuntimeSettings(
            AsrProviderKind ProviderKind,
            string Language,
            long MaxAudioBytes,
            TimeSpan MinDuration,
            TimeSpan MaxDuration,
            TimeSpan Timeout);

        private sealed class UiSettingsDocument
        {
            public string FontSizePreset { get; set; } = AppFontSizePreset.Standard.ToString();

            public string DeepSeekApiBaseUrl { get; set; } = DefaultDeepSeekApiBaseUrl;

            public string DeepSeekApiModel { get; set; } = DefaultDeepSeekApiModel;

            public string DeepSeekApiKey { get; set; } = string.Empty;

            public string AsrProvider { get; set; } = DefaultAsrProvider.ToString();

            public string AsrLanguage { get; set; } = DefaultAsrLanguage;

            public string AsrMaxAudioMegabytes { get; set; } = DefaultAsrMaxAudioMegabytes.ToString(CultureInfo.InvariantCulture);

            public string AsrMinDurationSeconds { get; set; } = DefaultAsrMinDurationSeconds.ToString(CultureInfo.InvariantCulture);

            public string AsrMaxDurationSeconds { get; set; } = DefaultAsrMaxDurationSeconds.ToString(CultureInfo.InvariantCulture);

            public string AsrTimeoutSeconds { get; set; } = DefaultAsrTimeoutSeconds.ToString(CultureInfo.InvariantCulture);
        }

        private sealed record FontSizeTokens(
            double BodyFontSize,
            double TableHeaderFontSize,
            double BadgeFontSize,
            double CompactListTitleFontSize,
            double ListTitleFontSize,
            double SectionTitleFontSize,
            double PageTitleFontSize,
            double MetricFontSize,
            double HeroMetricFontSize,
            double DetailMetricFontSize);
    }
}
