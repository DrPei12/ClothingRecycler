using System.Text;

namespace ClothingRecycler.Desktop.Services
{
    public sealed class AppLogger
    {
        private readonly SemaphoreSlim _writeLock = new(1, 1);

        public string LogDirectory => AppDataPaths.LogDirectory;

        public string CurrentLogFilePath => Path.Combine(LogDirectory, $"app-{DateTime.Now:yyyyMMdd}.log");

        public async Task InitializeAsync()
        {
            AppDataPaths.EnsureDirectories();
            await CleanupOldLogsAsync();
            await LogInfoAsync("应用日志服务已初始化。");
        }

        public Task LogInfoAsync(string message) => WriteEntryAsync("INFO", message);

        public Task LogWarningAsync(string message) => WriteEntryAsync("WARN", message);

        public Task LogErrorAsync(string message, Exception? exception = null) => WriteEntryAsync("ERROR", message, exception);

        public async Task<string> LogFatalAsync(string message, Exception exception)
        {
            AppDataPaths.EnsureDirectories();
            await WriteEntryAsync("FATAL", message, exception);

            var crashPath = Path.Combine(LogDirectory, $"crash-{DateTime.Now:yyyyMMdd-HHmmssfff}.log");
            var crashContent = BuildEntry("FATAL", message, exception);

            await _writeLock.WaitAsync();
            try
            {
                await File.WriteAllTextAsync(crashPath, crashContent, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }
            finally
            {
                _writeLock.Release();
            }

            return crashPath;
        }

        public string LogFatal(string message, Exception exception)
        {
            try
            {
                return LogFatalAsync(message, exception).GetAwaiter().GetResult();
            }
            catch
            {
                return string.Empty;
            }
        }

        private async Task WriteEntryAsync(string level, string message, Exception? exception = null)
        {
            AppDataPaths.EnsureDirectories();

            var entry = BuildEntry(level, message, exception);

            await _writeLock.WaitAsync();
            try
            {
                await File.AppendAllTextAsync(
                    CurrentLogFilePath,
                    entry + Environment.NewLine,
                    new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            }
            finally
            {
                _writeLock.Release();
            }
        }

        private static string BuildEntry(string level, string message, Exception? exception)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"[{DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss.fff zzz}] [{level}] {message}");

            if (exception is not null)
            {
                builder.AppendLine(exception.ToString());
            }

            return builder.ToString().TrimEnd();
        }

        private Task CleanupOldLogsAsync()
        {
            return Task.Run(() =>
            {
                if (!Directory.Exists(LogDirectory))
                {
                    return;
                }

                var threshold = DateTime.Now.AddDays(-30);
                foreach (var file in Directory.GetFiles(LogDirectory, "*.log"))
                {
                    try
                    {
                        var info = new FileInfo(file);
                        if (info.LastWriteTime < threshold)
                        {
                            info.Delete();
                        }
                    }
                    catch
                    {
                        // 忽略日志清理失败，避免影响主流程。
                    }
                }
            });
        }
    }
}
