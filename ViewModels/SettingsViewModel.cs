namespace ClothingRecycler.Desktop.ViewModels
{
    public sealed class SettingsViewModel : ViewModelBase
    {
        private readonly LocalDatabaseService _databaseService;
        private readonly AppLogger _logger;
        private readonly AppUiSettingsService _uiSettingsService;
        private string _activeCategoryCountText = "0";
        private string _archivedCategoryCountText = "0";
        private string _lowStockCategoryCountText = "0";
        private string _consistencyStatusText = "未运行";
        private string _lastConsistencyCheckText = "尚未执行账本一致性检查。";
        private FontSizeOptionModel? _selectedFontSizeOption;

        public SettingsViewModel(
            LocalDatabaseService databaseService,
            AppLogger logger,
            AppUiSettingsService uiSettingsService)
        {
            _databaseService = databaseService;
            _logger = logger;
            _uiSettingsService = uiSettingsService;
            Title = "\u8BBE\u7F6E";

            FontSizeOptions.Add(new FontSizeOptionModel(AppFontSizePreset.Small, "小", "更紧凑，适合屏幕较小或希望一页显示更多内容。"));
            FontSizeOptions.Add(new FontSizeOptionModel(AppFontSizePreset.Standard, "标准", "推荐默认大小，兼顾信息密度和易读性。"));
            FontSizeOptions.Add(new FontSizeOptionModel(AppFontSizePreset.Large, "大", "更易读，适合远距离查看或视力负担较大的场景。"));

            _selectedFontSizeOption = FontSizeOptions.FirstOrDefault(option => option.Preset == _uiSettingsService.CurrentFontSizePreset)
                ?? FontSizeOptions[1];
        }

        public ObservableCollection<CategoryManagementItemModel> CategoryItems { get; } = [];

        public ObservableCollection<FontSizeOptionModel> FontSizeOptions { get; } = [];

        public string DatabasePath => _databaseService.DatabasePath;

        public string BackupDirectory => _databaseService.BackupDirectory;

        public string ExportDirectory => _databaseService.ExportDirectory;

        public string LogDirectory => _logger.LogDirectory;

        public string SchemaVersionText => $"v{_databaseService.CurrentSchemaVersion}";

        public string AppVersionText => AppReleaseInfo.VersionText;

        public string PackagingText => AppReleaseInfo.PackagingText;

        public string UiSettingsPath => _uiSettingsService.SettingsFilePath;

        public string ActiveCategoryCountText
        {
            get => _activeCategoryCountText;
            private set => SetProperty(ref _activeCategoryCountText, value);
        }

        public string ArchivedCategoryCountText
        {
            get => _archivedCategoryCountText;
            private set => SetProperty(ref _archivedCategoryCountText, value);
        }

        public string LowStockCategoryCountText
        {
            get => _lowStockCategoryCountText;
            private set => SetProperty(ref _lowStockCategoryCountText, value);
        }

        public string ConsistencyStatusText
        {
            get => _consistencyStatusText;
            private set => SetProperty(ref _consistencyStatusText, value);
        }

        public string LastConsistencyCheckText
        {
            get => _lastConsistencyCheckText;
            private set => SetProperty(ref _lastConsistencyCheckText, value);
        }

        public FontSizeOptionModel? SelectedFontSizeOption
        {
            get => _selectedFontSizeOption;
            set
            {
                if (SetProperty(ref _selectedFontSizeOption, value) && value is not null)
                {
                    _ = ApplyFontSizeOptionAsync(value);
                }
            }
        }

        public Visibility EmptyStateVisibility => CategoryItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public async Task LoadAsync()
        {
            IsBusy = true;

            try
            {
                var items = await _databaseService.GetCategoryManagementItemsAsync();

                CategoryItems.Clear();
                foreach (var item in items)
                {
                    CategoryItems.Add(item);
                }

                ActiveCategoryCountText = items.Count(item => !item.IsArchived).ToString(CultureInfo.InvariantCulture);
                ArchivedCategoryCountText = items.Count(item => item.IsArchived).ToString(CultureInfo.InvariantCulture);
                LowStockCategoryCountText = items.Count(item => !item.IsArchived && item.Category.IsLowStock)
                    .ToString(CultureInfo.InvariantCulture);

                OnPropertyChanged(nameof(EmptyStateVisibility));
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task SaveCategoryAsync(CategoryModel category)
        {
            await _databaseService.SaveCategoryAsync(category);
            await LoadAsync();
        }

        public Task<string> CreateBackupAsync() => _databaseService.CreateBackupAsync();

        public Task<string?> RestoreDatabaseAsync(string sourcePath) => _databaseService.RestoreDatabaseAsync(sourcePath);

        public Task<string> ExportBusinessDataAsync() => _databaseService.ExportBusinessDataAsync();

        public Task<ImportPreviewModel> PreviewCategoriesImportAsync(string sourcePath) =>
            _databaseService.PreviewCategoriesImportAsync(sourcePath);

        public Task<ImportPreviewModel> PreviewCustomersImportAsync(string sourcePath) =>
            _databaseService.PreviewCustomersImportAsync(sourcePath);

        public async Task<ConsistencyCheckReportModel> RunConsistencyCheckAsync()
        {
            var report = await _databaseService.RunConsistencyCheckAsync();
            ConsistencyStatusText = report.IsHealthy ? "正常" : $"发现 {report.IssueCount} 项问题";
            LastConsistencyCheckText = report.IsHealthy
                ? $"最近一次：{report.GeneratedAtText}，未发现一致性问题。"
                : $"最近一次：{report.GeneratedAtText}，发现 {report.IssueCount} 项问题，请先核对报告。";

            return report;
        }

        public Task<ConsistencyRepairResultModel> RepairRepairableConsistencyIssuesAsync() =>
            _databaseService.RepairRepairableConsistencyIssuesAsync();

        public async Task<string> ImportCategoriesAsync(string sourcePath)
        {
            var message = await _databaseService.ImportCategoriesAsync(sourcePath);
            await LoadAsync();
            return message;
        }

        public async Task<string> ImportCustomersAsync(string sourcePath)
        {
            var message = await _databaseService.ImportCustomersAsync(sourcePath);
            await LoadAsync();
            return message;
        }

        public async Task SetCategoryArchivedAsync(CategoryManagementItemModel item, bool isArchived)
        {
            await _databaseService.SetCategoryArchivedAsync(item.Id, isArchived);
            await LoadAsync();
        }

        public async Task DeleteCategoryAsync(CategoryManagementItemModel item)
        {
            await _databaseService.DeleteCategoryAsync(item.Id);
            await LoadAsync();
        }

        private async Task ApplyFontSizeOptionAsync(FontSizeOptionModel option)
        {
            if (_uiSettingsService.CurrentFontSizePreset == option.Preset)
            {
                return;
            }

            await _uiSettingsService.SetFontSizePresetAsync(option.Preset);
        }
    }
}
