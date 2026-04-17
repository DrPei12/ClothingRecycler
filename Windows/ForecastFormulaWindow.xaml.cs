namespace ClothingRecycler.Desktop.FormulaWindows;

public sealed partial class ForecastFormulaWindow : Window
{
    public ForecastFormulaWindow(string projectedSalesAmountText, string inventoryCostText, string projectedNetProfitText)
    {
        ProjectedSalesAmountText = projectedSalesAmountText;
        InventoryCostText = inventoryCostText;
        ProjectedNetProfitText = projectedNetProfitText;

        InitializeComponent();
    }

    public string ProjectedSalesAmountText { get; }

    public string InventoryCostText { get; }

    public string ProjectedNetProfitText { get; }

    private void OnCloseClick(object sender, RoutedEventArgs e)
    {
        Close();
    }
}
