namespace ClothingRecycler.Desktop.Models
{
    public sealed class InboundOrderDraftLineModel : ObservableObject
    {
        private CategoryModel? _selectedCategory;
        private WeightUnit _selectedInputUnit = WeightUnit.Kilogram;
        private IReadOnlyList<WeightUnitOptionModel> _inputUnitOptions = WeightUnitHelper.GetInputOptions(WeightUnit.Kilogram);
        private double _quantity;
        private double _unitPrice;
        private double _baseSuggestedUnitPrice;

        public InboundOrderDraftLineModel(
            IReadOnlyList<CategoryModel> availableCategories,
            CategoryModel? selectedCategory = null,
            double suggestedUnitPrice = 0)
        {
            AvailableCategories = availableCategories;
            _selectedCategory = selectedCategory;
            _selectedInputUnit = selectedCategory?.UnitType ?? WeightUnit.Kilogram;
            _inputUnitOptions = WeightUnitHelper.GetInputOptions(_selectedInputUnit);
            _baseSuggestedUnitPrice = Math.Max(0, suggestedUnitPrice);
            _unitPrice = selectedCategory is null
                ? 0
                : WeightUnitHelper.ConvertUnitPrice(_baseSuggestedUnitPrice, selectedCategory.UnitType, _selectedInputUnit);
        }

        public IReadOnlyList<CategoryModel> AvailableCategories { get; }

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

        public CategoryModel? SelectedCategory
        {
            get => _selectedCategory;
            set
            {
                if (!SetProperty(ref _selectedCategory, value))
                {
                    return;
                }

                _selectedInputUnit = value?.UnitType ?? WeightUnit.Kilogram;
                _inputUnitOptions = WeightUnitHelper.GetInputOptions(_selectedInputUnit);

                if (value is null)
                {
                    _quantity = 0;
                    _unitPrice = 0;
                    _baseSuggestedUnitPrice = 0;
                }
                else
                {
                    _quantity = 0;
                    _unitPrice = WeightUnitHelper.ConvertUnitPrice(_baseSuggestedUnitPrice, value.UnitType, _selectedInputUnit);
                }

                OnPropertyChanged(nameof(Quantity));
                OnPropertyChanged(nameof(UnitPrice));
                OnPropertyChanged(nameof(SelectedInputUnitOption));
                RaiseCategoryStateChanged();
                RaiseComputedStateChanged();
            }
        }

        public WeightUnit SelectedInputUnit
        {
            get => _selectedInputUnit;
            set
            {
                if (SelectedCategory is null)
                {
                    return;
                }

                if (!WeightUnitHelper.SupportsInputUnit(SelectedCategory.UnitType, value))
                {
                    value = SelectedCategory.UnitType;
                }

                var previousUnit = _selectedInputUnit;
                if (SetProperty(ref _selectedInputUnit, value))
                {
                    _unitPrice = WeightUnitHelper.ConvertUnitPrice(_unitPrice, previousUnit, value);
                    OnPropertyChanged(nameof(UnitPrice));
                    OnPropertyChanged(nameof(SelectedInputUnitOption));
                    RaiseCategoryStateChanged();
                    RaiseComputedStateChanged();
                }
            }
        }

        public bool HasSelectedCategory => SelectedCategory is not null;

        public long? CategoryId => SelectedCategory?.Id;

        public string CategoryName => SelectedCategory?.Name ?? "请选择分类";

        public string CurrentStockText => SelectedCategory is null
            ? "选择分类后显示当前库存"
            : $"当前库存 {SelectedCategory.DisplayStockText}";

        public string UnitLabel => WeightUnitHelper.GetLabel(SelectedInputUnit);

        public string StockUnitLabel => SelectedCategory?.UnitLabel ?? "单位";

        public WeightUnit UnitType => SelectedInputUnit;

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

        public double SuggestedUnitPrice => SelectedCategory is null
            ? 0
            : WeightUnitHelper.ConvertUnitPrice(_baseSuggestedUnitPrice, SelectedCategory.UnitType, SelectedInputUnit);

        public bool HasInput => HasSelectedCategory && NormalizedQuantity > 0;

        public double NormalizedQuantity => SelectedCategory is null
            ? 0
            : WeightUnitHelper.NormalizeQuantity(Quantity, SelectedInputUnit);

        public double LineAmount => Math.Round(NormalizedQuantity * Math.Max(0, UnitPrice), 2);

        public string SuggestedPriceText => SelectedCategory is null
            ? "选择分类后自动带出价格记忆"
            : $"建议单价 {Currency(SuggestedUnitPrice)} / {UnitLabel}";

        public string QuantityText => !HasSelectedCategory
            ? "--"
            : SelectedInputUnit == WeightUnit.Piece
                ? $"{Math.Round(NormalizedQuantity):0} {UnitLabel}"
                : $"{NormalizedQuantity:0.##} {UnitLabel}";

        public string UnitPriceText => Currency(UnitPrice);

        public string LineAmountText => Currency(LineAmount);

        public void ApplySuggestedUnitPrice(double canonicalUnitPrice)
        {
            _baseSuggestedUnitPrice = Math.Max(0, canonicalUnitPrice);
            OnPropertyChanged(nameof(SuggestedUnitPrice));
            OnPropertyChanged(nameof(SuggestedPriceText));

            if (SelectedCategory is not null)
            {
                UnitPrice = WeightUnitHelper.ConvertUnitPrice(_baseSuggestedUnitPrice, SelectedCategory.UnitType, SelectedInputUnit);
            }
        }

        public void RestoreInput(double quantity, double unitPrice, WeightUnit inputUnitType)
        {
            SelectedInputUnit = inputUnitType;
            Quantity = quantity;
            UnitPrice = unitPrice > 0
                ? Math.Max(0, unitPrice)
                : SuggestedUnitPrice;
        }

        public void ClearInput()
        {
            Quantity = 0;
            UnitPrice = SuggestedUnitPrice;
        }

        private void RaiseCategoryStateChanged()
        {
            OnPropertyChanged(nameof(HasSelectedCategory));
            OnPropertyChanged(nameof(CategoryId));
            OnPropertyChanged(nameof(CategoryName));
            OnPropertyChanged(nameof(CurrentStockText));
            OnPropertyChanged(nameof(UnitLabel));
            OnPropertyChanged(nameof(StockUnitLabel));
            OnPropertyChanged(nameof(UnitType));
            OnPropertyChanged(nameof(SelectedInputUnit));
            OnPropertyChanged(nameof(InputUnitOptions));
            OnPropertyChanged(nameof(SelectedInputUnitOption));
            OnPropertyChanged(nameof(SuggestedUnitPrice));
            OnPropertyChanged(nameof(SuggestedPriceText));
            OnPropertyChanged(nameof(QuantityText));
        }

        private void RaiseComputedStateChanged()
        {
            OnPropertyChanged(nameof(HasInput));
            OnPropertyChanged(nameof(NormalizedQuantity));
            OnPropertyChanged(nameof(LineAmount));
            OnPropertyChanged(nameof(QuantityText));
            OnPropertyChanged(nameof(UnitPriceText));
            OnPropertyChanged(nameof(LineAmountText));
        }

        private static string Currency(double value) => $"¥{value:0.##}";
    }
}
