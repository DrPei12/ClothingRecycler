namespace ClothingRecycler.Desktop.Models
{
    public sealed class OutboundCategoryDraftModel : ObservableObject
    {
        private bool _isExpanded;
        private CustomerModel? _selectedCustomer;
        private string _newCustomerName = string.Empty;
        private double _quantity;
        private double _unitPrice;
        private double _suggestedUnitPrice;

        public OutboundCategoryDraftModel(CategoryModel category, double suggestedUnitPrice)
        {
            Category = category;
            _suggestedUnitPrice = Math.Max(0, suggestedUnitPrice);
            _unitPrice = _suggestedUnitPrice;
        }

        public CategoryModel Category { get; }

        public long CategoryId => Category.Id;

        public string CategoryName => Category.Name;

        public string CurrentStockText => Category.DisplayStockText;

        public string UnitLabel => Category.UnitLabel;

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

        public Visibility ExpandedVisibility => IsExpanded ? Visibility.Visible : Visibility.Collapsed;

        public string ExpandActionText => IsExpanded ? "\u6536\u8D77\u7F16\u8F91" : "\u5C55\u5F00\u7F16\u8F91";

        public string EffectiveCustomerText => string.IsNullOrWhiteSpace(ResolvedCustomerName)
            ? "\u533F\u540D\u51FA\u5E93"
            : ResolvedCustomerName;

        public string CustomerHintText
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(NewCustomerName.Trim()))
                {
                    return "\u5C06\u4F18\u5148\u4F7F\u7528\u65B0\u5BA2\u6237\u540D\u79F0\uFF0C\u5E76\u5728\u63D0\u4EA4\u540E\u81EA\u52A8\u5EFA\u7ACB\u5BA2\u6237\u6863\u6848\u3002";
                }

                if (SelectedCustomer is not null)
                {
                    return "\u5DF2\u9009\u62E9\u73B0\u6709\u5BA2\u6237\uFF0C\u4F1A\u4F18\u5148\u5E26\u51FA\u8BE5\u5BA2\u6237\u6B64\u5206\u7C7B\u6700\u8FD1\u4E00\u6B21\u6210\u4EA4\u4EF7\u3002";
                }

                return "\u5982\u679C\u4E0D\u9009\u5BA2\u6237\u4E5F\u4E0D\u65B0\u589E\u5BA2\u6237\uFF0C\u5C06\u6309\u533F\u540D\u51FA\u5E93\u5904\u7406\u3002";
            }
        }

        public double NormalizedQuantity => Category.UnitType == WeightUnit.Piece
            ? Math.Round(Math.Max(0, Quantity))
            : Math.Round(Math.Max(0, Quantity), 2);

        public double TotalRevenue => Math.Round(NormalizedQuantity * Math.Max(0, UnitPrice), 2);

        public bool HasEnoughStock => NormalizedQuantity <= Category.DisplayStock + 0.0001;

        public bool CanSubmit => NormalizedQuantity > 0 && UnitPrice > 0 && HasEnoughStock;

        public string SuggestedPriceText => Currency(SuggestedUnitPrice);

        public string TotalRevenueText => Currency(TotalRevenue);

        public string StockValidationText
        {
            get
            {
                if (NormalizedQuantity <= 0)
                {
                    return $"\u8BF7\u5148\u586B\u5199\u8981\u51FA\u5E93\u7684\u6570\u91CF\uFF0C\u5F53\u524D\u8BA1\u91CF\u5355\u4F4D\u4E3A {UnitLabel}\u3002";
                }

                return HasEnoughStock
                    ? $"\u5E93\u5B58\u5145\u8DB3\uFF0C\u5F53\u524D\u53EF\u51FA\u5E93\u5E93\u5B58\u4E3A {Category.DisplayStockText}\u3002"
                    : $"\u5E93\u5B58\u4E0D\u8DB3\uFF0C\u5F53\u524D\u4EC5\u5269 {Category.DisplayStockText}\u3002";
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

        public void ApplySuggestedUnitPrice(double unitPrice)
        {
            SuggestedUnitPrice = unitPrice;
            UnitPrice = unitPrice;
        }

        private void RaiseCustomerStateChanged()
        {
            OnPropertyChanged(nameof(EffectiveCustomerText));
            OnPropertyChanged(nameof(CustomerHintText));
        }

        private void RaiseComputedStateChanged()
        {
            OnPropertyChanged(nameof(NormalizedQuantity));
            OnPropertyChanged(nameof(TotalRevenue));
            OnPropertyChanged(nameof(TotalRevenueText));
            OnPropertyChanged(nameof(HasEnoughStock));
            OnPropertyChanged(nameof(CanSubmit));
            OnPropertyChanged(nameof(StockValidationText));
        }

        private static string Currency(double value) => $"\u00A5{value:0.##}";
    }
}
