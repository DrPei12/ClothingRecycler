using System.Diagnostics;

using Windows.Storage.Pickers;

using WinRT.Interop;

namespace ClothingRecycler.Desktop.Pages
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsPage()
        {
            ViewModel = App.GetService<SettingsViewModel>();
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public SettingsViewModel ViewModel { get; }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private async void OnBackupDatabaseClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var backupPath = await ViewModel.CreateBackupAsync();
                await ShowMessageAsync("\u5907\u4EFD\u5DF2\u5B8C\u6210", $"\u5907\u4EFD\u6587\u4EF6\u5DF2\u4FDD\u5B58\u5230\uff1A\n{backupPath}");
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u5907\u4EFD\u5931\u8D25", ex.Message);
            }
        }

        private async void OnRestoreDatabaseClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var picker = new FileOpenPicker();
                picker.FileTypeFilter.Add(".db");
                picker.FileTypeFilter.Add(".sqlite");
                picker.FileTypeFilter.Add(".bak");
                InitializeWithWindow.Initialize(picker, GetWindowHandle());

                var file = await picker.PickSingleFileAsync();
                if (file is null)
                {
                    return;
                }

                var confirmDialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = "\u786E\u8BA4\u6062\u590D\u5907\u4EFD",
                    PrimaryButtonText = "\u6062\u590D\u5E76\u91CD\u542F",
                    CloseButtonText = "\u53D6\u6D88",
                    DefaultButton = ContentDialogButton.Primary,
                    Content = $"\u5C06\u4F7F\u7528\u4EE5\u4E0B\u5907\u4EFD\u6587\u4EF6\u8986\u76D6\u5F53\u524D\u6570\u636E\uff1A\n{file.Path}\n\n\u6062\u590D\u524D\u4F1A\u5148\u81EA\u52A8\u521B\u5EFA\u4E00\u4EFD\u5F53\u524D\u6570\u636E\u7684\u6062\u590D\u70B9\u3002"
                };

                if (await confirmDialog.ShowAsync() != ContentDialogResult.Primary)
                {
                    return;
                }

                var restorePointPath = await ViewModel.RestoreDatabaseAsync(file.Path);
                await ShowMessageAsync(
                    "\u6062\u590D\u5B8C\u6210",
                    restorePointPath is null
                        ? "\u5907\u4EFD\u5DF2\u6062\u590D\u3002\u5E94\u7528\u5C06\u91CD\u542F\u4EE5\u52A0\u8F7D\u6700\u65B0\u6570\u636E\u3002"
                        : $"\u5907\u4EFD\u5DF2\u6062\u590D\u3002\n\u5F53\u524D\u6570\u636E\u7684\u6062\u590D\u70B9\u5DF2\u4FDD\u5B58\u5230\uff1A\n{restorePointPath}\n\n\u5E94\u7528\u5C06\u91CD\u542F\u4EE5\u52A0\u8F7D\u6700\u65B0\u6570\u636E\u3002");

                RestartApplication();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u6062\u590D\u5931\u8D25", ex.Message);
            }
        }

        private void OnOpenBackupDirectoryClick(object sender, RoutedEventArgs e)
        {
            OpenDirectory(ViewModel.BackupDirectory);
        }

        private async void OnExportBusinessDataClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var exportPath = await ViewModel.ExportBusinessDataAsync();
                await ShowMessageAsync("\u5BFC\u51FA\u5B8C\u6210", $"\u7ECF\u8425\u6570\u636E\u5DF2\u5BFC\u51FA\u5230\uff1A\n{exportPath}");
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u5BFC\u51FA\u5931\u8D25", ex.Message);
            }
        }

        private void OnOpenExportDirectoryClick(object sender, RoutedEventArgs e)
        {
            OpenDirectory(ViewModel.ExportDirectory);
        }

        private void OnOpenLogDirectoryClick(object sender, RoutedEventArgs e)
        {
            OpenDirectory(ViewModel.LogDirectory);
        }

        private async void OnRunConsistencyCheckClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var report = await ViewModel.RunConsistencyCheckAsync();
                var dialog = new ConsistencyCheckDialog(report, allowRepair: false)
                {
                    XamlRoot = XamlRoot
                };

                await dialog.ShowAsync();
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("检查失败", ex.Message);
            }
        }

        private async void OnImportCategoriesClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await RunImportFlowAsync(
                    previewLoader: ViewModel.PreviewCategoriesImportAsync,
                    importExecutor: ViewModel.ImportCategoriesAsync);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u5BFC\u5165\u5931\u8D25", ex.Message);
            }
        }

        private async void OnImportCustomersClick(object sender, RoutedEventArgs e)
        {
            try
            {
                await RunImportFlowAsync(
                    previewLoader: ViewModel.PreviewCustomersImportAsync,
                    importExecutor: ViewModel.ImportCustomersAsync);
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u5BFC\u5165\u5931\u8D25", ex.Message);
            }
        }

        private async void OnAddCategoryClick(object sender, RoutedEventArgs e)
        {
            try
            {
                var dialog = new CategoryEditorDialog
                {
                    XamlRoot = XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Result is not null)
                {
                    await ViewModel.SaveCategoryAsync(dialog.Result);
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u4FDD\u5B58\u5931\u8D25", ex.Message);
            }
        }

        private async void OnEditCategoryClick(object sender, RoutedEventArgs e)
        {
            if (sender is not AppBarButton button || button.Tag is not CategoryManagementItemModel item)
            {
                return;
            }

            try
            {
                var dialog = new CategoryEditorDialog(item.Category)
                {
                    XamlRoot = XamlRoot
                };

                if (await dialog.ShowAsync() == ContentDialogResult.Primary && dialog.Result is not null)
                {
                    await ViewModel.SaveCategoryAsync(dialog.Result);
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u4FDD\u5B58\u5931\u8D25", ex.Message);
            }
        }

        private async void OnArchiveCategoryClick(object sender, RoutedEventArgs e)
        {
            if (sender is not AppBarButton button || button.Tag is not CategoryManagementItemModel item)
            {
                return;
            }

            var isArchiving = !item.IsArchived;
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = isArchiving ? "\u5F52\u6863\u5206\u7C7B" : "\u542F\u7528\u5206\u7C7B",
                PrimaryButtonText = isArchiving ? "\u5F52\u6863" : "\u542F\u7528",
                CloseButtonText = "\u53D6\u6D88",
                DefaultButton = ContentDialogButton.Primary,
                Content = isArchiving
                    ? $"\u201C{item.Name}\u201D \u5F52\u6863\u540E\u4E0D\u4F1A\u518D\u51FA\u73B0\u5728\u5165\u5E93\u3001\u51FA\u5E93\u7684\u65B0\u5EFA\u8868\u5355\u91CC\uff0C\u4F46\u5386\u53F2\u6570\u636E\u4F1A\u4FDD\u7559\u3002"
                    : $"\u786E\u8BA4\u91CD\u65B0\u542F\u7528\u201C{item.Name}\u201D\u5417\uff1F\u542F\u7528\u540E\u5B83\u4F1A\u91CD\u65B0\u51FA\u73B0\u5728\u65B0\u5EFA\u4E1A\u52A1\u8868\u5355\u91CC\u3002"
            };

            try
            {
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    await ViewModel.SetCategoryArchivedAsync(item, isArchiving);
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u64CD\u4F5C\u5931\u8D25", ex.Message);
            }
        }

        private async void OnDeleteCategoryClick(object sender, RoutedEventArgs e)
        {
            if (sender is not AppBarButton button || button.Tag is not CategoryManagementItemModel item)
            {
                return;
            }

            if (!item.CanDelete)
            {
                var riskDialog = new ContentDialog
                {
                    XamlRoot = XamlRoot,
                    Title = "\u4E0D\u5EFA\u8BAE\u76F4\u63A5\u5220\u9664",
                    PrimaryButtonText = item.IsArchived ? "\u77E5\u9053\u4E86" : "\u6539\u4E3A\u5F52\u6863",
                    CloseButtonText = "\u53D6\u6D88",
                    DefaultButton = ContentDialogButton.Primary,
                    Content = new StackPanel
                    {
                        Spacing = 8,
                        Children =
                        {
                            new TextBlock
                            {
                                Text = $"\u201C{item.Name}\u201D \u8FD8\u6709\u5E93\u5B58\u6216\u5386\u53F2\u6D41\u6C34\uff0C\u76F4\u63A5\u5220\u9664\u4F1A\u7834\u574F\u53F0\u8D26\u3002",
                                TextWrapping = TextWrapping.WrapWholeWords
                            },
                            new TextBlock
                            {
                                Opacity = 0.72,
                                Text = $"\u5F53\u524D\u5E93\u5B58\uff1A{item.DisplayStockText}",
                                TextWrapping = TextWrapping.WrapWholeWords
                            },
                            new TextBlock
                            {
                                Opacity = 0.72,
                                Text = $"\u5386\u53F2\u6458\u8981\uff1A{item.RecordSummaryText}",
                                TextWrapping = TextWrapping.WrapWholeWords
                            }
                        }
                    }
                };

                try
                {
                    if (!item.IsArchived && await riskDialog.ShowAsync() == ContentDialogResult.Primary)
                    {
                        await ViewModel.SetCategoryArchivedAsync(item, isArchived: true);
                    }
                }
                catch (Exception ex)
                {
                    await ShowMessageAsync("\u64CD\u4F5C\u5931\u8D25", ex.Message);
                }

                return;
            }

            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = "\u5220\u9664\u5206\u7C7B",
                PrimaryButtonText = "\u5220\u9664",
                CloseButtonText = "\u53D6\u6D88",
                DefaultButton = ContentDialogButton.Close,
                Content = $"\u786E\u8BA4\u5220\u9664\u201C{item.Name}\u201D\u5417\uff1F\u8FD9\u4E2A\u64CD\u4F5C\u65E0\u6CD5\u64A4\u9500\u3002"
            };

            try
            {
                if (await dialog.ShowAsync() == ContentDialogResult.Primary)
                {
                    await ViewModel.DeleteCategoryAsync(item);
                }
            }
            catch (Exception ex)
            {
                await ShowMessageAsync("\u5220\u9664\u5931\u8D25", ex.Message);
            }
        }

        private async Task ShowMessageAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                XamlRoot = XamlRoot,
                Title = title,
                Content = message,
                CloseButtonText = "\u786E\u5B9A"
            };

            await dialog.ShowAsync();
        }

        private async Task RunImportFlowAsync(
            Func<string, Task<ImportPreviewModel>> previewLoader,
            Func<string, Task<string>> importExecutor)
        {
            var file = await PickCsvFileAsync();
            if (file is null)
            {
                return;
            }

            var preview = await previewLoader(file.Path);
            var dialog = new ImportPreviewDialog(preview)
            {
                XamlRoot = XamlRoot
            };

            if (await dialog.ShowAsync() != ContentDialogResult.Primary)
            {
                return;
            }

            var result = await importExecutor(file.Path);
            await ShowMessageAsync("\u5BFC\u5165\u5B8C\u6210", result);
        }

        private async Task<Windows.Storage.StorageFile?> PickCsvFileAsync()
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".csv");
            InitializeWithWindow.Initialize(picker, GetWindowHandle());
            return await picker.PickSingleFileAsync();
        }

        private static nint GetWindowHandle() => WindowNative.GetWindowHandle(App.GetService<MainWindow>());

        private static void OpenDirectory(string path)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{path}\"",
                UseShellExecute = true
            });
        }

        private static void RestartApplication()
        {
            if (!string.IsNullOrWhiteSpace(Environment.ProcessPath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath,
                    UseShellExecute = true
                });
            }

            Application.Current.Exit();
        }
    }
}
