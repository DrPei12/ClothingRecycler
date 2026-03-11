namespace ClothingRecycler.Desktop.Services
{
    public static class AppDataPaths
    {
        public static string? RootDirectoryOverride { get; set; }

        public static string RootDirectory =>
            string.IsNullOrWhiteSpace(RootDirectoryOverride)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClothingRecycler")
                : RootDirectoryOverride;

        public static string DatabasePath => Path.Combine(RootDirectory, "clothingrecycler.db");

        public static string BackupDirectory => Path.Combine(RootDirectory, "backups");

        public static string ExportDirectory => Path.Combine(RootDirectory, "exports");

        public static string LogDirectory => Path.Combine(RootDirectory, "logs");

        public static string UiSettingsPath => Path.Combine(RootDirectory, "ui-settings.json");

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(RootDirectory);
            Directory.CreateDirectory(BackupDirectory);
            Directory.CreateDirectory(ExportDirectory);
            Directory.CreateDirectory(LogDirectory);
        }
    }
}
