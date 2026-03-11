namespace ClothingRecycler.Desktop.Controls
{
    public sealed partial class CategoryEditorDialog : ContentDialog
    {
        private readonly CategoryModel? _existingCategory;

        public CategoryEditorDialog(CategoryModel? existingCategory = null)
        {
            InitializeComponent();
            _existingCategory = existingCategory;
            Title = existingCategory is null ? "新增分类" : "编辑分类";

            UnitComboBox.SelectedIndex = 0;

            if (existingCategory is null)
            {
                return;
            }

            NameTextBox.Text = existingCategory.Name;
            BuyPriceBox.Value = existingCategory.BuyPrice;
            SellPriceBox.Value = existingCategory.SellPrice;
            SelectUnit(existingCategory.UnitType);

            InitialStockPanel.Visibility = Visibility.Collapsed;
            CurrentStockPanel.Visibility = Visibility.Visible;
            CurrentStockTextBlock.Text = existingCategory.DisplayStockText;

            if (existingCategory.DisplayStock > 0)
            {
                UnitComboBox.IsEnabled = false;
            }
        }

        public CategoryModel? Result { get; private set; }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var name = NameTextBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(name))
            {
                ShowError("请输入分类名称。");
                args.Cancel = true;
                return;
            }

            if (BuyPriceBox.Value <= 0 || SellPriceBox.Value <= 0)
            {
                ShowError("收购价和卖价必须大于 0。");
                args.Cancel = true;
                return;
            }

            var unit = GetSelectedUnit();
            var initialStock = _existingCategory is null ? Math.Max(0, InitialStockBox.Value) : _existingCategory.DisplayStock;

            Result = new CategoryModel
            {
                Id = _existingCategory?.Id ?? 0,
                Name = name,
                BuyPrice = BuyPriceBox.Value,
                SellPrice = SellPriceBox.Value,
                Stock = unit == WeightUnit.Kilogram ? initialStock : _existingCategory?.Stock ?? 0,
                StockInJin = unit == WeightUnit.Jin ? initialStock : _existingCategory?.StockInJin ?? 0,
                StockInPieces = unit == WeightUnit.Piece ? (int)Math.Round(initialStock) : _existingCategory?.StockInPieces ?? 0,
                UnitType = unit,
                IsArchived = _existingCategory?.IsArchived ?? false,
                CreatedAt = _existingCategory?.CreatedAt ?? DateTimeOffset.Now,
                UpdatedAt = DateTimeOffset.Now
            };

            if (_existingCategory is not null)
            {
                Result.Stock = _existingCategory.Stock;
                Result.StockInJin = _existingCategory.StockInJin;
                Result.StockInPieces = _existingCategory.StockInPieces;
            }
        }

        private void ShowError(string message)
        {
            ValidationInfoBar.Message = message;
            ValidationInfoBar.IsOpen = true;
        }

        private void SelectUnit(WeightUnit unit)
        {
            foreach (var item in UnitComboBox.Items.OfType<ComboBoxItem>())
            {
                if (string.Equals(item.Tag?.ToString(), unit.ToString(), StringComparison.OrdinalIgnoreCase))
                {
                    UnitComboBox.SelectedItem = item;
                    return;
                }
            }
        }

        private WeightUnit GetSelectedUnit()
        {
            var tag = (UnitComboBox.SelectedItem as ComboBoxItem)?.Tag?.ToString();
            return Enum.TryParse<WeightUnit>(tag, true, out var unit) ? unit : WeightUnit.Kilogram;
        }
    }
}
