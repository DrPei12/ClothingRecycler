namespace ClothingRecycler.Desktop.ViewModels
{
    public sealed class StockViewModel : ViewModelBase
    {
        private readonly LocalDatabaseService _databaseService;
        private readonly List<StockCategoryItemModel> _allCategories = [];
        private string _selectedFilter = "\u5168\u90E8\u5206\u7C7B";
        private string _searchText = string.Empty;
        private StockCategoryItemModel? _selectedCategory;
        private string _totalInventoryCost = "\u00A50";
        private string _forecastRevenue = "\u00A50";
        private string _categoryCountText = "0";
        private string _lowStockCountText = "0";
        private string _selectedCategoryName = "\u8BF7\u9009\u62E9\u5206\u7C7B";
        private string _selectedCategoryStockText = "0";
        private string _selectedCategoryBuyPriceText = "\u00A50";
        private string _selectedCategorySellPriceText = "\u00A50";
        private string _selectedCategoryInventoryCostText = "\u00A50";
        private string _selectedCategoryForecastRevenueText = "\u00A50";
        private string _selectedCategoryPriceSpreadText = "\u00A50";
        private string _selectedCategoryLastActivityText = "\u6682\u65E0\u6D41\u6C34";
        private string _selectedCategoryLastAuditText = "\u6682\u65E0\u76D8\u70B9";
        private string _selectedCategoryActivitySummaryText = "\u6682\u65E0\u4E1A\u52A1\u8BB0\u5F55";
        private string _adjustmentSectionTitle = "\u6700\u8FD1\u8C03\u6574";
        private string _auditSectionTitle = "\u6700\u8FD1\u76D8\u70B9";
        private string _lowStockTaskSummaryText = "\u6682\u65E0\u5F85\u8865\u8D27\u5206\u7C7B";

        public StockViewModel(LocalDatabaseService databaseService)
        {
            _databaseService = databaseService;
            Title = "\u5E93\u5B58";
            Filters.Add("\u5168\u90E8\u5206\u7C7B");
            Filters.Add("\u4EC5\u4F4E\u5E93\u5B58");
            Filters.Add("\u4EC5\u6709\u5E93\u5B58");
            Filters.Add("\u5DF2\u5F52\u6863");
        }

        public ObservableCollection<string> Filters { get; } = [];

        public ObservableCollection<StockCategoryItemModel> Categories { get; } = [];

        public ObservableCollection<StockAdjustmentRecordModel> RecentAdjustments { get; } = [];

        public ObservableCollection<StockAuditRecordModel> RecentAudits { get; } = [];

        public ObservableCollection<StockCategoryItemModel> LowStockTasks { get; } = [];

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

        public string ForecastRevenue
        {
            get => _forecastRevenue;
            private set => SetProperty(ref _forecastRevenue, value);
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

        public string SelectedCategoryForecastRevenueText
        {
            get => _selectedCategoryForecastRevenueText;
            private set => SetProperty(ref _selectedCategoryForecastRevenueText, value);
        }

        public string SelectedCategoryPriceSpreadText
        {
            get => _selectedCategoryPriceSpreadText;
            private set => SetProperty(ref _selectedCategoryPriceSpreadText, value);
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
                ForecastRevenue = Currency(summary.ForecastRevenue);
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

            if (SelectedCategory is null)
            {
                SelectedCategoryName = "\u8BF7\u9009\u62E9\u5206\u7C7B";
                SelectedCategoryStockText = "0";
                SelectedCategoryBuyPriceText = Currency(0);
                SelectedCategorySellPriceText = Currency(0);
                SelectedCategoryInventoryCostText = Currency(0);
                SelectedCategoryForecastRevenueText = Currency(0);
                SelectedCategoryPriceSpreadText = Currency(0);
                SelectedCategoryLastActivityText = "\u6682\u65E0\u6D41\u6C34";
                SelectedCategoryLastAuditText = "\u6682\u65E0\u76D8\u70B9";
                SelectedCategoryActivitySummaryText = "\u6682\u65E0\u4E1A\u52A1\u8BB0\u5F55";
                AuditSectionTitle = "\u6700\u8FD1\u76D8\u70B9";
                AdjustmentSectionTitle = "\u6700\u8FD1\u8C03\u6574";
                RaiseSelectionState();
                return;
            }

            var item = SelectedCategory;
            var audits = await _databaseService.GetRecentStockAuditsAsync(8, item.Id);
            var adjustments = await _databaseService.GetRecentStockAdjustmentsAsync(12, item.Id);

            foreach (var audit in audits)
            {
                RecentAudits.Add(audit);
            }

            foreach (var adjustment in adjustments)
            {
                RecentAdjustments.Add(adjustment);
            }

            SelectedCategoryName = item.Name;
            SelectedCategoryStockText = item.DisplayStockText;
            SelectedCategoryBuyPriceText = item.BuyPriceText;
            SelectedCategorySellPriceText = item.SellPriceText;
            SelectedCategoryInventoryCostText = item.InventoryCostText;
            SelectedCategoryForecastRevenueText = item.ForecastRevenueText;
            SelectedCategoryPriceSpreadText = item.PriceSpreadText;
            SelectedCategoryLastActivityText = item.LastActivityText;
            SelectedCategoryLastAuditText = audits.FirstOrDefault()?.TimestampText ?? "\u6682\u65E0\u76D8\u70B9";
            SelectedCategoryActivitySummaryText = item.ActivitySummaryText;
            AuditSectionTitle = $"{item.Name} \u7684\u76D8\u70B9\u8BB0\u5F55";
            AdjustmentSectionTitle = $"{item.Name} \u7684\u8C03\u6574\u8BB0\u5F55";
            RaiseSelectionState();
        }

        public async Task AdjustSelectedCategoryStockAsync(StockAdjustmentInputModel input)
        {
            if (SelectedCategory is null || input.CategoryId != SelectedCategory.Id)
            {
                throw new InvalidOperationException("\u8BF7\u5148\u9009\u62E9\u8981\u8C03\u6574\u7684\u5206\u7C7B\u3002");
            }

            await _databaseService.AdjustCategoryStockAsync(input.CategoryId, input.AdjustedQuantity, input.Reason);
            StatusMessage = "\u5E93\u5B58\u5DF2\u4FEE\u6B63\uff0C\u8C03\u6574\u8BB0\u5F55\u5DF2\u4FDD\u5B58\u3002";
            await LoadAsync(input.CategoryId);
        }

        public async Task AuditSelectedCategoryAsync(StockAuditInputModel input)
        {
            if (SelectedCategory is null || input.CategoryId != SelectedCategory.Id)
            {
                throw new InvalidOperationException("\u8BF7\u5148\u9009\u62E9\u8981\u76D8\u70B9\u7684\u5206\u7C7B\u3002");
            }

            await _databaseService.CreateStockAuditAsync(input.CategoryId, input.ActualQuantity, input.Note);
            StatusMessage = "\u76D8\u70B9\u8BB0\u5F55\u5DF2\u4FDD\u5B58\uff0C\u5982\u6709\u5DEE\u5F02\u5DF2\u540C\u6B65\u6821\u6B63\u5E93\u5B58\u3002";
            await LoadAsync(input.CategoryId);
        }

        public async Task FocusCategoryAsync(long categoryId)
        {
            if (SelectedFilter != "\u5168\u90E8\u5206\u7C7B")
            {
                SelectedFilter = "\u5168\u90E8\u5206\u7C7B";
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
                ? "\u6682\u65E0\u5F85\u8865\u8D27\u5206\u7C7B"
                : $"{lowStockItems.Count} \u4E2A\u5206\u7C7B\u9700\u8981\u8865\u8D27";

            OnPropertyChanged(nameof(LowStockTasksEmptyVisibility));
        }

        private bool MatchesFilter(StockCategoryItemModel item) => SelectedFilter switch
        {
            "\u4EC5\u4F4E\u5E93\u5B58" => !item.IsArchived && item.IsLowStock,
            "\u4EC5\u6709\u5E93\u5B58" => item.HasStock,
            "\u5DF2\u5F52\u6863" => item.IsArchived,
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
        }

        private static string Currency(double value) => $"\u00A5{value:0.##}";
    }
}
