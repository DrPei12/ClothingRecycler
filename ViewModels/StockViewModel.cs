namespace ClothingRecycler.Desktop.ViewModels
{
    public sealed class StockViewModel : ViewModelBase
    {
        private readonly LocalDatabaseService _databaseService;
        private readonly List<StockCategoryItemModel> _allCategories = [];
        private string _selectedFilter = "全部分类";
        private string _searchText = string.Empty;
        private StockCategoryItemModel? _selectedCategory;
        private string _totalInventoryCost = "¥0";
        private string _categoryCountText = "0";
        private string _lowStockCountText = "0";
        private string _selectedCategoryName = "请选择分类";
        private string _selectedCategoryStockText = "0";
        private string _selectedCategoryBuyPriceText = "¥0";
        private string _selectedCategorySellPriceText = "¥0";
        private string _selectedCategoryInventoryCostText = "¥0";
        private string _selectedCategoryPriceBucketSummaryText = "暂无库存";
        private string _selectedCategoryProjectedNetProfitText = "¥0";
        private string _selectedCategoryLastActivityText = "暂无流水";
        private string _selectedCategoryLastAuditText = "暂无盘点";
        private string _selectedCategoryActivitySummaryText = "暂无业务记录";
        private string _adjustmentSectionTitle = "最近调整";
        private string _auditSectionTitle = "最近盘点";
        private string _lowStockTaskSummaryText = "暂无待补货分类";

        public StockViewModel(LocalDatabaseService databaseService)
        {
            _databaseService = databaseService;
            Title = "库存";
            Filters.Add("全部分类");
            Filters.Add("仅低库存");
            Filters.Add("仅有库存");
            Filters.Add("已归档");
        }

        public ObservableCollection<string> Filters { get; } = [];

        public ObservableCollection<StockCategoryItemModel> Categories { get; } = [];

        public ObservableCollection<StockAdjustmentRecordModel> RecentAdjustments { get; } = [];

        public ObservableCollection<StockAuditRecordModel> RecentAudits { get; } = [];

        public ObservableCollection<StockCategoryItemModel> LowStockTasks { get; } = [];

        public ObservableCollection<CategoryPriceBucketModel> SelectedCategoryPriceBuckets { get; } = [];

        public string SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (!SetProperty(ref _selectedFilter, value))
                {
                    return;
                }

                ApplyFilters();
            }
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                if (!SetProperty(ref _searchText, value))
                {
                    return;
                }

                ApplyFilters();
            }
        }

        public StockCategoryItemModel? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (!SetProperty(ref _selectedCategory, value))
                {
                    return;
                }

                RaiseSelectionState();
            }
        }

        public string TotalInventoryCost
        {
            get => _totalInventoryCost;
            private set => SetProperty(ref _totalInventoryCost, value);
        }

        public string CategoryCountText
        {
            get => _categoryCountText;
            private set => SetProperty(ref _categoryCountText, value);
        }

        public string LowStockCountText
        {
            get => _lowStockCountText;
            private set => SetProperty(ref _lowStockCountText, value);
        }

        public string SelectedCategoryName
        {
            get => _selectedCategoryName;
            private set => SetProperty(ref _selectedCategoryName, value);
        }

        public string SelectedCategoryStockText
        {
            get => _selectedCategoryStockText;
            private set => SetProperty(ref _selectedCategoryStockText, value);
        }

        public string SelectedCategoryBuyPriceText
        {
            get => _selectedCategoryBuyPriceText;
            private set => SetProperty(ref _selectedCategoryBuyPriceText, value);
        }

        public string SelectedCategorySellPriceText
        {
            get => _selectedCategorySellPriceText;
            private set => SetProperty(ref _selectedCategorySellPriceText, value);
        }

        public string SelectedCategoryInventoryCostText
        {
            get => _selectedCategoryInventoryCostText;
            private set => SetProperty(ref _selectedCategoryInventoryCostText, value);
        }

        public string SelectedCategoryPriceBucketSummaryText
        {
            get => _selectedCategoryPriceBucketSummaryText;
            private set => SetProperty(ref _selectedCategoryPriceBucketSummaryText, value);
        }

        public string SelectedCategoryProjectedNetProfitText
        {
            get => _selectedCategoryProjectedNetProfitText;
            private set => SetProperty(ref _selectedCategoryProjectedNetProfitText, value);
        }

        public string SelectedCategoryLastActivityText
        {
            get => _selectedCategoryLastActivityText;
            private set => SetProperty(ref _selectedCategoryLastActivityText, value);
        }

        public string SelectedCategoryLastAuditText
        {
            get => _selectedCategoryLastAuditText;
            private set => SetProperty(ref _selectedCategoryLastAuditText, value);
        }

        public string SelectedCategoryActivitySummaryText
        {
            get => _selectedCategoryActivitySummaryText;
            private set => SetProperty(ref _selectedCategoryActivitySummaryText, value);
        }

        public string AdjustmentSectionTitle
        {
            get => _adjustmentSectionTitle;
            private set => SetProperty(ref _adjustmentSectionTitle, value);
        }

        public string AuditSectionTitle
        {
            get => _auditSectionTitle;
            private set => SetProperty(ref _auditSectionTitle, value);
        }

        public string LowStockTaskSummaryText
        {
            get => _lowStockTaskSummaryText;
            private set => SetProperty(ref _lowStockTaskSummaryText, value);
        }

        public bool HasSelectedCategory => SelectedCategory is not null;

        public Visibility EmptyStateVisibility => Categories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility NoSelectionVisibility => HasSelectedCategory ? Visibility.Collapsed : Visibility.Visible;

        public Visibility DetailVisibility => HasSelectedCategory ? Visibility.Visible : Visibility.Collapsed;

        public Visibility RecentAdjustmentsEmptyVisibility =>
            HasSelectedCategory && RecentAdjustments.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility RecentAuditsEmptyVisibility =>
            HasSelectedCategory && RecentAudits.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility LowStockTasksEmptyVisibility => LowStockTasks.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility SelectedCategoryPriceBucketsEmptyVisibility =>
            HasSelectedCategory && SelectedCategoryPriceBuckets.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public async Task LoadAsync(long? preferredSelectedCategoryId = null)
        {
            IsBusy = true;

            try
            {
                var selectedCategoryId = preferredSelectedCategoryId ?? SelectedCategory?.Id;
                var items = await _databaseService.GetStockCategoryItemsAsync();
                var summary = await _databaseService.GetDashboardSummaryAsync();

                _allCategories.Clear();
                _allCategories.AddRange(items);
                RefreshLowStockTasks();

                TotalInventoryCost = Currency(summary.TotalInventoryCost);
                CategoryCountText = summary.CategoryCount.ToString(CultureInfo.InvariantCulture);
                LowStockCountText = summary.LowStockCount.ToString(CultureInfo.InvariantCulture);

                ApplyFilters(selectedCategoryId);
                await LoadSelectedCategoryDetailsAsync();
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task LoadSelectedCategoryDetailsAsync()
        {
            RecentAdjustments.Clear();
            RecentAudits.Clear();
            SelectedCategoryPriceBuckets.Clear();

            if (SelectedCategory is null)
            {
                SelectedCategoryName = "请选择分类";
                SelectedCategoryStockText = "0";
                SelectedCategoryBuyPriceText = Currency(0);
                SelectedCategorySellPriceText = Currency(0);
                SelectedCategoryInventoryCostText = Currency(0);
                SelectedCategoryPriceBucketSummaryText = "暂无库存";
                SelectedCategoryProjectedNetProfitText = Currency(0);
                SelectedCategoryLastActivityText = "暂无流水";
                SelectedCategoryLastAuditText = "暂无盘点";
                SelectedCategoryActivitySummaryText = "暂无业务记录";
                AuditSectionTitle = "最近盘点";
                AdjustmentSectionTitle = "最近调整";
                RaiseSelectionState();
                return;
            }

            var item = SelectedCategory;
            var auditsTask = _databaseService.GetRecentStockAuditsAsync(8, item.Id);
            var adjustmentsTask = _databaseService.GetRecentStockAdjustmentsAsync(12, item.Id);
            var priceBucketsTask = _databaseService.GetCategoryPriceBucketsAsync(item.Id);

            await Task.WhenAll(auditsTask, adjustmentsTask, priceBucketsTask);

            var audits = await auditsTask;
            var adjustments = await adjustmentsTask;
            var priceBuckets = await priceBucketsTask;

            foreach (var audit in audits)
            {
                RecentAudits.Add(audit);
            }

            foreach (var adjustment in adjustments)
            {
                RecentAdjustments.Add(adjustment);
            }

            foreach (var priceBucket in priceBuckets)
            {
                SelectedCategoryPriceBuckets.Add(priceBucket);
            }

            SelectedCategoryName = item.Name;
            SelectedCategoryStockText = item.DisplayStockText;
            SelectedCategoryBuyPriceText = item.BuyPriceRangeText;
            SelectedCategorySellPriceText = item.SellPriceText;
            SelectedCategoryInventoryCostText = item.InventoryCostText;
            SelectedCategoryPriceBucketSummaryText = item.PriceBucketSummaryText;
            SelectedCategoryProjectedNetProfitText = item.ProjectedNetProfitText;
            SelectedCategoryLastActivityText = item.LastActivityText;
            SelectedCategoryLastAuditText = audits.FirstOrDefault()?.TimestampText ?? "暂无盘点";
            SelectedCategoryActivitySummaryText = item.ActivitySummaryText;
            AuditSectionTitle = $"{item.Name} 的盘点记录";
            AdjustmentSectionTitle = $"{item.Name} 的调整记录";
            RaiseSelectionState();
        }

        public async Task AdjustSelectedCategoryStockAsync(StockAdjustmentInputModel input)
        {
            if (SelectedCategory is null || input.CategoryId != SelectedCategory.Id)
            {
                throw new InvalidOperationException("请先选择要调整的分类。");
            }

            await _databaseService.AdjustCategoryStockAsync(input.CategoryId, input.AdjustedQuantity, input.Reason);
            StatusMessage = "库存已修正，调整记录已保存。";
            await LoadAsync(input.CategoryId);
        }

        public async Task AuditSelectedCategoryAsync(StockAuditInputModel input)
        {
            if (SelectedCategory is null || input.CategoryId != SelectedCategory.Id)
            {
                throw new InvalidOperationException("请先选择要盘点的分类。");
            }

            await _databaseService.CreateStockAuditAsync(input.CategoryId, input.ActualQuantity, input.Note);
            StatusMessage = "盘点记录已保存，如有差异已同步校正库存。";
            await LoadAsync(input.CategoryId);
        }

        public async Task FocusCategoryAsync(long categoryId)
        {
            if (SelectedFilter != "全部分类")
            {
                SelectedFilter = "全部分类";
            }

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                SearchText = string.Empty;
            }

            ApplyFilters(categoryId);
            await LoadSelectedCategoryDetailsAsync();
        }

        private void ApplyFilters(long? preferredSelectedCategoryId = null)
        {
            var selectedId = preferredSelectedCategoryId ?? SelectedCategory?.Id;
            var filteredItems = _allCategories
                .Where(MatchesFilter)
                .Where(MatchesSearch)
                .OrderBy(item => item.IsArchived)
                .ThenByDescending(item => item.IsLowStock)
                .ThenBy(item => item.Name)
                .ToList();

            Categories.Clear();
            foreach (var item in filteredItems)
            {
                Categories.Add(item);
            }

            SelectedCategory = filteredItems.FirstOrDefault(item => item.Id == selectedId)
                ?? filteredItems.FirstOrDefault();

            OnPropertyChanged(nameof(EmptyStateVisibility));
        }

        private void RefreshLowStockTasks()
        {
            var lowStockItems = _allCategories
                .Where(item => item.CanQuickRestock)
                .OrderBy(item => item.Category.UnitType == WeightUnit.Piece ? item.CurrentQuantity : item.CurrentQuantity)
                .ThenBy(item => item.Name)
                .ToList();

            LowStockTasks.Clear();
            foreach (var item in lowStockItems)
            {
                LowStockTasks.Add(item);
            }

            LowStockTaskSummaryText = lowStockItems.Count == 0
                ? "暂无待补货分类"
                : $"{lowStockItems.Count} 个分类需要补货";

            OnPropertyChanged(nameof(LowStockTasksEmptyVisibility));
        }

        private bool MatchesFilter(StockCategoryItemModel item) => SelectedFilter switch
        {
            "仅低库存" => !item.IsArchived && item.IsLowStock,
            "仅有库存" => item.HasStock,
            "已归档" => item.IsArchived,
            _ => true
        };

        private bool MatchesSearch(StockCategoryItemModel item)
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                return true;
            }

            return item.Name.Contains(SearchText.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        private void RaiseSelectionState()
        {
            OnPropertyChanged(nameof(HasSelectedCategory));
            OnPropertyChanged(nameof(NoSelectionVisibility));
            OnPropertyChanged(nameof(DetailVisibility));
            OnPropertyChanged(nameof(RecentAuditsEmptyVisibility));
            OnPropertyChanged(nameof(RecentAdjustmentsEmptyVisibility));
            OnPropertyChanged(nameof(SelectedCategoryPriceBucketsEmptyVisibility));
        }

        private static string Currency(double value) => $"¥{value:0.##}";
    }
}
