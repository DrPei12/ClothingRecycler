namespace ClothingRecycler.Desktop.ViewModels
{
    public sealed class DashboardViewModel : ViewModelBase
    {
        private readonly LocalDatabaseService _databaseService;
        private int _categoryCount;
        private int _lowStockCount;
        private string _todayInboundAmount = Currency(0);
        private string _todayOutboundAmount = Currency(0);
        private string _totalInventoryCost = Currency(0);
        private string _projectedSalesAmount = Currency(0);
        private string _projectedNetProfit = Currency(0);
        private bool _isQuickStartVisible;

        public DashboardViewModel(LocalDatabaseService databaseService)
        {
            _databaseService = databaseService;
            Title = "仪表盘";
        }

        public ObservableCollection<CategoryModel> TopCategories { get; } = [];

        public int CategoryCount
        {
            get => _categoryCount;
            private set => SetProperty(ref _categoryCount, value);
        }

        public int LowStockCount
        {
            get => _lowStockCount;
            private set => SetProperty(ref _lowStockCount, value);
        }

        public string TodayInboundAmount
        {
            get => _todayInboundAmount;
            private set => SetProperty(ref _todayInboundAmount, value);
        }

        public string TodayOutboundAmount
        {
            get => _todayOutboundAmount;
            private set => SetProperty(ref _todayOutboundAmount, value);
        }

        public string TotalInventoryCost
        {
            get => _totalInventoryCost;
            private set => SetProperty(ref _totalInventoryCost, value);
        }

        public string ProjectedSalesAmount
        {
            get => _projectedSalesAmount;
            private set => SetProperty(ref _projectedSalesAmount, value);
        }

        public string ProjectedNetProfit
        {
            get => _projectedNetProfit;
            private set => SetProperty(ref _projectedNetProfit, value);
        }

        public bool IsQuickStartVisible
        {
            get => _isQuickStartVisible;
            private set => SetProperty(ref _isQuickStartVisible, value);
        }

        public async Task LoadAsync()
        {
            IsBusy = true;

            try
            {
                var summary = await _databaseService.GetDashboardSummaryAsync();
                var topCategories = await _databaseService.GetTopForecastCategoriesAsync(5);

                CategoryCount = summary.CategoryCount;
                LowStockCount = summary.LowStockCount;
                TotalInventoryCost = Currency(summary.TotalInventoryCost);
                ProjectedSalesAmount = Currency(summary.ProjectedSalesAmount);
                ProjectedNetProfit = Currency(summary.ProjectedNetProfit);
                TodayInboundAmount = Currency(summary.TodayInboundAmount);
                TodayOutboundAmount = Currency(summary.TodayOutboundAmount);
                IsQuickStartVisible = summary.CategoryCount == 0;

                TopCategories.Clear();
                foreach (var category in topCategories)
                {
                    TopCategories.Add(category);
                }
            }
            finally
            {
                IsBusy = false;
            }
        }

        private static string Currency(double value) => $"￥{value:0.##}";
    }
}
