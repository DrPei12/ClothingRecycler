namespace ClothingRecycler.Desktop.ViewModels
{
    public sealed class AnalyticsViewModel : ViewModelBase
    {
        private readonly LocalDatabaseService _databaseService;
        private string _totalInboundAmountText = "\u00A50";
        private string _totalOutboundAmountText = "\u00A50";
        private string _estimatedMarginText = "\u00A50";
        private string _activeCustomerCountText = "0";
        private string _totalOrderCountText = "0";

        public AnalyticsViewModel(LocalDatabaseService databaseService)
        {
            _databaseService = databaseService;
            Title = "\u7ECF\u8425\u5206\u6790";
        }

        public ObservableCollection<DailyAnalyticsItemModel> DailyItems { get; } = [];

        public ObservableCollection<CategoryAnalyticsItemModel> TopInboundCategories { get; } = [];

        public ObservableCollection<CategoryAnalyticsItemModel> TopOutboundCategories { get; } = [];

        public ObservableCollection<CustomerAnalyticsItemModel> TopCustomers { get; } = [];

        public string TotalInboundAmountText
        {
            get => _totalInboundAmountText;
            private set => SetProperty(ref _totalInboundAmountText, value);
        }

        public string TotalOutboundAmountText
        {
            get => _totalOutboundAmountText;
            private set => SetProperty(ref _totalOutboundAmountText, value);
        }

        public string EstimatedMarginText
        {
            get => _estimatedMarginText;
            private set => SetProperty(ref _estimatedMarginText, value);
        }

        public string ActiveCustomerCountText
        {
            get => _activeCustomerCountText;
            private set => SetProperty(ref _activeCustomerCountText, value);
        }

        public string TotalOrderCountText
        {
            get => _totalOrderCountText;
            private set => SetProperty(ref _totalOrderCountText, value);
        }

        public Visibility DailyEmptyVisibility => DailyItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility InboundRankingEmptyVisibility => TopInboundCategories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility OutboundRankingEmptyVisibility => TopOutboundCategories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CustomerRankingEmptyVisibility => TopCustomers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public async Task LoadAsync()
        {
            IsBusy = true;

            try
            {
                var overview = await _databaseService.GetAnalyticsOverviewAsync();
                var dailyItems = await _databaseService.GetDailyAnalyticsAsync(7);
                var inboundCategories = await _databaseService.GetTopInboundCategoriesAnalyticsAsync(6);
                var outboundCategories = await _databaseService.GetTopOutboundCategoriesAnalyticsAsync(6);
                var topCustomers = await _databaseService.GetTopCustomersAnalyticsAsync(6);

                TotalInboundAmountText = Currency(overview.TotalInboundAmount);
                TotalOutboundAmountText = Currency(overview.TotalOutboundAmount);
                EstimatedMarginText = Currency(overview.EstimatedMargin);
                ActiveCustomerCountText = overview.ActiveCustomerCount.ToString(CultureInfo.InvariantCulture);
                TotalOrderCountText = overview.TotalOrderCount.ToString(CultureInfo.InvariantCulture);

                ReplaceItems(DailyItems, dailyItems);
                ReplaceItems(TopInboundCategories, inboundCategories);
                ReplaceItems(TopOutboundCategories, outboundCategories);
                ReplaceItems(TopCustomers, topCustomers);

                RaiseEmptyStates();
            }
            finally
            {
                IsBusy = false;
            }
        }

        private void RaiseEmptyStates()
        {
            OnPropertyChanged(nameof(DailyEmptyVisibility));
            OnPropertyChanged(nameof(InboundRankingEmptyVisibility));
            OnPropertyChanged(nameof(OutboundRankingEmptyVisibility));
            OnPropertyChanged(nameof(CustomerRankingEmptyVisibility));
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
