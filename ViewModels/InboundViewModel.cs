namespace ClothingRecycler.Desktop.ViewModels
{
    public sealed class InboundViewModel : ViewModelBase
    {
        private readonly LocalDatabaseService _databaseService;
        private readonly List<CustomerCategoryPriceModel> _priceMemories = [];

        private CustomerModel? _selectedCustomer;
        private string _newCustomerName = string.Empty;
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

        public ObservableCollection<InboundOrderDraftLineModel> CategoryEntries { get; } = [];

        public CustomerModel? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (!SetProperty(ref _selectedCustomer, value))
                {
                    return;
                }

                ApplySuggestedUnitPrices();
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

                ApplySuggestedUnitPrices();
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

        public string EffectiveCustomerText => string.IsNullOrWhiteSpace(ResolvedCustomerName)
            ? "\u8BF7\u5148\u9009\u62E9\u6216\u65B0\u5EFA\u5BA2\u6237"
            : ResolvedCustomerName;

        public string FilledCategoryCountText => FilledEntries.Count().ToString(CultureInfo.InvariantCulture);

        public string QuantitySummaryText
        {
            get
            {
                var filledEntries = FilledEntries.ToList();
                if (filledEntries.Count == 0)
                {
                    return "0";
                }

                return string.Join(" / ",
                    filledEntries
                        .GroupBy(entry => entry.Category.UnitType)
                        .Select(group =>
                        {
                            var quantity = group.Sum(entry => entry.NormalizedQuantity);
                            var unitLabel = group.First().UnitLabel;
                            return group.Key == WeightUnit.Piece
                                ? $"{Math.Round(quantity):0} {unitLabel}"
                                : $"{quantity:0.##} {unitLabel}";
                        }));
            }
        }

        public string TotalCostText => Currency(FilledEntries.Sum(entry => entry.LineAmount));

        public bool HasCustomerSelection => !string.IsNullOrWhiteSpace(ResolvedCustomerName);

        public bool CanSubmit => HasCustomerSelection && FilledEntries.Any() && FilledEntries.All(entry => entry.UnitPrice > 0);

        public Visibility EmptyStateVisibility => Categories.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility CustomerPromptVisibility =>
            Categories.Count > 0 && !HasCustomerSelection ? Visibility.Visible : Visibility.Collapsed;

        public Visibility EntryPanelVisibility =>
            Categories.Count > 0 && HasCustomerSelection ? Visibility.Visible : Visibility.Collapsed;

        public Visibility FormVisibility => Categories.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        public Visibility RecentRecordsEmptyVisibility => RecentRecords.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        private IEnumerable<InboundOrderDraftLineModel> FilledEntries => CategoryEntries.Where(entry => entry.HasInput);

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

        public Task LoadAsync() => LoadAsync(clearDraft: false);

        public async Task LoadAsync(bool clearDraft)
        {
            IsBusy = true;

            try
            {
                var selectedCustomerId = SelectedCustomer?.Id;
                var preservedNewCustomerName = NewCustomerName;
                var preservedDraft = clearDraft
                    ? new Dictionary<long, (double Quantity, double UnitPrice)>()
                    : CategoryEntries.ToDictionary(
                        entry => entry.CategoryId,
                        entry => (entry.Quantity, entry.UnitPrice));

                var categories = await _databaseService.GetActiveCategoriesAsync();
                var customers = await _databaseService.GetCustomersAsync();
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

                SelectedCustomer = customers.FirstOrDefault(customer => customer.Id == selectedCustomerId);
                NewCustomerName = preservedNewCustomerName;

                RebuildCategoryEntries(categories, preservedDraft, clearDraft);

                TodayInboundAmount = Currency(summary.TodayInboundAmount);
                CategoryCountText = summary.CategoryCount.ToString(CultureInfo.InvariantCulture);
                RecentRecordCountText = recentRecords.Count.ToString(CultureInfo.InvariantCulture);

                RaiseComputedStateChanged();
                OnPropertyChanged(nameof(EmptyStateVisibility));
                OnPropertyChanged(nameof(CustomerPromptVisibility));
                OnPropertyChanged(nameof(EntryPanelVisibility));
                OnPropertyChanged(nameof(FormVisibility));
                OnPropertyChanged(nameof(RecentRecordsEmptyVisibility));
            }
            finally
            {
                IsBusy = false;
            }
        }

        public async Task<InboundOrderConfirmationModel> SubmitAsync()
        {
            if (!HasCustomerSelection)
            {
                throw new InvalidOperationException("\u8BF7\u5148\u9009\u62E9\u6216\u65B0\u5EFA\u5BA2\u6237\u3002");
            }

            var lines = FilledEntries
                .Select(entry => new InboundOrderLineInputModel
                {
                    CategoryId = entry.CategoryId,
                    Quantity = entry.NormalizedQuantity,
                    UnitPrice = entry.UnitPrice
                })
                .ToList();

            if (lines.Count == 0)
            {
                throw new InvalidOperationException("\u8BF7\u81F3\u5C11\u586B\u5199\u4E00\u4E2A\u5206\u7C7B\u7684\u5165\u5E93\u6570\u91CF\u3002");
            }

            if (lines.Any(line => line.UnitPrice <= 0))
            {
                throw new InvalidOperationException("\u6240\u6709\u5DF2\u586B\u5199\u7684\u5165\u5E93\u660E\u7EC6\u90FD\u5FC5\u987B\u8BBE\u7F6E\u5927\u4E8E 0 \u7684\u5355\u4EF7\u3002");
            }

            var customerName = ResolvedCustomerName;
            var confirmation = await _databaseService.AddInboundOrderAsync(customerName, lines);

            NewCustomerName = string.Empty;
            StatusMessage = "\u5165\u5E93\u5DF2\u4FDD\u5B58\uff0C\u5E93\u5B58\u548C\u5165\u5E93\u8BA2\u5355\u5DF2\u66F4\u65B0\u3002";

            await LoadAsync(clearDraft: true);

            SelectedCustomer = Customers.FirstOrDefault(customer =>
                string.Equals(customer.Name, customerName, StringComparison.OrdinalIgnoreCase));

            return confirmation;
        }

        private void RebuildCategoryEntries(
            IReadOnlyList<CategoryModel> categories,
            IReadOnlyDictionary<long, (double Quantity, double UnitPrice)> preservedDraft,
            bool clearDraft)
        {
            foreach (var entry in CategoryEntries)
            {
                entry.PropertyChanged -= OnDraftLinePropertyChanged;
            }

            CategoryEntries.Clear();

            foreach (var category in categories)
            {
                var suggestedUnitPrice = GetRememberedPrice(category.Id) ?? category.BuyPrice;
                var entry = new InboundOrderDraftLineModel(category, suggestedUnitPrice);

                if (!clearDraft && preservedDraft.TryGetValue(category.Id, out var draft))
                {
                    entry.Quantity = draft.Quantity;
                    entry.UnitPrice = draft.UnitPrice > 0 ? draft.UnitPrice : suggestedUnitPrice;
                }

                entry.PropertyChanged += OnDraftLinePropertyChanged;
                CategoryEntries.Add(entry);
            }
        }

        private void ApplySuggestedUnitPrices()
        {
            foreach (var entry in CategoryEntries)
            {
                var rememberedPrice = GetRememberedPrice(entry.CategoryId) ?? entry.Category.BuyPrice;
                entry.ApplySuggestedUnitPrice(rememberedPrice);
            }
        }

        private double? GetRememberedPrice(long categoryId)
        {
            var customerId = ResolveCustomerId();
            if (!customerId.HasValue)
            {
                return null;
            }

            return _priceMemories
                .FirstOrDefault(memory => memory.CustomerId == customerId.Value && memory.CategoryId == categoryId)
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

        private void OnDraftLinePropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName is nameof(InboundOrderDraftLineModel.Quantity)
                or nameof(InboundOrderDraftLineModel.UnitPrice)
                or nameof(InboundOrderDraftLineModel.HasInput)
                or nameof(InboundOrderDraftLineModel.LineAmount))
            {
                RaiseComputedStateChanged();
            }
        }

        private void RaiseComputedStateChanged()
        {
            OnPropertyChanged(nameof(EffectiveCustomerText));
            OnPropertyChanged(nameof(FilledCategoryCountText));
            OnPropertyChanged(nameof(QuantitySummaryText));
            OnPropertyChanged(nameof(TotalCostText));
            OnPropertyChanged(nameof(HasCustomerSelection));
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(CustomerPromptVisibility));
            OnPropertyChanged(nameof(EntryPanelVisibility));
        }

        private static string Currency(double value) => $"\u00A5{value:0.##}";
    }
}
