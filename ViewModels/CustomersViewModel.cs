namespace ClothingRecycler.Desktop.ViewModels
{
    public sealed class CustomersViewModel : ViewModelBase
    {
        private readonly LocalDatabaseService _databaseService;
        private CustomerListItemModel? _selectedCustomer;
        private string _totalCustomerCountText = "0";
        private string _activeTodayCustomerCountText = "0";
        private string _inboundCustomerCountText = "0";
        private string _outboundCustomerCountText = "0";
        private string _selectedCustomerName = "\u8BF7\u9009\u62E9\u5BA2\u6237";
        private string _selectedCustomerOrderCountText = "0";
        private string _selectedCustomerInboundAmountText = "\u00A50";
        private string _selectedCustomerOutboundAmountText = "\u00A50";
        private string _selectedCustomerLastTransactionText = "\u6682\u65E0\u4EA4\u6613";
        private string _selectedCustomerPhoneText = "\u672A\u586B\u5199";
        private string _selectedCustomerEmailText = "\u672A\u586B\u5199";
        private string _selectedCustomerAddressText = "\u672A\u586B\u5199";
        private string _selectedCustomerNoteText = "\u6682\u65E0\u5907\u6CE8";

        public CustomersViewModel(LocalDatabaseService databaseService)
        {
            _databaseService = databaseService;
            Title = "\u5BA2\u6237";
        }

        public ObservableCollection<CustomerListItemModel> Customers { get; } = [];

        public ObservableCollection<OrderListItemModel> SelectedCustomerOrders { get; } = [];

        public ObservableCollection<CustomerPriceMemoryItemModel> SelectedCustomerPriceMemories { get; } = [];

        public CustomerListItemModel? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (!SetProperty(ref _selectedCustomer, value))
                {
                    return;
                }

                OnPropertyChanged(nameof(HasSelectedCustomer));
                OnPropertyChanged(nameof(NoSelectionVisibility));
                OnPropertyChanged(nameof(DetailVisibility));
            }
        }

        public string TotalCustomerCountText
        {
            get => _totalCustomerCountText;
            private set => SetProperty(ref _totalCustomerCountText, value);
        }

        public string ActiveTodayCustomerCountText
        {
            get => _activeTodayCustomerCountText;
            private set => SetProperty(ref _activeTodayCustomerCountText, value);
        }

        public string InboundCustomerCountText
        {
            get => _inboundCustomerCountText;
            private set => SetProperty(ref _inboundCustomerCountText, value);
        }

        public string OutboundCustomerCountText
        {
            get => _outboundCustomerCountText;
            private set => SetProperty(ref _outboundCustomerCountText, value);
        }

        public string SelectedCustomerName
        {
            get => _selectedCustomerName;
            private set => SetProperty(ref _selectedCustomerName, value);
        }

        public string SelectedCustomerOrderCountText
        {
            get => _selectedCustomerOrderCountText;
            private set => SetProperty(ref _selectedCustomerOrderCountText, value);
        }

        public string SelectedCustomerInboundAmountText
        {
            get => _selectedCustomerInboundAmountText;
            private set => SetProperty(ref _selectedCustomerInboundAmountText, value);
        }

        public string SelectedCustomerOutboundAmountText
        {
            get => _selectedCustomerOutboundAmountText;
            private set => SetProperty(ref _selectedCustomerOutboundAmountText, value);
        }

        public string SelectedCustomerLastTransactionText
        {
            get => _selectedCustomerLastTransactionText;
            private set => SetProperty(ref _selectedCustomerLastTransactionText, value);
        }

        public string SelectedCustomerPhoneText
        {
            get => _selectedCustomerPhoneText;
            private set => SetProperty(ref _selectedCustomerPhoneText, value);
        }

        public string SelectedCustomerEmailText
        {
            get => _selectedCustomerEmailText;
            private set => SetProperty(ref _selectedCustomerEmailText, value);
        }

        public string SelectedCustomerAddressText
        {
            get => _selectedCustomerAddressText;
            private set => SetProperty(ref _selectedCustomerAddressText, value);
        }

        public string SelectedCustomerNoteText
        {
            get => _selectedCustomerNoteText;
            private set => SetProperty(ref _selectedCustomerNoteText, value);
        }

        public bool HasSelectedCustomer => SelectedCustomer is not null;

        public Visibility EmptyStateVisibility => Customers.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility NoSelectionVisibility => HasSelectedCustomer ? Visibility.Collapsed : Visibility.Visible;

        public Visibility DetailVisibility => HasSelectedCustomer ? Visibility.Visible : Visibility.Collapsed;

        public Visibility SelectedCustomerOrdersEmptyVisibility =>
            HasSelectedCustomer && SelectedCustomerOrders.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility SelectedCustomerPriceMemoriesEmptyVisibility =>
            HasSelectedCustomer && SelectedCustomerPriceMemories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public async Task LoadAsync(long? preferredSelectedCustomerId = null)
        {
            IsBusy = true;

            try
            {
                var selectedCustomerId = preferredSelectedCustomerId ?? SelectedCustomer?.Id;
                var overview = await _databaseService.GetCustomerOverviewAsync();
                var customers = await _databaseService.GetCustomerListItemsAsync();

                Customers.Clear();
                foreach (var customer in customers)
                {
                    Customers.Add(customer);
                }

                TotalCustomerCountText = overview.TotalCustomerCount.ToString(CultureInfo.InvariantCulture);
                ActiveTodayCustomerCountText = overview.ActiveTodayCustomerCount.ToString(CultureInfo.InvariantCulture);
                InboundCustomerCountText = overview.InboundCustomerCount.ToString(CultureInfo.InvariantCulture);
                OutboundCustomerCountText = overview.OutboundCustomerCount.ToString(CultureInfo.InvariantCulture);

                SelectedCustomer = customers.FirstOrDefault(customer => customer.Id == selectedCustomerId)
                    ?? customers.FirstOrDefault();

                await LoadSelectedCustomerDetailsAsync();

                OnPropertyChanged(nameof(EmptyStateVisibility));
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task LoadSelectedCustomerDetailsAsync()
        {
            SelectedCustomerOrders.Clear();
            SelectedCustomerPriceMemories.Clear();

            if (SelectedCustomer is null)
            {
                SelectedCustomerName = "\u8BF7\u9009\u62E9\u5BA2\u6237";
                SelectedCustomerOrderCountText = "0";
                SelectedCustomerInboundAmountText = Currency(0);
                SelectedCustomerOutboundAmountText = Currency(0);
                SelectedCustomerLastTransactionText = "\u6682\u65E0\u4EA4\u6613";
                SelectedCustomerPhoneText = "\u672A\u586B\u5199";
                SelectedCustomerEmailText = "\u672A\u586B\u5199";
                SelectedCustomerAddressText = "\u672A\u586B\u5199";
                SelectedCustomerNoteText = "\u6682\u65E0\u5907\u6CE8";
                RaiseDetailVisibility();
                return;
            }

            var customer = await _databaseService.GetCustomerAsync(SelectedCustomer.Id);
            var summary = await _databaseService.GetCustomerDetailSummaryAsync(SelectedCustomer.Id);
            var orders = await _databaseService.GetCustomerRecentOrdersAsync(SelectedCustomer.Id, 12);
            var memories = await _databaseService.GetCustomerPriceMemoriesAsync(SelectedCustomer.Id);

            foreach (var order in orders)
            {
                SelectedCustomerOrders.Add(order);
            }

            foreach (var memory in memories)
            {
                SelectedCustomerPriceMemories.Add(memory);
            }

            SelectedCustomerName = customer?.DisplayName ?? SelectedCustomer.DisplayName;
            SelectedCustomerOrderCountText = summary.TotalOrderCount.ToString(CultureInfo.InvariantCulture);
            SelectedCustomerInboundAmountText = Currency(summary.InboundAmount);
            SelectedCustomerOutboundAmountText = Currency(summary.OutboundAmount);
            SelectedCustomerLastTransactionText = summary.LastTransactionAt?.ToLocalTime().ToString("MM-dd HH:mm")
                ?? "\u6682\u65E0\u4EA4\u6613";
            SelectedCustomerPhoneText = customer?.PhoneDisplayText ?? "\u672A\u586B\u5199";
            SelectedCustomerEmailText = customer?.EmailDisplayText ?? "\u672A\u586B\u5199";
            SelectedCustomerAddressText = customer?.AddressDisplayText ?? "\u672A\u586B\u5199";
            SelectedCustomerNoteText = customer?.NoteDisplayText ?? "\u6682\u65E0\u5907\u6CE8";

            RaiseDetailVisibility();
        }

        public async Task<CustomerModel?> GetSelectedCustomerAsync()
        {
            if (SelectedCustomer is null)
            {
                return null;
            }

            return await _databaseService.GetCustomerAsync(SelectedCustomer.Id);
        }

        public async Task<long> SaveCustomerAsync(CustomerModel customer)
        {
            var customerId = await _databaseService.SaveCustomerAsync(customer);
            StatusMessage = "\u5BA2\u6237\u4FE1\u606F\u5DF2\u4FDD\u5B58\u3002";
            await LoadAsync(customerId);
            return customerId;
        }

        private void RaiseDetailVisibility()
        {
            OnPropertyChanged(nameof(HasSelectedCustomer));
            OnPropertyChanged(nameof(NoSelectionVisibility));
            OnPropertyChanged(nameof(DetailVisibility));
            OnPropertyChanged(nameof(SelectedCustomerOrdersEmptyVisibility));
            OnPropertyChanged(nameof(SelectedCustomerPriceMemoriesEmptyVisibility));
        }

        private static string Currency(double value) => $"\u00A5{value:0.##}";
    }
}
