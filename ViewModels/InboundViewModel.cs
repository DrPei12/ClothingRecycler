namespace ClothingRecycler.Desktop.ViewModels
{
    public sealed class InboundViewModel : ViewModelBase
    {
        private readonly LocalDatabaseService _databaseService;
        private readonly LocalAiDraftAgentService _localAiDraftAgentService;
        private readonly List<CustomerCategoryPriceModel> _priceMemories = [];
        private readonly record struct InboundDraftSnapshot(long? CategoryId, WeightUnit InputUnitType, double Quantity, double UnitPrice);

        private CustomerModel? _selectedCustomer;
        private string _newCustomerName = string.Empty;
        private string _todayInboundAmount = "\u00A50";
        private string _categoryCountText = "0";
        private string _recentRecordCountText = "0";

        public InboundViewModel(LocalDatabaseService databaseService, LocalAiDraftAgentService localAiDraftAgentService)
        {
            _databaseService = databaseService;
            _localAiDraftAgentService = localAiDraftAgentService;
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
                        .GroupBy(entry => entry.UnitType)
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

        public bool CanAddCategoryEntry =>
            HasCustomerSelection
            && Categories.Count > 0
            && CategoryEntries.Count < Categories.Count
            && CategoryEntries.All(entry => entry.HasSelectedCategory);

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
                    ? new List<InboundDraftSnapshot>()
                    : CategoryEntries
                        .Select(entry => new InboundDraftSnapshot(entry.CategoryId, entry.SelectedInputUnit, entry.Quantity, entry.UnitPrice))
                        .ToList();

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

        public async Task<AiOrderDraftSuggestion> SuggestAndApplyAiDraftAsync(
            string userInput,
            AiLocalProviderKind providerKind,
            CancellationToken cancellationToken = default)
        {
            if (Categories.Count == 0 || Customers.Count == 0)
            {
                await LoadAsync(clearDraft: false);
            }

            var suggestion = await _localAiDraftAgentService.SuggestInboundDraftAsync(userInput, providerKind, cancellationToken);
            await ApplyAiSuggestionAsync(suggestion);
            return suggestion;
        }

        public async Task ApplyAiSuggestionAsync(AiOrderDraftSuggestion suggestion)
        {
            ArgumentNullException.ThrowIfNull(suggestion);

            if (suggestion.Operation != AiDraftOperation.Inbound)
            {
                throw new InvalidOperationException("AI suggestion operation does not match inbound drafting.");
            }

            await LoadAsync(clearDraft: true);

            ApplyAiCustomerSuggestion(suggestion);
            var snapshots = BuildAiDraftSnapshots(suggestion);
            RebuildCategoryEntries(Categories, snapshots, clearDraft: false);

            var skippedLineCount = Math.Max(0, suggestion.Lines.Count - snapshots.Count);
            StatusMessage = BuildAiDraftStatusMessage("AI inbound draft applied.", snapshots.Count, skippedLineCount, suggestion);
            RaiseComputedStateChanged();
        }

        public async Task<PendingInboundOrderModel> PrepareSubmitAsync()
        {
            var customerName = ResolveCustomerNameOrThrow();
            var lines = BuildValidatedLines();
            return await _databaseService.PrepareInboundOrderAsync(customerName, lines);
        }

        public async Task<InboundOrderConfirmationModel> ConfirmSubmitAsync(PendingInboundOrderModel pendingOrder)
        {
            ArgumentNullException.ThrowIfNull(pendingOrder);

            var confirmation = await _databaseService.ConfirmInboundOrderAsync(pendingOrder);
            var customerName = pendingOrder.CustomerName;

            NewCustomerName = string.Empty;
            StatusMessage = "\u5165\u5E93\u5DF2\u4FDD\u5B58\uff0C\u5E93\u5B58\u548C\u5165\u5E93\u8BA2\u5355\u5DF2\u66F4\u65B0\u3002";

            await LoadAsync(clearDraft: true);

            SelectedCustomer = Customers.FirstOrDefault(customer =>
                string.Equals(customer.Name, customerName, StringComparison.OrdinalIgnoreCase));

            return confirmation;
        }

        public async Task<InboundOrderConfirmationModel> SubmitAsync()
        {
            var pendingOrder = await PrepareSubmitAsync();
            return await ConfirmSubmitAsync(pendingOrder);
        }

        public void AddCategoryEntry()
        {
            if (!CanAddCategoryEntry)
            {
                return;
            }

            CategoryEntries.Add(CreateCategoryEntry(Categories));
            RaiseComputedStateChanged();
        }

        public void RemoveCategoryEntry(InboundOrderDraftLineModel entry)
        {
            if (!CategoryEntries.Contains(entry))
            {
                return;
            }

            if (CategoryEntries.Count == 1)
            {
                entry.SelectedCategory = null;
                RaiseComputedStateChanged();
                return;
            }

            entry.PropertyChanged -= OnDraftLinePropertyChanged;
            CategoryEntries.Remove(entry);
            RaiseComputedStateChanged();
        }

        private void RebuildCategoryEntries(
            IReadOnlyList<CategoryModel> categories,
            IReadOnlyList<InboundDraftSnapshot> preservedDraft,
            bool clearDraft)
        {
            foreach (var entry in CategoryEntries)
            {
                entry.PropertyChanged -= OnDraftLinePropertyChanged;
            }

            CategoryEntries.Clear();

            var rowsToRestore = !clearDraft && preservedDraft.Count > 0
                ? preservedDraft
                    : new List<InboundDraftSnapshot> { new(null, WeightUnit.Kilogram, 0, 0) };

            foreach (var draft in rowsToRestore)
            {
                CategoryEntries.Add(CreateCategoryEntry(categories, draft));
            }
        }

        private void ApplySuggestedUnitPrices()
        {
            foreach (var entry in CategoryEntries)
            {
                if (!entry.CategoryId.HasValue || entry.SelectedCategory is null)
                {
                    continue;
                }

                var rememberedPrice = GetRememberedPrice(entry.CategoryId.Value) ?? entry.SelectedCategory.BuyPrice;
                entry.ApplySuggestedUnitPrice(rememberedPrice);
            }
        }

        private void ApplyAiCustomerSuggestion(AiOrderDraftSuggestion suggestion)
        {
            SelectedCustomer = null;
            NewCustomerName = string.Empty;

            CustomerModel? matchedCustomer = null;
            if (suggestion.ExistingCustomerId.HasValue)
            {
                matchedCustomer = Customers.FirstOrDefault(customer => customer.Id == suggestion.ExistingCustomerId.Value);
            }

            matchedCustomer ??= Customers.FirstOrDefault(customer =>
                string.Equals(customer.Name, suggestion.CustomerName?.Trim(), StringComparison.OrdinalIgnoreCase));

            if (matchedCustomer is not null)
            {
                SelectedCustomer = matchedCustomer;
                return;
            }

            if (!string.IsNullOrWhiteSpace(suggestion.CustomerName))
            {
                NewCustomerName = suggestion.CustomerName.Trim();
            }
        }

        private List<InboundDraftSnapshot> BuildAiDraftSnapshots(AiOrderDraftSuggestion suggestion)
        {
            var snapshots = new List<InboundDraftSnapshot>();
            var addedCategoryIds = new HashSet<long>();

            foreach (var line in suggestion.Lines)
            {
                var category = ResolveCategoryForSuggestion(line);
                if (category is null || !addedCategoryIds.Add(category.Id))
                {
                    continue;
                }

                var inputUnit = line.InputUnitType.HasValue && WeightUnitHelper.SupportsInputUnit(category.UnitType, line.InputUnitType.Value)
                    ? line.InputUnitType.Value
                    : category.UnitType;

                snapshots.Add(new InboundDraftSnapshot(
                    category.Id,
                    inputUnit,
                    Math.Max(0, line.Quantity ?? 0),
                    Math.Max(0, line.UnitPrice ?? 0)));
            }

            return snapshots;
        }

        private CategoryModel? ResolveCategoryForSuggestion(AiOrderDraftLineSuggestion line)
        {
            if (line.ExistingCategoryId.HasValue)
            {
                return Categories.FirstOrDefault(category => category.Id == line.ExistingCategoryId.Value);
            }

            if (string.IsNullOrWhiteSpace(line.CategoryName))
            {
                return null;
            }

            return Categories.FirstOrDefault(category =>
                       string.Equals(category.Name, line.CategoryName.Trim(), StringComparison.OrdinalIgnoreCase))
                   ?? Categories.FirstOrDefault(category =>
                       category.Name.Contains(line.CategoryName.Trim(), StringComparison.OrdinalIgnoreCase));
        }

        private static string BuildAiDraftStatusMessage(
            string prefix,
            int appliedLineCount,
            int skippedLineCount,
            AiOrderDraftSuggestion suggestion)
        {
            var parts = new List<string>
            {
                $"{prefix} Applied {appliedLineCount} line(s)."
            };

            if (skippedLineCount > 0)
            {
                parts.Add($"{skippedLineCount} line(s) still need manual review.");
            }

            if (suggestion.MissingFields.Count > 0)
            {
                parts.Add($"Missing: {string.Join(", ", suggestion.MissingFields)}.");
            }

            if (suggestion.Warnings.Count > 0)
            {
                parts.Add($"Warnings: {string.Join(" | ", suggestion.Warnings.Take(3))}");
            }

            return string.Join(" ", parts);
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
            if (sender is InboundOrderDraftLineModel entry
                && e.PropertyName is nameof(InboundOrderDraftLineModel.SelectedCategory))
            {
                if (entry.CategoryId.HasValue && entry.SelectedCategory is not null)
                {
                    var rememberedPrice = GetRememberedPrice(entry.CategoryId.Value) ?? entry.SelectedCategory.BuyPrice;
                    entry.ApplySuggestedUnitPrice(rememberedPrice);
                }
            }

            if (e.PropertyName is nameof(InboundOrderDraftLineModel.Quantity)
                or nameof(InboundOrderDraftLineModel.UnitPrice)
                or nameof(InboundOrderDraftLineModel.HasInput)
                or nameof(InboundOrderDraftLineModel.LineAmount)
                or nameof(InboundOrderDraftLineModel.SelectedCategory))
            {
                RaiseComputedStateChanged();
            }

            if (e.PropertyName is nameof(InboundOrderDraftLineModel.SelectedInputUnit))
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
            OnPropertyChanged(nameof(CanAddCategoryEntry));
            OnPropertyChanged(nameof(CustomerPromptVisibility));
            OnPropertyChanged(nameof(EntryPanelVisibility));
        }

        private string ResolveCustomerNameOrThrow()
        {
            if (!HasCustomerSelection)
            {
                throw new InvalidOperationException("\u8BF7\u5148\u9009\u62E9\u6216\u65B0\u5EFA\u5BA2\u6237\u3002");
            }

            return ResolvedCustomerName;
        }

        private List<InboundOrderLineInputModel> BuildValidatedLines()
        {
            var selectedEntries = CategoryEntries
                .Where(entry => entry.HasSelectedCategory)
                .ToList();

            var duplicateCategory = selectedEntries
                .Where(entry => entry.CategoryId.HasValue)
                .GroupBy(entry => entry.CategoryId!.Value)
                .FirstOrDefault(group => group.Count() > 1);

            if (duplicateCategory is not null)
            {
                throw new InvalidOperationException("\u540C\u4E00\u5206\u7C7B\u53EA\u9700\u586B\u5199\u4E00\u6B21\uFF0C\u8BF7\u5220\u9664\u91CD\u590D\u884C\u3002");
            }

            var lines = FilledEntries
                .Select(entry => new InboundOrderLineInputModel
                {
                    CategoryId = entry.CategoryId!.Value,
                    Quantity = entry.NormalizedQuantity,
                    UnitPrice = entry.UnitPrice,
                    InputUnitType = entry.SelectedInputUnit
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

            return lines;
        }

        private InboundOrderDraftLineModel CreateCategoryEntry(
            IReadOnlyList<CategoryModel> categories,
            InboundDraftSnapshot? snapshot = null)
        {
            var selectedCategory = snapshot?.CategoryId.HasValue == true
                ? categories.FirstOrDefault(category => category.Id == snapshot.Value.CategoryId.Value)
                : null;

            var suggestedUnitPrice = selectedCategory is null
                ? 0
                : GetRememberedPrice(selectedCategory.Id) ?? selectedCategory.BuyPrice;

            var entry = new InboundOrderDraftLineModel(categories, selectedCategory, suggestedUnitPrice);
            entry.PropertyChanged += OnDraftLinePropertyChanged;

            if (selectedCategory is not null && snapshot.HasValue)
            {
                entry.RestoreInput(snapshot.Value.Quantity, snapshot.Value.UnitPrice, snapshot.Value.InputUnitType);
            }

            return entry;
        }

        private static string Currency(double value) => $"\u00A5{value:0.##}";
    }
}
