using System.Text.Json;

namespace ClothingRecycler.Desktop.Services
{
    public sealed class AppUiSettingsService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public event EventHandler<AppFontSizePreset>? FontSizePresetChanged;

        public AppFontSizePreset CurrentFontSizePreset { get; private set; } = AppFontSizePreset.Standard;

        public string SettingsFilePath => AppDataPaths.UiSettingsPath;

        public void Initialize()
        {
            AppDataPaths.EnsureDirectories();

            if (File.Exists(SettingsFilePath))
            {
                try
                {
                    var json = File.ReadAllText(SettingsFilePath);
                    var settings = JsonSerializer.Deserialize<UiSettingsDocument>(json, JsonOptions);
                    if (Enum.TryParse<AppFontSizePreset>(settings?.FontSizePreset, ignoreCase: true, out var preset))
                    {
                        CurrentFontSizePreset = preset;
                    }
                }
                catch
                {
                    CurrentFontSizePreset = AppFontSizePreset.Standard;
                }
            }

            ApplyCurrentFontSize();
        }

        public async Task SetFontSizePresetAsync(AppFontSizePreset preset)
        {
            CurrentFontSizePreset = preset;
            ApplyCurrentFontSize();
            FontSizePresetChanged?.Invoke(this, CurrentFontSizePreset);

            var document = new UiSettingsDocument
            {
                FontSizePreset = preset.ToString()
            };

            var json = JsonSerializer.Serialize(document, JsonOptions);
            await File.WriteAllTextAsync(SettingsFilePath, json);
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

        private sealed class UiSettingsDocument
        {
            public string FontSizePreset { get; set; } = AppFontSizePreset.Standard.ToString();
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
