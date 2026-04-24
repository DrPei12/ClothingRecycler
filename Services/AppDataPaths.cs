namespace ClothingRecycler.Desktop.Services
{
    public static class AppDataPaths
    {
        private const string AiModelDirectoryEnvVar = "CLOTHING_RECYCLER_AI_MODEL_DIRECTORY";

        public static string? RootDirectoryOverride { get; set; }

        public static string? AiModelDirectoryOverride { get; set; }

        public static string RootDirectory =>
            string.IsNullOrWhiteSpace(RootDirectoryOverride)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ClothingRecycler")
                : RootDirectoryOverride;

        public static string DatabasePath => Path.Combine(RootDirectory, "clothingrecycler.db");

        public static string BackupDirectory => Path.Combine(RootDirectory, "backups");

        public static string ExportDirectory => Path.Combine(RootDirectory, "exports");

        public static string LogDirectory => Path.Combine(RootDirectory, "logs");

        public static string VoiceInputDirectory => Path.Combine(RootDirectory, "voice-inputs");

        public static string AiModelDirectory
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(AiModelDirectoryOverride))
                {
                    return AiModelDirectoryOverride;
                }

                var configured = Environment.GetEnvironmentVariable(AiModelDirectoryEnvVar);
                if (!string.IsNullOrWhiteSpace(configured))
                {
                    return Path.GetFullPath(configured.Trim());
                }

                var workspaceArtifactsDirectory = TryResolveWorkspaceArtifactsAiModelDirectory();
                return workspaceArtifactsDirectory ?? Path.Combine(RootDirectory, "AiModels");
            }
        }

        public static string UiSettingsPath => Path.Combine(RootDirectory, "ui-settings.json");

        public static void EnsureDirectories()
        {
            Directory.CreateDirectory(RootDirectory);
            Directory.CreateDirectory(BackupDirectory);
            Directory.CreateDirectory(ExportDirectory);
            Directory.CreateDirectory(LogDirectory);
            Directory.CreateDirectory(VoiceInputDirectory);
            Directory.CreateDirectory(AiModelDirectory);
        }

        private static string? TryResolveWorkspaceArtifactsAiModelDirectory()
        {
            foreach (var candidateRoot in EnumerateWorkspaceRootCandidates())
            {
                if (!Directory.Exists(candidateRoot))
                {
                    continue;
                }

                var projectFilePath = Path.Combine(candidateRoot, "ClothingRecycler.Desktop.csproj");
                if (!File.Exists(projectFilePath))
                {
                    continue;
                }

                return Path.Combine(candidateRoot, "artifacts", "ai-models");
            }

            return null;
        }

        private static IEnumerable<string> EnumerateWorkspaceRootCandidates()
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var seed in new[] { Directory.GetCurrentDirectory(), AppContext.BaseDirectory })
            {
                if (string.IsNullOrWhiteSpace(seed))
                {
                    continue;
                }

                var directory = new DirectoryInfo(Path.GetFullPath(seed));
                while (directory is not null)
                {
                    if (seen.Add(directory.FullName))
                    {
                        yield return directory.FullName;
                    }

                    directory = directory.Parent;
                }
            }
        }
    }
}
