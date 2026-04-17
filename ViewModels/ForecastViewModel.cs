namespace ClothingRecycler.Desktop.ViewModels
{
    public sealed class ForecastViewModel : ViewModelBase
    {
        private readonly LocalDatabaseService _databaseService;
        private string _projectedNetProfitText = "\u00A50";
        private string _projectedSalesAmountText = "\u00A50";
        private string _totalInventoryCostText = "\u00A50";
        private string _categoryCountText = "0";
        private string _lowStockCountText = "0";

        public ForecastViewModel(LocalDatabaseService databaseService)
        {
            _databaseService = databaseService;
            Title = "\u9884\u8BA1\u6536\u5165";
        }

        public ObservableCollection<CategoryModel> TopForecastCategories { get; } = [];

        public ObservableCollection<CategoryModel> LowStockCategories { get; } = [];

        public ObservableCollection<CategoryModel> TopSpreadCategories { get; } = [];

        public string ProjectedNetProfitText
        {
            get => _projectedNetProfitText;
            private set => SetProperty(ref _projectedNetProfitText, value);
        }

        public string ProjectedSalesAmountText
        {
            get => _projectedSalesAmountText;
            private set => SetProperty(ref _projectedSalesAmountText, value);
        }

        public string TotalInventoryCostText
        {
            get => _totalInventoryCostText;
            private set => SetProperty(ref _totalInventoryCostText, value);
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

        public Visibility ForecastRankingEmptyVisibility => TopForecastCategories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility LowStockEmptyVisibility => LowStockCategories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility PriceSpreadEmptyVisibility => TopSpreadCategories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public async Task LoadAsync()
        {
            IsBusy = true;

            try
            {
                var categories = await _databaseService.GetCategoriesWithPricingAsync();
                var summary = await _databaseService.GetDashboardSummaryAsync();

                var topForecast = categories
                    .OrderByDescending(category => category.ForecastNetProfit)
                    .ThenBy(category => category.Name)
                    .Take(12)
                    .ToList();

                var lowStock = categories
                    .Where(category => category.IsLowStock)
                    .OrderBy(category => category.DisplayStock)
                    .ThenBy(category => category.Name)
                    .Take(8)
                    .ToList();

                var topSpread = categories
                    .OrderByDescending(category => category.SellPrice - category.BuyPrice)
                    .ThenBy(category => category.Name)
                    .Take(8)
                    .ToList();

                ProjectedNetProfitText = Currency(summary.ProjectedNetProfit);
                ProjectedSalesAmountText = Currency(summary.ProjectedSalesAmount);
                TotalInventoryCostText = Currency(summary.TotalInventoryCost);
                CategoryCountText = summary.CategoryCount.ToString(CultureInfo.InvariantCulture);
                LowStockCountText = summary.LowStockCount.ToString(CultureInfo.InvariantCulture);

                ReplaceItems(TopForecastCategories, topForecast);
                ReplaceItems(LowStockCategories, lowStock);
                ReplaceItems(TopSpreadCategories, topSpread);

                RaiseEmptyStates();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RaiseEmptyStates()
        {
            OnPropertyChanged(nameof(ForecastRankingEmptyVisibility));
            OnPropertyChanged(nameof(LowStockEmptyVisibility));
            OnPropertyChanged(nameof(PriceSpreadEmptyVisibility));
        }

        private static void ReplaceItems<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
        {
            target.Clear();
            foreach (var item in source)
            {
                target.Add(item);
            }
        }

        private static string Currency(double value) => $"\u00A5{value:0.##}";
    }
}
