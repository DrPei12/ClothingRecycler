namespace ClothingRecycler.Desktop.Controls
{
    public sealed partial class OrderEditorDialog : ContentDialog
    {
        private readonly OrderEditModel _originalOrder;
        private readonly IReadOnlyList<CategoryModel> _categories;
        private readonly IReadOnlyList<CustomerModel> _customers;
        private readonly IReadOnlyList<CustomerCategoryPriceModel> _priceMemories;
        private bool _isInitializing;
        private bool _isAdjustingUnitSelection;
        private WeightUnit _selectedInputUnit;

        public OrderEditorDialog(
            OrderEditModel order,
            IReadOnlyList<CategoryModel> categories,
            IReadOnlyList<CustomerModel> customers,
            IReadOnlyList<CustomerCategoryPriceModel> priceMemories)
        {
            InitializeComponent();
            _originalOrder = order;
            _categories = categories;
            _customers = customers;
            _priceMemories = priceMemories;
            _selectedInputUnit = order.UnitType;

            Title = $"\u7F16\u8F91{order.TypeText}\u8BA2\u5355";
            OrderNumberTextBlock.Text = order.OrderNumber;
            OrderTypeTextBlock.Text = order.TypeText;

            _isInitializing = true;
            CategoryComboBox.ItemsSource = _categories;
            CustomerComboBox.ItemsSource = _customers;

            CategoryComboBox.SelectedItem = _categories.FirstOrDefault(category => category.Id == order.CategoryId);
            CustomerComboBox.SelectedItem = order.CustomerId.HasValue
                ? _customers.FirstOrDefault(customer => customer.Id == order.CustomerId.Value)
                : null;

            if (!order.CustomerId.HasValue && !string.IsNullOrWhiteSpace(order.CustomerName))
            {
                NewCustomerNameTextBox.Text = order.CustomerName;
            }

            QuantityBox.Value = order.Quantity;
            UnitPriceBox.Value = order.UnitPrice;
            UpdateInputUnitOptions(preferredUnit: order.UnitType, convertExistingPrice: false);
            _isInitializing = false;

            UpdateSummary();
        }

        public OrderEditModel? Result { get; private set; }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var category = CategoryComboBox.SelectedItem as CategoryModel;
            if (category is null)
            {
                ShowError("\u8BF7\u5148\u9009\u62E9\u5206\u7C7B\u3002");
                args.Cancel = true;
                return;
            }

            var inputUnitType = GetSelectedInputUnit(category.UnitType);
            var normalizedQuantity = NormalizeQuantity(inputUnitType);
            if (normalizedQuantity <= 0)
            {
                ShowError("\u6570\u91CF\u5FC5\u987B\u5927\u4E8E 0\u3002");
                args.Cancel = true;
                return;
            }

            if (UnitPriceBox.Value <= 0)
            {
                ShowError("\u5355\u4EF7\u5FC5\u987B\u5927\u4E8E 0\u3002");
                args.Cancel = true;
                return;
            }

            if (_originalOrder.IsOutbound && !HasEnoughStock(category, normalizedQuantity))
            {
                ShowError("\u4FEE\u6539\u540E\u5E93\u5B58\u4E0D\u8DB3\uff0C\u8BF7\u8C03\u6574\u5206\u7C7B\u6216\u6570\u91CF\u3002");
                args.Cancel = true;
                return;
            }

            var resolvedCustomer = ResolveCustomer();
            var resolvedCustomerName = ResolveCustomerName();

            Result = new OrderEditModel
            {
                OrderId = _originalOrder.OrderId,
                OrderNumber = _originalOrder.OrderNumber,
                Type = _originalOrder.Type,
                CustomerId = resolvedCustomer?.Id,
                CustomerName = resolvedCustomerName,
                CategoryId = category.Id,
                Quantity = normalizedQuantity,
                UnitPrice = UnitPriceBox.Value,
                UnitType = inputUnitType,
                Timestamp = _originalOrder.Timestamp
            };
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox comboBox && comboBox == CategoryComboBox)
            {
                UpdateInputUnitOptions();
            }

            if (_isInitializing)
            {
                UpdateSummary();
                return;
            }

            ApplyRememberedPrice();
            UpdateSummary();
        }

        private void OnCustomerNameTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing)
            {
                UpdateSummary();
                return;
            }

            ApplyRememberedPrice();
            UpdateSummary();
        }

        private void OnNumberValueChanged(NumberBox sender, NumberBoxValueChangedEventArgs args)
        {
            UpdateSummary();
        }

        private void OnInputUnitSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var category = CategoryComboBox.SelectedItem as CategoryModel;
            if (category is null)
            {
                return;
            }

            var previousUnit = _selectedInputUnit;
            var nextUnit = GetSelectedInputUnit(category.UnitType);
            if (_isInitializing || _isAdjustingUnitSelection || previousUnit == nextUnit)
            {
                _selectedInputUnit = nextUnit;
                UpdateSummary();
                return;
            }

            UnitPriceBox.Value = WeightUnitHelper.ConvertUnitPrice(UnitPriceBox.Value, previousUnit, nextUnit);
            _selectedInputUnit = nextUnit;
            UpdateSummary();
        }

        private void ApplyRememberedPrice()
        {
            var category = CategoryComboBox.SelectedItem as CategoryModel;
            if (category is null)
            {
                return;
            }

            var customer = ResolveCustomer();
            if (customer is null)
            {
                return;
            }

            var rememberedPrice = _priceMemories
                .OrderByDescending(memory => memory.UpdatedAt)
                .FirstOrDefault(memory => memory.CustomerId == customer.Id && memory.CategoryId == category.Id)
                ?.Price;

            if (rememberedPrice.HasValue)
            {
                UnitPriceBox.Value = WeightUnitHelper.ConvertUnitPrice(
                    rememberedPrice.Value,
                    category.UnitType,
                    GetSelectedInputUnit(category.UnitType));
            }
        }

        private void UpdateSummary()
        {
            ValidationInfoBar.IsOpen = false;

            var category = CategoryComboBox.SelectedItem as CategoryModel;
            if (category is null)
            {
                CurrentStockTextBlock.Text = "-";
                UnitTextBlock.Text = "-";
                TotalAmountTextBlock.Text = "\u00A50";
                CustomerTextBlock.Text = "\u5F53\u524D\u5BA2\u6237\uff1A\u533F\u540D";
                ValidationTextBlock.Text = "\u8BF7\u5148\u9009\u62E9\u5206\u7C7B\u3002";
                return;
            }

            var inputUnitType = GetSelectedInputUnit(category.UnitType);
            var normalizedQuantity = NormalizeQuantity(inputUnitType);
            var totalAmount = Math.Round(normalizedQuantity * Math.Max(0, UnitPriceBox.Value), 2);
            var convertedQuantity = WeightUnitHelper.ConvertQuantity(normalizedQuantity, inputUnitType, category.UnitType);

            CurrentStockTextBlock.Text = GetAvailableStockText(category);
            UnitTextBlock.Text = $"{category.UnitLabel} \uFF08\u5E93\u5B58\u7EDF\u4E00\u5355\u4F4D\uFF09";
            UnitPriceBox.Header = $"\u5355\u4EF7\uFF08{WeightUnitHelper.GetLabel(inputUnitType)}\uFF09";
            TotalAmountTextBlock.Text = $"\u00A5{totalAmount:0.##}";
            CustomerTextBlock.Text = $"\u5F53\u524D\u5BA2\u6237\uff1A{ResolveCustomerDisplayText()}";

            if (_originalOrder.IsOutbound)
            {
                ValidationTextBlock.Text = HasEnoughStock(category, normalizedQuantity)
                    ? $"\u7EA0\u9519\u540E\u5E93\u5B58\u5145\u8DB3\uff0C\u672C\u6B21\u8F93\u5165 {normalizedQuantity:0.##} {WeightUnitHelper.GetLabel(inputUnitType)}\uFF0C\u6298\u5408 {convertedQuantity:0.##} {category.UnitLabel}\u3002"
                    : $"\u7EA0\u9519\u540E\u5E93\u5B58\u4E0D\u8DB3\uff0C\u672C\u6B21\u8F93\u5165\u6298\u5408 {convertedQuantity:0.##} {category.UnitLabel}\uFF0C\u8BF7\u91CD\u65B0\u8C03\u6574\u3002";
                return;
            }

            ValidationTextBlock.Text = $"\u4FDD\u5B58\u540E\u4F1A\u6309 {normalizedQuantity:0.##} {WeightUnitHelper.GetLabel(inputUnitType)} \u91CD\u65B0\u5165\u8D26\uFF0C\u5E93\u5B58\u4ECD\u7EDF\u4E00\u6309 {category.UnitLabel} \u663E\u793A\u3002";
        }

        private string GetAvailableStockText(CategoryModel category)
        {
            var availableStock = category.DisplayStock;
            if (_originalOrder.IsOutbound && category.Id == _originalOrder.CategoryId)
            {
                availableStock += WeightUnitHelper.ConvertQuantity(_originalOrder.Quantity, _originalOrder.UnitType, category.UnitType);
            }

            return category.UnitType == WeightUnit.Piece
                ? $"{Math.Round(availableStock):0} {category.UnitLabel}"
                : $"{availableStock:0.##} {category.UnitLabel}";
        }

        private bool HasEnoughStock(CategoryModel category, double quantity)
        {
            var availableStock = category.DisplayStock;
            if (category.Id == _originalOrder.CategoryId)
            {
                availableStock += WeightUnitHelper.ConvertQuantity(_originalOrder.Quantity, _originalOrder.UnitType, category.UnitType);
            }

            var convertedQuantity = WeightUnitHelper.ConvertQuantity(quantity, GetSelectedInputUnit(category.UnitType), category.UnitType);
            return convertedQuantity <= availableStock + 0.0001;
        }

        private CustomerModel? ResolveCustomer()
        {
            var trimmedNewName = NewCustomerNameTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedNewName))
            {
                return _customers.FirstOrDefault(customer =>
                    string.Equals(customer.Name, trimmedNewName, StringComparison.OrdinalIgnoreCase));
            }

            return CustomerComboBox.SelectedItem as CustomerModel;
        }

        private string ResolveCustomerName()
        {
            var trimmedNewName = NewCustomerNameTextBox.Text.Trim();
            if (!string.IsNullOrWhiteSpace(trimmedNewName))
            {
                return trimmedNewName;
            }

            return (CustomerComboBox.SelectedItem as CustomerModel)?.Name?.Trim() ?? string.Empty;
        }

        private string ResolveCustomerDisplayText()
        {
            var customerName = ResolveCustomerName();
            return string.IsNullOrWhiteSpace(customerName) ? "\u533F\u540D" : customerName;
        }

        private double NormalizeQuantity(WeightUnit inputUnitType)
        {
            var value = Math.Max(0, QuantityBox.Value);
            return inputUnitType == WeightUnit.Piece
                ? Math.Round(value)
                : Math.Round(value, 2);
        }

        private void UpdateInputUnitOptions(WeightUnit? preferredUnit = null, bool convertExistingPrice = false)
        {
            if (CategoryComboBox.SelectedItem is not CategoryModel category)
            {
                InputUnitComboBox.ItemsSource = null;
                return;
            }

            var previousUnit = _selectedInputUnit;
            var targetUnit = ResolvePreferredInputUnit(category, preferredUnit ?? previousUnit);
            var options = WeightUnitHelper.GetInputOptions(category.UnitType);

            _isAdjustingUnitSelection = true;
            InputUnitComboBox.ItemsSource = options;
            InputUnitComboBox.SelectedItem = options.First(option => option.UnitType == targetUnit);
            _isAdjustingUnitSelection = false;

            if (convertExistingPrice && previousUnit != targetUnit)
            {
                UnitPriceBox.Value = WeightUnitHelper.ConvertUnitPrice(UnitPriceBox.Value, previousUnit, targetUnit);
            }

            _selectedInputUnit = targetUnit;
        }

        private WeightUnit GetSelectedInputUnit(WeightUnit categoryUnitType)
        {
            if (InputUnitComboBox.SelectedItem is WeightUnitOptionModel option
                && WeightUnitHelper.SupportsInputUnit(categoryUnitType, option.UnitType))
            {
                return option.UnitType;
            }

            return ResolvePreferredInputUnit(categoryUnitType, _selectedInputUnit);
        }

        private WeightUnit ResolvePreferredInputUnit(CategoryModel category, WeightUnit? preferredUnit = null) =>
            ResolvePreferredInputUnit(category.UnitType, preferredUnit);

        private static WeightUnit ResolvePreferredInputUnit(WeightUnit categoryUnitType, WeightUnit? preferredUnit)
        {
            if (preferredUnit.HasValue && WeightUnitHelper.SupportsInputUnit(categoryUnitType, preferredUnit.Value))
            {
                return preferredUnit.Value;
            }

            return categoryUnitType;
        }

        private void ShowError(string message)
        {
            ValidationInfoBar.Message = message;
            ValidationInfoBar.IsOpen = true;
        }
    }
}
