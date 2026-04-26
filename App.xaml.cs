using System.Diagnostics;

namespace ClothingRecycler.Desktop
{
    public partial class App : Application
    {
        private readonly IServiceProvider _services;
        private readonly AppLogger _logger;
        private Window? _window;
        private int _isHandlingFatalException;

        public App()
        {
            InitializeComponent();
            _services = ConfigureServices();
            _logger = _services.GetRequiredService<AppLogger>();

            UnhandledException += OnUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        public static T GetService<T>() where T : notnull
        {
            if (Current is not App app)
            {
                throw new InvalidOperationException("\u5E94\u7528\u5C1A\u672A\u521D\u59CB\u5316\u3002");
            }

            return app._services.GetRequiredService<T>();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                var uiSettingsService = GetService<AppUiSettingsService>();
                uiSettingsService.Initialize();

                await _logger.InitializeAsync();
                await _logger.LogInfoAsync($"应用启动开始。版本 {AppReleaseInfo.VersionText}，模式：{AppReleaseInfo.PackagingText}。");
                await _logger.LogInfoAsync($"界面字体预设：{uiSettingsService.CurrentFontSizePreset}。");

                var databaseService = GetService<LocalDatabaseService>();
                await databaseService.InitializeAsync();
                await _logger.LogInfoAsync($"数据库初始化完成，当前架构版本 v{databaseService.CurrentSchemaVersion}。");

                _window ??= GetService<MainWindow>();
                _window.Activate();

                await _logger.LogInfoAsync("主窗口已激活。");
            }
            catch (Exception ex)
            {
                await HandleFatalExceptionAsync("应用启动失败。", ex);
            }
        }

        private static IServiceProvider ConfigureServices()
        {
            var services = new ServiceCollection();

            services.AddSingleton<AppLogger>();
            services.AddSingleton<AppUiSettingsService>();
            services.AddSingleton<LocalDatabaseService>();
            services.AddSingleton<ShellNavigationService>();
            services.AddSingleton<IAiBusinessContextSource, LocalDatabaseAiBusinessContextSource>();
            services.AddSingleton<AiDraftHandoffService>();
            services.AddSingleton<ILocalAiProvider, HuggingFaceUltravoxPythonProvider>();
            services.AddSingleton<ILocalAiProvider, OllamaLocalAiProvider>();
            services.AddSingleton<ILocalAiProvider, DeepSeekApiProvider>();
            services.AddSingleton<IAudioTranscriptionProvider, UltravoxLocalAsrProvider>();
            services.AddSingleton<AiSpeechRecognitionService>();
            services.AddSingleton<AiLiveSpeechRecognitionService>();
            services.AddSingleton<LocalAiDraftAgentService>();
            services.AddSingleton<AiDraftNormalizationToolService>();
            services.AddSingleton<AiWorkflowToolService>();
            services.AddSingleton<AiWorkflowAgentService>();
            services.AddSingleton<AiVoiceRecordingService>();
            services.AddSingleton<OrderExportService>();
            services.AddSingleton<MainWindow>();
            services.AddSingleton<AiAssistantViewModel>();

            services.AddTransient<DashboardViewModel>();
            services.AddTransient<InboundViewModel>();
            services.AddTransient<OutboundViewModel>();
            services.AddTransient<OrdersViewModel>();
            services.AddTransient<CustomersViewModel>();
            services.AddTransient<AnalyticsViewModel>();
            services.AddTransient<ForecastViewModel>();
            services.AddTransient<StockViewModel>();
            services.AddTransient<SettingsViewModel>();

            return services.BuildServiceProvider();
        }

        private async void OnUnhandledException(object sender, Microsoft.UI.Xaml.UnhandledExceptionEventArgs e)
        {
            e.Handled = true;
            await HandleFatalExceptionAsync("发生未处理的界面线程异常。", e.Exception);
        }

        private void OnCurrentDomainUnhandledException(object? sender, System.UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception
                ?? new InvalidOperationException("发生未知的应用程序域未处理异常。");

            _logger.LogFatal("发生未处理的 AppDomain 异常。", exception);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            _logger.LogFatal("发生未观察到的任务异常。", e.Exception);
            e.SetObserved();
        }

        private async Task HandleFatalExceptionAsync(string message, Exception exception)
        {
            if (Interlocked.Exchange(ref _isHandlingFatalException, 1) == 1)
            {
                return;
            }

            var crashLogPath = await _logger.LogFatalAsync(message, exception);

            try
            {
                _window ??= GetService<MainWindow>();
                _window.Activate();
                await Task.Delay(100);

                if (_window.Content is FrameworkElement root && root.XamlRoot is not null)
                {
                    var dialog = new ContentDialog
                    {
                        XamlRoot = root.XamlRoot,
                        Title = "应用遇到严重错误",
                        PrimaryButtonText = "打开日志目录",
                        CloseButtonText = "退出应用",
                        DefaultButton = ContentDialogButton.Close,
                        Content =
                            $"系统已记录崩溃日志，建议先保存当前工作。\n\n错误信息：{exception.Message}\n\n日志文件：\n{crashLogPath}"
                    };

                    if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = "explorer.exe",
                            Arguments = $"\"{AppDataPaths.LogDirectory}\"",
                            UseShellExecute = true
                        });
                    }
                }
            }
            catch
            {
                // 严重异常场景下优先保证日志落盘，忽略弹窗失败。
            }
            finally
            {
                Exit();
            }
        }
    }
}
