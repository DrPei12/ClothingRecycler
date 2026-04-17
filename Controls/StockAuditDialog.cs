namespace ClothingRecycler.Desktop.Controls
{
    public sealed class StockAuditDialog : ContentDialog
    {
        private readonly StockCategoryItemModel _item;
        private readonly InfoBar _validationInfoBar;
        private readonly NumberBox _actualQuantityBox;
        private readonly TextBox _noteTextBox;

        public StockAuditDialog(StockCategoryItemModel item)
        {
            _item = item;

            Title = $"\u76D8\u70B9\u767B\u8BB0 - {item.Name}";
            PrimaryButtonText = "\u4FDD\u5B58\u76D8\u70B9";
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

            _actualQuantityBox = new NumberBox
            {
                Minimum = 0,
                SmallChange = item.Category.UnitType == WeightUnit.Piece ? 1 : 0.5,
                Value = item.CurrentQuantity
            };

            _noteTextBox = new TextBox
            {
                AcceptsReturn = true,
                PlaceholderText = "\u4F8B\u5982\uFF1A\u665A\u73ED\u76D8\u70B9\u3001\u5E93\u533A\u6574\u7406\u540E\u590D\u6838",
                TextWrapping = TextWrapping.Wrap
            };

            var root = new StackPanel
            {
                Spacing = 12
            };
            root.Children.Add(_validationInfoBar);
            root.Children.Add(CreateReadOnlyField("\u5206\u7C7B", item.Name, emphasize: true));
            root.Children.Add(CreateReadOnlyField("\u7CFB\u7EDF\u5E93\u5B58", item.DisplayStockText, emphasize: true));
            root.Children.Add(CreateEditableField("\u5B9E\u9645\u76D8\u70B9\u6570\u91CF", _actualQuantityBox));
            root.Children.Add(CreateEditableField("\u76D8\u70B9\u5907\u6CE8", _noteTextBox));
            root.Children.Add(new TextBlock
            {
                Opacity = 0.72,
                Text = "\u76D8\u70B9\u4F1A\u7559\u4E0B\u4E00\u7B14\u72EC\u7ACB\u76D8\u70B9\u8BB0\u5F55\uFF1B\u5982\u679C\u5B9E\u9645\u76D8\u70B9\u6570\u91CF\u548C\u7CFB\u7EDF\u5E93\u5B58\u4E0D\u4E00\u81F4\uFF0C\u7CFB\u7EDF\u4F1A\u540C\u65F6\u751F\u6210\u4E00\u7B14\u5E93\u5B58\u6821\u6B63\u8BB0\u5F55\u3002",
                TextWrapping = TextWrapping.WrapWholeWords
            });

            Content = root;
        }

        public StockAuditInputModel? Result { get; private set; }

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
            var actualQuantity = _actualQuantityBox.Value;
            if (double.IsNaN(actualQuantity) || actualQuantity < 0)
            {
                args.Cancel = true;
                ShowValidation("\u76D8\u70B9\u6570\u91CF\u4E0D\u80FD\u4E3A\u8D1F\u6570\u3002");
                return;
            }

            actualQuantity = _item.Category.UnitType == WeightUnit.Piece
                ? Math.Round(actualQuantity)
                : Math.Round(actualQuantity, 2);

            _validationInfoBar.IsOpen = false;
            Result = new StockAuditInputModel
            {
                CategoryId = _item.Id,
                ActualQuantity = actualQuantity,
                Note = _noteTextBox.Text?.Trim() ?? string.Empty
            };
        }

        private void ShowValidation(string message)
        {
            _validationInfoBar.Message = message;
            _validationInfoBar.IsOpen = true;
        }
    }
}
