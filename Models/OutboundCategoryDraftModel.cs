namespace ClothingRecycler.Desktop.Models
{
    public sealed class OutboundCategoryDraftModel : ObservableObject
    {
        private bool _isExpanded;
        private CustomerModel? _selectedCustomer;
        private string _newCustomerName = string.Empty;
        private WeightUnit _selectedInputUnit;
        private readonly IReadOnlyList<WeightUnitOptionModel> _inputUnitOptions;
        private double _quantity;
        private double _unitPrice;
        private double _baseSuggestedUnitPrice;

        public OutboundCategoryDraftModel(CategoryModel category, double suggestedUnitPrice)
        {
            Category = category;
            _selectedInputUnit = category.UnitType;
            _inputUnitOptions = WeightUnitHelper.GetInputOptions(category.UnitType);
            _baseSuggestedUnitPrice = Math.Max(0, suggestedUnitPrice);
            _unitPrice = WeightUnitHelper.ConvertUnitPrice(_baseSuggestedUnitPrice, category.UnitType, _selectedInputUnit);
        }

        public CategoryModel Category { get; }

        public IReadOnlyList<WeightUnitOptionModel> InputUnitOptions => _inputUnitOptions;

        public WeightUnitOptionModel? SelectedInputUnitOption
        {
            get => InputUnitOptions.FirstOrDefault(option => option.UnitType == SelectedInputUnit);
            set
            {
                if (value is not null)
                {
                    SelectedInputUnit = value.UnitType;
                }
            }
        }

        public long CategoryId => Category.Id;

        public string CategoryName => Category.Name;

        public string CurrentStockText => Category.DisplayStockText;

        public string StockUnitLabel => Category.UnitLabel;

        public WeightUnit SelectedInputUnit
        {
            get => _selectedInputUnit;
            set
            {
                if (!WeightUnitHelper.SupportsInputUnit(Category.UnitType, value))
                {
                    value = Category.UnitType;
                }

                var previousUnit = _selectedInputUnit;
                if (SetProperty(ref _selectedInputUnit, value))
                {
                    _unitPrice = WeightUnitHelper.ConvertUnitPrice(_unitPrice, previousUnit, value);
                    OnPropertyChanged(nameof(UnitPrice));
                    OnPropertyChanged(nameof(SuggestedUnitPrice));
                    OnPropertyChanged(nameof(SuggestedPriceText));
                    OnPropertyChanged(nameof(InputUnitLabel));
                    OnPropertyChanged(nameof(SelectedInputUnitOption));
                    RaiseComputedStateChanged();
                }
            }
        }

        public string InputUnitLabel => WeightUnitHelper.GetLabel(SelectedInputUnit);

        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (SetProperty(ref _isExpanded, value))
                {
                    OnPropertyChanged(nameof(ExpandedVisibility));
                    OnPropertyChanged(nameof(ExpandActionText));
                }
            }
        }

        public CustomerModel? SelectedCustomer
        {
            get => _selectedCustomer;
            set
            {
                if (SetProperty(ref _selectedCustomer, value))
                {
                    RaiseCustomerStateChanged();
                }
            }
        }

        public string NewCustomerName
        {
            get => _newCustomerName;
            set
            {
                if (SetProperty(ref _newCustomerName, value))
                {
                    RaiseCustomerStateChanged();
                }
            }
        }

        public double Quantity
        {
            get => _quantity;
            set
            {
                if (SetProperty(ref _quantity, Math.Max(0, value)))
                {
                    RaiseComputedStateChanged();
                }
            }
        }

        public double UnitPrice
        {
            get => _unitPrice;
            set
            {
                if (SetProperty(ref _unitPrice, Math.Max(0, value)))
                {
                    RaiseComputedStateChanged();
                }
            }
        }

        public double SuggestedUnitPrice => WeightUnitHelper.ConvertUnitPrice(_baseSuggestedUnitPrice, Category.UnitType, SelectedInputUnit);

        public Visibility ExpandedVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;

        public string ExpandActionText => IsExpanded ? "收起编辑" : "展开编辑";

        public string EffectiveCustomerText => string.IsNullOrWhiteSpace(ResolvedCustomerName)
            ? "匿名出库"
            : ResolvedCustomerName;

        public string CustomerHintText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(NewCustomerName.Trim()))
                {
                    return "将优先使用新客户名称，并在提交后自动建立客户档案。";
                }

                if (SelectedCustomer is not null)
                {
                    return "已选择现有客户，会优先带出该客户此分类最近一次成交价。";
                }

                return "如果不选客户也不新增客户，将按匿名出库处理。";
            }
        }

        public double NormalizedQuantity => WeightUnitHelper.NormalizeQuantity(Quantity, SelectedInputUnit);

        public double StockQuantityInCategoryUnit => WeightUnitHelper.NormalizeQuantity(Category.DisplayStock, Category.UnitType);

        public double ConvertedQuantity => WeightUnitHelper.ConvertQuantity(NormalizedQuantity, SelectedInputUnit, Category.UnitType);

        public double TotalRevenue => Math.Round(NormalizedQuantity * Math.Max(0, UnitPrice), 2);

        public bool HasEnoughStock => ConvertedQuantity <= StockQuantityInCategoryUnit + 0.0001;

        public bool CanSubmit => NormalizedQuantity > 0 && UnitPrice > 0 && HasEnoughStock;

        public string SuggestedPriceText => $"{Currency(SuggestedUnitPrice)} / {InputUnitLabel}";

        public string TotalRevenueText => Currency(TotalRevenue);

        public string StockValidationText
        {
            get
            {
                if (NormalizedQuantity <= 0)
                {
                    return $"请先填写要出库的数量，本次录单单位为 {InputUnitLabel}，库存统一按 {StockUnitLabel} 管理。";
                }

                if (SelectedInputUnit == Category.UnitType)
                {
                    return HasEnoughStock
                        ? $"库存充足，当前可出库库存为 {Category.DisplayStockText}。"
                        : $"库存不足，当前仅剩 {Category.DisplayStockText}。";
                }

                var convertedText = Category.UnitType == WeightUnit.Piece
                    ? $"{Math.Round(ConvertedQuantity):0} {StockUnitLabel}"
                    : $"{ConvertedQuantity:0.##} {StockUnitLabel}";

                return HasEnoughStock
                    ? $"当前库存为 {Category.DisplayStockText}，本次录入 {NormalizedQuantity:0.##} {InputUnitLabel}，折合 {convertedText}。"
                    : $"库存不足，当前库存为 {Category.DisplayStockText}，本次录入折合 {convertedText}。";
            }
        }

        public string ResolvedCustomerName
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

        public void ApplySuggestedUnitPrice(double canonicalUnitPrice)
        {
            _baseSuggestedUnitPrice = Math.Max(0, canonicalUnitPrice);
            OnPropertyChanged(nameof(SuggestedUnitPrice));
            OnPropertyChanged(nameof(SuggestedPriceText));
            UnitPrice = WeightUnitHelper.ConvertUnitPrice(_baseSuggestedUnitPrice, Category.UnitType, SelectedInputUnit);
        }

        public void RestoreDraft(double quantity, double unitPrice, WeightUnit inputUnitType)
        {
            SelectedInputUnit = inputUnitType;
            Quantity = quantity;
            UnitPrice = unitPrice > 0
                ? Math.Max(0, unitPrice)
                : SuggestedUnitPrice;
        }

        private void RaiseCustomerStateChanged()
        {
            OnPropertyChanged(nameof(EffectiveCustomerText));
            OnPropertyChanged(nameof(CustomerHintText));
        }

        private void RaiseComputedStateChanged()
        {
            OnPropertyChanged(nameof(NormalizedQuantity));
            OnPropertyChanged(nameof(StockQuantityInCategoryUnit));
            OnPropertyChanged(nameof(ConvertedQuantity));
            OnPropertyChanged(nameof(TotalRevenue));
            OnPropertyChanged(nameof(TotalRevenueText));
            OnPropertyChanged(nameof(HasEnoughStock));
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(StockValidationText));
        }

        private static string Currency(double value) => $"¥{value:0.##}";
    }
}
