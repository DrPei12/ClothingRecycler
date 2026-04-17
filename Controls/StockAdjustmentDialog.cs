namespace ClothingRecycler.Desktop.Controls
{
    public sealed class StockAdjustmentDialog : ContentDialog
    {
        private readonly StockCategoryItemModel _item;
        private readonly InfoBar _validationInfoBar;
        private readonly NumberBox _adjustedQuantityBox;
        private readonly TextBox _reasonTextBox;

        public StockAdjustmentDialog(
            StockCategoryItemModel item,
            double? initialAdjustedQuantity = null,
            string? defaultReason = null,
            string? title = null,
            string? primaryButtonText = null)
        {
            _item = item;

            Title = title ?? $"\u624B\u52A8\u8C03\u6574\u5E93\u5B58 - {item.Name}";
            PrimaryButtonText = primaryButtonText ?? "\u4FDD\u5B58\u8C03\u6574";
            CloseButtonText = "\u53D6\u6D88";
            DefaultButton = ContentDialogButton.Primary;
            MinWidth = 520;
            PrimaryButtonClick += OnPrimaryButtonClick;

            _validationInfoBar = new InfoBar
            {
                IsClosable = false,
                IsOpen = false,
                Severity = InfoBarSeverity.Error
            };

            _adjustedQuantityBox = new NumberBox
            {
                Minimum = 0,
                SmallChange = item.Category.UnitType == WeightUnit.Piece ? 1 : 0.5,
                Value = initialAdjustedQuantity ?? item.Category.DisplayStock
            };

            _reasonTextBox = new TextBox
            {
                AcceptsReturn = true,
                PlaceholderText = "\u4F8B\u5982\uFF1A\u76D8\u70B9\u5DEE\u5F02\u3001\u7834\u635F\u3001\u8865\u5F55\u6570\u636E",
                Text = defaultReason ?? string.Empty,
                TextWrapping = TextWrapping.Wrap
            };

            var root = new StackPanel
            {
                Spacing = 12
            };
            root.Children.Add(_validationInfoBar);
            root.Children.Add(CreateReadOnlyField("\u5206\u7C7B", item.Name, emphasize: true));
            root.Children.Add(CreateReadOnlyField("\u5F53\u524D\u5E93\u5B58", item.DisplayStockText, emphasize: true));
            root.Children.Add(CreateEditableField("\u8C03\u6574\u540E\u5E93\u5B58", _adjustedQuantityBox));
            root.Children.Add(CreateEditableField("\u8C03\u6574\u539F\u56E0", _reasonTextBox));
            root.Children.Add(new TextBlock
            {
                Opacity = 0.72,
                Text = "\u624B\u52A8\u8C03\u6574\u4F1A\u76F4\u63A5\u628A\u5F53\u524D\u5206\u7C7B\u5E93\u5B58\u6539\u6210\u4F60\u586B\u5199\u7684\u503C\uFF0C\u5E76\u7559\u4E0B\u4E00\u7B14\u624B\u52A8\u8C03\u6574\u8BB0\u5F55\u3002",
                TextWrapping = TextWrapping.WrapWholeWords
            });

            Content = root;
        }

        public StockAdjustmentInputModel? Result { get; private set; }

        private static FrameworkElement CreateReadOnlyField(string label, string value, bool emphasize = false)
        {
            var panel = new StackPanel
            {
                Spacing = 4
            };

            panel.Children.Add(new TextBlock
            {
                Opacity = 0.72,
                Text = label
            });
            panel.Children.Add(new TextBlock
            {
                Text = value,
                FontSize = emphasize ? 18 : 14,
                FontWeight = emphasize ? Microsoft.UI.Text.FontWeights.SemiBold : Microsoft.UI.Text.FontWeights.Normal
            });

            return panel;
        }

        private static FrameworkElement CreateEditableField(string label, FrameworkElement input)
        {
            var panel = new StackPanel
            {
                Spacing = 4
            };

            panel.Children.Add(new TextBlock
            {
                Opacity = 0.72,
                Text = label
            });
            panel.Children.Add(input);

            return panel;
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var adjustedQuantity = _adjustedQuantityBox.Value;
            if (double.IsNaN(adjustedQuantity) || adjustedQuantity < 0)
            {
                args.Cancel = true;
                ShowValidation("\u8C03\u6574\u540E\u5E93\u5B58\u4E0D\u80FD\u4E3A\u8D1F\u6570\u3002");
                return;
            }

            adjustedQuantity = _item.Category.UnitType == WeightUnit.Piece
                ? Math.Round(adjustedQuantity)
                : Math.Round(adjustedQuantity, 2);

            if (Math.Abs(adjustedQuantity - _item.Category.DisplayStock) < 0.0001)
            {
                args.Cancel = true;
                ShowValidation("\u8C03\u6574\u540E\u5E93\u5B58\u4E0E\u5F53\u524D\u4E00\u81F4\uFF0C\u65E0\u9700\u4FDD\u5B58\u3002");
                return;
            }

            _validationInfoBar.IsOpen = false;
            Result = new StockAdjustmentInputModel
            {
                CategoryId = _item.Id,
                AdjustedQuantity = adjustedQuantity,
                Reason = _reasonTextBox.Text?.Trim() ?? string.Empty
            };
        }

        private void ShowValidation(string message)
        {
            _validationInfoBar.Message = message;
            _validationInfoBar.IsOpen = true;
        }
    }
}
