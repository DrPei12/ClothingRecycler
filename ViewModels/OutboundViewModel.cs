namespace ClothingRecycler.Desktop.ViewModels
{
    public sealed class OutboundViewModel : ViewModelBase
    {
        private readonly record struct OutboundDraftState(
            long? SelectedCustomerId,
            string NewCustomerName,
            double Quantity,
            double UnitPrice);

        private readonly LocalDatabaseService _databaseService;
        private readonly List<CustomerCategoryPriceModel> _priceMemories = [];

        private string _todayOutboundAmount = "\u00A50";
        private string _categoryCountText = "0";
        private string _recentRecordCountText = "0";

        public OutboundViewModel(LocalDatabaseService databaseService)
        {
            _databaseService = databaseService;
            Title = "\u51FA\u5E93";
        }

        public ObservableCollection<CustomerModel> Customers { get; } = [];

        public ObservableCollection<OutboundRecordModel> RecentRecords { get; } = [];

        public ObservableCollection<OutboundCategoryDraftModel> CategoryEntries { get; } = [];

        public string TodayOutboundAmount
        {
            get => _todayOutboundAmount;
            private set => SetProperty(ref _todayOutboundAmount, value);
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

        public Visibility EmptyStateVisibility => CategoryEntries.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Visibility FormVisibility => CategoryEntries.Count == 0 ? Visibility.Collapsed : Visibility.Visible;

        public Visibility RecentRecordsEmptyVisibility => RecentRecords.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        public Task LoadAsync() => LoadAsync(clearDraft: false);

        public async Task LoadAsync(bool clearDraft)
        {
            IsBusy = true;

            try
            {
                var expandedCategoryId = clearDraft
                    ? (long?)null
                    : CategoryEntries.FirstOrDefault(entry => entry.IsExpanded)?.CategoryId;
                var preservedDrafts = clearDraft
                    ? new Dictionary<long, OutboundDraftState>()
                    : CategoryEntries.ToDictionary(
                        entry => entry.CategoryId,
                        entry => new OutboundDraftState(
                            entry.SelectedCustomer?.Id,
                            entry.NewCustomerName,
                            entry.Quantity,
                            entry.UnitPrice));

                var categories = await _databaseService.GetActiveCategoriesAsync();
                var customers = await _databaseService.GetCustomersAsync();
                var priceMemories = await _databaseService.GetCustomerCategoryPricesAsync();
                var summary = await _databaseService.GetDashboardSummaryAsync();
                var recentRecords = await _databaseService.GetRecentOutboundRecordsAsync(12);

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

                RebuildCategoryEntries(categories, customers, preservedDrafts, expandedCategoryId, clearDraft);

                TodayOutboundAmount = Currency(summary.TodayOutboundAmount);
                CategoryCountText = summary.CategoryCount.ToString(CultureInfo.InvariantCulture);
                RecentRecordCountText = recentRecords.Count.ToString(CultureInfo.InvariantCulture);

                OnPropertyChanged(nameof(EmptyStateVisibility));
                OnPropertyChanged(nameof(FormVisibility));
                OnPropertyChanged(nameof(RecentRecordsEmptyVisibility));
            }
            finally
            {
                IsBusy = false;
            }
        }

        public void ToggleEntry(OutboundCategoryDraftModel entry)
        {
            var shouldExpand = !entry.IsExpanded;

            foreach (var item in CategoryEntries)
            {
                item.IsExpanded = false;
            }

            entry.IsExpanded = shouldExpand;
        }

        public async Task<OutboundOrderConfirmationModel> SubmitAsync(OutboundCategoryDraftModel entry)
        {
            var normalizedQuantity = entry.NormalizedQuantity;
            if (normalizedQuantity <= 0)
            {
                throw new InvalidOperationException("\u51FA\u5E93\u6570\u91CF\u5FC5\u987B\u5927\u4E8E 0\u3002");
            }

            if (entry.UnitPrice <= 0)
            {
                throw new InvalidOperationException("\u51FA\u5E93\u5355\u4EF7\u5FC5\u987B\u5927\u4E8E 0\u3002");
            }

            if (!entry.HasEnoughStock)
            {
                throw new InvalidOperationException("\u51FA\u5E93\u5931\u8D25\uFF0C\u5F53\u524D\u5E93\u5B58\u4E0D\u8DB3\u3002");
            }

            var customerName = entry.ResolvedCustomerName;
            var confirmation = await _databaseService.AddOutboundRecordAsync(entry.CategoryId, normalizedQuantity, entry.UnitPrice, customerName);

            StatusMessage = $"{entry.CategoryName} \u5DF2\u5B8C\u6210\u51FA\u5E93\uFF0C\u5E93\u5B58\u5DF2\u540C\u6B65\u6263\u51CF\u3002";

            await LoadAsync(clearDraft: true);

            var reloadedEntry = CategoryEntries.FirstOrDefault(item => item.CategoryId == entry.CategoryId);
            if (reloadedEntry is not null)
            {
                reloadedEntry.IsExpanded = true;

                if (!string.IsNullOrWhiteSpace(customerName))
                {
                    reloadedEntry.SelectedCustomer = Customers.FirstOrDefault(customer =>
                        string.Equals(customer.Name, customerName, StringComparison.OrdinalIgnoreCase));

                    if (reloadedEntry.SelectedCustomer is null)
                    {
                        reloadedEntry.NewCustomerName = customerName;
                    }
                }
            }

            return confirmation;
        }

        private void RebuildCategoryEntries(
            IReadOnlyList<CategoryModel> categories,
            IReadOnlyList<CustomerModel> customers,
            IReadOnlyDictionary<long, OutboundDraftState> preservedDrafts,
            long? expandedCategoryId,
            bool clearDraft)
        {
            foreach (var entry in CategoryEntries)
            {
                entry.PropertyChanged -= OnEntryPropertyChanged;
            }

            CategoryEntries.Clear();

            foreach (var category in categories)
            {
                var entry = new OutboundCategoryDraftModel(category, category.SellPrice);

                if (!clearDraft && preservedDrafts.TryGetValue(category.Id, out var draft))
                {
                    entry.SelectedCustomer = customers.FirstOrDefault(customer => customer.Id == draft.SelectedCustomerId);
                    entry.NewCustomerName = draft.NewCustomerName;

                    var suggestedUnitPrice = GetRememberedPrice(entry) ?? category.SellPrice;
                    entry.ApplySuggestedUnitPrice(suggestedUnitPrice);
                    entry.Quantity = draft.Quantity;
                    entry.UnitPrice = draft.UnitPrice > 0 ? draft.UnitPrice : suggestedUnitPrice;
                    entry.IsExpanded = expandedCategoryId == category.Id;
                }
                else
                {
                    entry.ApplySuggestedUnitPrice(category.SellPrice);
                }

                entry.PropertyChanged += OnEntryPropertyChanged;
                CategoryEntries.Add(entry);
            }
        }

        private void OnEntryPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (sender is not OutboundCategoryDraftModel entry)
            {
                return;
            }

            if (e.PropertyName is nameof(OutboundCategoryDraftModel.SelectedCustomer)
                or nameof(OutboundCategoryDraftModel.NewCustomerName))
            {
                var rememberedPrice = GetRememberedPrice(entry) ?? entry.Category.SellPrice;
                entry.ApplySuggestedUnitPrice(rememberedPrice);
            }
        }

        private double? GetRememberedPrice(OutboundCategoryDraftModel entry)
        {
            var customerId = ResolveCustomerId(entry);
            if (!customerId.HasValue)
            {
                return null;
            }

            return _priceMemories
                .FirstOrDefault(memory => memory.CustomerId == customerId.Value && memory.CategoryId == entry.CategoryId)
                ?.Price;
        }

        private long? ResolveCustomerId(OutboundCategoryDraftModel entry)
        {
            var trimmedNewName = entry.NewCustomerName.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedNewName))
            {
                return Customers
                    .FirstOrDefault(customer => string.Equals(customer.Name, trimmedNewName, StringComparison.OrdinalIgnoreCase))
                    ?.Id;
            }

            return entry.SelectedCustomer?.Id;
        }

        private static string Currency(double value) => $"\u00A5{value:0.##}";
    }
}
