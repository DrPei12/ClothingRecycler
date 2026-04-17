namespace ClothingRecycler.Desktop.Pages
{
    public sealed partial class ForecastPage : Page
    {
        public ForecastPage()
        {
            ViewModel = App.GetService<ForecastViewModel>();
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public ForecastViewModel ViewModel { get; }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private async void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private void OnProfitFormulaClick(object sender, RoutedEventArgs e)
        {
            var window = new ClothingRecycler.Desktop.FormulaWindows.ForecastFormulaWindow(
                ViewModel.ProjectedSalesAmountText,
                ViewModel.TotalInventoryCostText,
                ViewModel.ProjectedNetProfitText);

            ClothingRecycler.Desktop.Helpers.SecondaryWindowManager.Show(
                window,
                "预计收入计算公式",
                preferredWidth: 1120,
                preferredHeight: 780);
        }
    }
}
