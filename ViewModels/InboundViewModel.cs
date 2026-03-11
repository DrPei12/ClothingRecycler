namespace ClothingRecycler.Desktop.ViewModels
{
    public sealed class InboundViewModel : ViewModelBase
    {
        private readonly LocalDatabaseService _databaseService;
        private readonly List<CustomerCategoryPriceModel> _priceMemories = [];

        private CategoryModel? _selectedCategory;
        private CustomerModel? _selectedCustomer;
        private string _newCustomerName = string.Empty;
        private double _quantity;
        private double _unitPrice;
        private string _todayInboundAmount = "\u00A50";
        private string _categoryCountText = "0";
        private string _recentRecordCountText = "0";

        public InboundViewModel(LocalDatabaseService databaseService)
        {
            _databaseService = databaseService;
            Title = "\u5165\u5E93";
        }

        public ObservableCollection<CategoryModel> Categories { get; } = [];

        public ObservableCollection<CustomerModel> Customers { get; } = [];

        public ObservableCollection<InboundRecordModel> RecentRecords { get; } = [];

        public CategoryModel? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (!SetProperty(ref _selectedCategory, value))
                {
                    return;
                }

                ApplySuggestedUnitPrice();
                RaiseComputedStateChanged();
            }
        }

        public CustomerModel? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (!SetProperty(ref _selectedCustomer, value))
                {
                    return;
                }

                if (string.IsNullOrWhiteSpace(NewCustomerName))
                {
                    ApplySuggestedUnitPrice();
                }

                RaiseComputedStateChanged();
            }
        }

        public string NewCustomerName
        {
            get => _newCustomerName;
            set
            {
                if (!SetProperty(ref _newCustomerName, value))
                {
                    return;
                }

                ApplySuggestedUnitPrice();
                RaiseComputedStateChanged();
            }
        }

        public double Quantity
        {
            get => _quantity;
            set
            {
                if (!SetProperty(ref _quantity, value))
                {
                    return;
                }

                RaiseComputedStateChanged();
            }
        }

        public double UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (!SetProperty(ref _unitPrice, value))
                {
                    return;
                }

                RaiseComputedStateChanged();
            }
        }

        public string TodayInboundAmount
        {
            get => _todayInboundAmount;
            private set => SetProperty(ref _todayInboundAmount, value);
        }

        public string CategoryCountText
        {
            get => _categoryCountText;
            private set => SetProperty(ref _categoryCountText, value);
        }

        public string RecentRecordCountText
        {
            get => _recentRecordCountText;
            private set => SetProperty(ref _recentRecordCountText, value);
        }

        public string CurrentStockText => SelectedCategory?.DisplayStockText ?? "\u8BF7\u5148\u5728\u201C\u8BBE\u7F6E\u201D\u4E2D\u521B\u5EFA\u5206\u7C7B";

        public string SelectedUnitText => SelectedCategory?.UnitLabel ?? "-";

        public string EffectiveCustomerText => string.IsNullOrWhiteSpace(ResolvedCustomerName)
            ? "\u533F\u540D\u5165\u5E93"
            : ResolvedCustomerName;

        public string TotalCostText => Currency(NormalizeQuantity() * UnitPrice);

        public bool CanSubmit => SelectedCategory is not null && NormalizeQuantity() > 0 && UnitPrice > 0;

        public Visibility EmptyStateVisibility => Categories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility FormVisibility => Categories.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        public Visibility RecentRecordsEmptyVisibility => RecentRecords.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        private string ResolvedCustomerName
        {
            get
            {
                var trimmedNewName = NewCustomerName.Trim();
                if (!string.IsNullOrWhiteSpace(trimmedNewName))
                {
                    return trimmedNewName;
                }

                return SelectedCustomer?.Name?.Trim() ?? string.Empty;
            }
        }

        public async Task LoadAsync()
        {
            IsBusy = true;

            try
            {
                var selectedCategoryId = SelectedCategory?.Id;
                var selectedCustomerId = SelectedCustomer?.Id;
                var preservedNewCustomerName = NewCustomerName;

                var categories = await _databaseService.GetActiveCategoriesAsync();
                var customers = await _databaseService.GetInboundCustomersAsync();
                var priceMemories = await _databaseService.GetCustomerCategoryPricesAsync();
                var summary = await _databaseService.GetDashboardSummaryAsync();
                var recentRecords = await _databaseService.GetRecentInboundRecordsAsync(12);

                Categories.Clear();
                foreach (var category in categories)
                {
                    Categories.Add(category);
                }

                Customers.Clear();
                foreach (var customer in customers)
                {
                    Customers.Add(customer);
                }

                RecentRecords.Clear();
                foreach (var record in recentRecords)
                {
                    RecentRecords.Add(record);
                }

                _priceMemories.Clear();
                _priceMemories.AddRange(priceMemories.OrderByDescending(memory => memory.UpdatedAt));

                SelectedCategory = categories.FirstOrDefault(category => category.Id == selectedCategoryId)
                    ?? categories.FirstOrDefault();
                SelectedCustomer = customers.FirstOrDefault(customer => customer.Id == selectedCustomerId);
                NewCustomerName = preservedNewCustomerName;

                if (SelectedCategory is null)
                {
                    Quantity = 0;
                    UnitPrice = 0;
                }

                TodayInboundAmount = Currency(summary.TodayInboundAmount);
                CategoryCountText = summary.CategoryCount.ToString(CultureInfo.InvariantCulture);
                RecentRecordCountText = recentRecords.Count.ToString(CultureInfo.InvariantCulture);

                RaiseComputedStateChanged();
                OnPropertyChanged(nameof(EmptyStateVisibility));
                OnPropertyChanged(nameof(FormVisibility));
                OnPropertyChanged(nameof(RecentRecordsEmptyVisibility));
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task SubmitAsync()
        {
            if (SelectedCategory is null)
            {
                throw new InvalidOperationException("\u8BF7\u5148\u9009\u62E9\u5165\u5E93\u5206\u7C7B\u3002");
            }

            var normalizedQuantity = NormalizeQuantity();
            if (normalizedQuantity <= 0)
            {
                throw new InvalidOperationException("\u5165\u5E93\u6570\u91CF\u5FC5\u987B\u5927\u4E8E 0\u3002");
            }

            if (UnitPrice <= 0)
            {
                throw new InvalidOperationException("\u5165\u5E93\u5355\u4EF7\u5FC5\u987B\u5927\u4E8E 0\u3002");
            }

            var customerName = ResolvedCustomerName;
            await _databaseService.AddInboundRecordAsync(SelectedCategory.Id, normalizedQuantity, UnitPrice, customerName);

            Quantity = SelectedCategory.UnitType == WeightUnit.Piece ? 1 : 0;
            NewCustomerName = string.Empty;
            StatusMessage = "\u5165\u5E93\u5DF2\u4FDD\u5B58\uff0C\u5E93\u5B58\u5DF2\u66F4\u65B0\u3002";

            await LoadAsync();

            if (!string.IsNullOrWhiteSpace(customerName))
            {
                SelectedCustomer = Customers.FirstOrDefault(customer =>
                    string.Equals(customer.Name, customerName, StringComparison.OrdinalIgnoreCase));
            }
        }

        private void ApplySuggestedUnitPrice()
        {
            if (SelectedCategory is null)
            {
                return;
            }

            var rememberedPrice = GetRememberedPrice();
            UnitPrice = rememberedPrice ?? SelectedCategory.BuyPrice;
        }

        private double? GetRememberedPrice()
        {
            if (SelectedCategory is null)
            {
                return null;
            }

            var customerId = ResolveCustomerId();
            if (!customerId.HasValue)
            {
                return null;
            }

            return _priceMemories
                .FirstOrDefault(memory => memory.CustomerId == customerId.Value && memory.CategoryId == SelectedCategory.Id)
                ?.Price;
        }

        private long? ResolveCustomerId()
        {
            var trimmedNewName = NewCustomerName.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedNewName))
            {
                return Customers
                    .FirstOrDefault(customer => string.Equals(customer.Name, trimmedNewName, StringComparison.OrdinalIgnoreCase))
                    ?.Id;
            }

            return SelectedCustomer?.Id;
        }

        private double NormalizeQuantity()
        {
            var quantity = Math.Max(0, Quantity);

            return SelectedCategory?.UnitType == WeightUnit.Piece
                ? Math.Round(quantity)
                : Math.Round(quantity, 2);
        }

        private void RaiseComputedStateChanged()
        {
            OnPropertyChanged(nameof(CurrentStockText));
            OnPropertyChanged(nameof(SelectedUnitText));
            OnPropertyChanged(nameof(EffectiveCustomerText));
            OnPropertyChanged(nameof(TotalCostText));
            OnPropertyChanged(nameof(CanSubmit));
        }

        private static string Currency(double value) => $"\u00A5{value:0.##}";
    }
}
