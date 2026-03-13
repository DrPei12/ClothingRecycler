namespace ClothingRecycler.Desktop.Models
{
    public sealed class InboundOrderDraftLineModel : ObservableObject
    {
        private double _quantity;
        private double _unitPrice;
        private double _suggestedUnitPrice;

        public InboundOrderDraftLineModel(CategoryModel category, double suggestedUnitPrice)
        {
            Category = category;
            _suggestedUnitPrice = Math.Max(0, suggestedUnitPrice);
            _unitPrice = _suggestedUnitPrice;
        }

        public CategoryModel Category { get; }

        public long CategoryId => Category.Id;

        public string CategoryName => Category.Name;

        public string CurrentStockText => $"当前库存 {Category.DisplayStockText}";

        public string UnitLabel => Category.UnitLabel;

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

        public double SuggestedUnitPrice
        {
            get => _suggestedUnitPrice;
            private set
            {
                if (SetProperty(ref _suggestedUnitPrice, Math.Max(0, value)))
                {
                    OnPropertyChanged(nameof(SuggestedPriceText));
                }
            }
        }

        public bool HasInput => NormalizedQuantity > 0;

        public double NormalizedQuantity => Category.UnitType == WeightUnit.Piece
            ? Math.Round(Math.Max(0, Quantity))
            : Math.Round(Math.Max(0, Quantity), 2);

        public double LineAmount => Math.Round(NormalizedQuantity * Math.Max(0, UnitPrice), 2);

        public string SuggestedPriceText => $"建议单价 {Currency(SuggestedUnitPrice)}";

        public string QuantityText => Category.UnitType == WeightUnit.Piece
            ? $"{Math.Round(NormalizedQuantity):0} {UnitLabel}"
            : $"{NormalizedQuantity:0.##} {UnitLabel}";

        public string UnitPriceText => Currency(UnitPrice);

        public string LineAmountText => Currency(LineAmount);

        public void ApplySuggestedUnitPrice(double unitPrice)
        {
            SuggestedUnitPrice = unitPrice;
            UnitPrice = unitPrice;
        }

        public void ClearInput()
        {
            Quantity = 0;
            UnitPrice = SuggestedUnitPrice;
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
