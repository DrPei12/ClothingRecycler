namespace ClothingRecycler.Desktop.ViewModels
{
    public sealed class OrdersViewModel : ViewModelBase
    {
        private readonly LocalDatabaseService _databaseService;
        private string _selectedFilter = "\u5168\u90E8";
        private OrderListItemModel? _selectedOrder;
        private string _totalOrderCountText = "0";
        private string _todayOrderCountText = "0";
        private string _inboundOrderCountText = "0";
        private string _outboundOrderCountText = "0";
        private string _selectedOrderNumberText = "\u8BF7\u9009\u62E9\u8BA2\u5355";
        private string _selectedOrderTypeText = "-";
        private string _selectedOrderCustomerText = "\u533F\u540D";
        private string _selectedOrderAmountText = "\u00A50";
        private string _selectedOrderQuantityText = "0";
        private string _selectedOrderTimestampText = "\u6682\u65E0";

        public OrdersViewModel(LocalDatabaseService databaseService)
        {
            _databaseService = databaseService;
            Title = "\u8BA2\u5355";
            Filters.Add("\u5168\u90E8");
            Filters.Add("\u5165\u5E93");
            Filters.Add("\u51FA\u5E93");
        }

        public ObservableCollection<string> Filters { get; } = [];

        public ObservableCollection<OrderListItemModel> Orders { get; } = [];

        public ObservableCollection<OrderDetailItemModel> SelectedOrderItems { get; } = [];

        public string SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (!SetProperty(ref _selectedFilter, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(EmptyStateVisibility));
            }
        }

        public OrderListItemModel? SelectedOrder
        {
            get => _selectedOrder;
            set
            {
                if (!SetProperty(ref _selectedOrder, value))
                {
                    return;
                }

                RaiseDetailVisibility();
            }
        }

        public string TotalOrderCountText
        {
            get => _totalOrderCountText;
            private set => SetProperty(ref _totalOrderCountText, value);
        }

        public string TodayOrderCountText
        {
            get => _todayOrderCountText;
            private set => SetProperty(ref _todayOrderCountText, value);
        }

        public string InboundOrderCountText
        {
            get => _inboundOrderCountText;
            private set => SetProperty(ref _inboundOrderCountText, value);
        }

        public string OutboundOrderCountText
        {
            get => _outboundOrderCountText;
            private set => SetProperty(ref _outboundOrderCountText, value);
        }

        public string SelectedOrderNumberText
        {
            get => _selectedOrderNumberText;
            private set => SetProperty(ref _selectedOrderNumberText, value);
        }

        public string SelectedOrderTypeText
        {
            get => _selectedOrderTypeText;
            private set => SetProperty(ref _selectedOrderTypeText, value);
        }

        public string SelectedOrderCustomerText
        {
            get => _selectedOrderCustomerText;
            private set => SetProperty(ref _selectedOrderCustomerText, value);
        }

        public string SelectedOrderAmountText
        {
            get => _selectedOrderAmountText;
            private set => SetProperty(ref _selectedOrderAmountText, value);
        }

        public string SelectedOrderQuantityText
        {
            get => _selectedOrderQuantityText;
            private set => SetProperty(ref _selectedOrderQuantityText, value);
        }

        public string SelectedOrderTimestampText
        {
            get => _selectedOrderTimestampText;
            private set => SetProperty(ref _selectedOrderTimestampText, value);
        }

        public bool HasSelectedOrder => SelectedOrder is not null;

        public Visibility EmptyStateVisibility => Orders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility NoSelectionVisibility => HasSelectedOrder ? Visibility.Collapsed : Visibility.Visible;

        public Visibility DetailVisibility => HasSelectedOrder ? Visibility.Visible : Visibility.Collapsed;

        public Visibility SelectedOrderItemsEmptyVisibility =>
            HasSelectedOrder && SelectedOrderItems.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public async Task LoadAsync(long? preferredSelectedOrderId = null)
        {
            IsBusy = true;

            try
            {
                var selectedOrderId = preferredSelectedOrderId ?? SelectedOrder?.Id;
                var overview = await _databaseService.GetOrderOverviewAsync();
                var orders = await _databaseService.GetRecentOrdersAsync(MapFilterToType(SelectedFilter), 50);

                Orders.Clear();
                foreach (var order in orders)
                {
                    Orders.Add(order);
                }

                TotalOrderCountText = overview.TotalOrderCount.ToString(CultureInfo.InvariantCulture);
                TodayOrderCountText = overview.TodayOrderCount.ToString(CultureInfo.InvariantCulture);
                InboundOrderCountText = overview.InboundOrderCount.ToString(CultureInfo.InvariantCulture);
                OutboundOrderCountText = overview.OutboundOrderCount.ToString(CultureInfo.InvariantCulture);

                SelectedOrder = orders.FirstOrDefault(order => order.Id == selectedOrderId)
                    ?? orders.FirstOrDefault();

                await LoadSelectedOrderDetailsAsync();

                OnPropertyChanged(nameof(EmptyStateVisibility));
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task LoadSelectedOrderDetailsAsync()
        {
            SelectedOrderItems.Clear();

            if (SelectedOrder is null)
            {
                SelectedOrderNumberText = "\u8BF7\u9009\u62E9\u8BA2\u5355";
                SelectedOrderTypeText = "-";
                SelectedOrderCustomerText = "\u533F\u540D";
                SelectedOrderAmountText = Currency(0);
                SelectedOrderQuantityText = "0";
                SelectedOrderTimestampText = "\u6682\u65E0";
                RaiseDetailVisibility();
                return;
            }

            var items = await _databaseService.GetOrderDetailItemsAsync(SelectedOrder.Id);
            foreach (var item in items)
            {
                SelectedOrderItems.Add(item);
            }

            SelectedOrderNumberText = SelectedOrder.OrderNumber;
            SelectedOrderTypeText = SelectedOrder.TypeText;
            SelectedOrderCustomerText = SelectedOrder.CustomerDisplayName;
            SelectedOrderAmountText = SelectedOrder.TotalAmountText;
            SelectedOrderQuantityText = SelectedOrder.TotalWeightText;
            SelectedOrderTimestampText = SelectedOrder.TimestampText;

            RaiseDetailVisibility();
        }

        public async Task<OrderEditModel?> GetSelectedOrderEditModelAsync()
        {
            if (SelectedOrder is null)
            {
                return null;
            }

            return await _databaseService.GetOrderEditModelAsync(SelectedOrder.Id);
        }

        public Task<IReadOnlyList<CategoryModel>> GetCategoriesAsync() => _databaseService.GetCategoriesAsync();

        public Task<IReadOnlyList<CustomerModel>> GetCustomersAsync() => _databaseService.GetCustomersAsync();

        public Task<IReadOnlyList<CustomerCategoryPriceModel>> GetCustomerCategoryPricesAsync() =>
            _databaseService.GetCustomerCategoryPricesAsync();

        public async Task UpdateOrderAsync(OrderEditModel order)
        {
            await _databaseService.UpdateOrderAsync(order);
            StatusMessage = "\u8BA2\u5355\u5DF2\u4FEE\u6B63\uff0C\u5E93\u5B58\u548C\u5BA2\u6237\u6D3E\u751F\u6570\u636E\u5DF2\u91CD\u5EFA\u3002";
            await LoadAsync(order.OrderId);
        }

        public async Task DeleteSelectedOrderAsync()
        {
            if (SelectedOrder is null)
            {
                throw new InvalidOperationException("\u8BF7\u5148\u9009\u62E9\u8981\u5220\u9664\u7684\u8BA2\u5355\u3002");
            }

            await _databaseService.DeleteOrderAsync(SelectedOrder.Id);
            StatusMessage = "\u8BA2\u5355\u5DF2\u5220\u9664\uff0c\u5E93\u5B58\u548C\u5BA2\u6237\u6D3E\u751F\u6570\u636E\u5DF2\u56DE\u6EDA\u3002";
            await LoadAsync();
        }

        private void RaiseDetailVisibility()
        {
            OnPropertyChanged(nameof(HasSelectedOrder));
            OnPropertyChanged(nameof(NoSelectionVisibility));
            OnPropertyChanged(nameof(DetailVisibility));
            OnPropertyChanged(nameof(SelectedOrderItemsEmptyVisibility));
        }

        private static string? MapFilterToType(string filter) => filter switch
        {
            "\u5165\u5E93" => "inbound",
            "\u51FA\u5E93" => "outbound",
            _ => null
        };

        private static string Currency(double value) => $"\u00A5{value:0.##}";
    }
}
