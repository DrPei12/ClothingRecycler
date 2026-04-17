namespace ClothingRecycler.Desktop.Pages
{
    public sealed partial class DashboardPage : Page
    {
        public DashboardPage()
        {
            ViewModel = App.GetService<DashboardViewModel>();
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public DashboardViewModel ViewModel { get; }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            await ViewModel.LoadAsync();
        }

        private void OnProfitFormulaClick(object sender, RoutedEventArgs e)
        {
            var window = new ClothingRecycler.Desktop.FormulaWindows.ForecastFormulaWindow(
                ViewModel.ProjectedSalesAmount,
                ViewModel.TotalInventoryCost,
                ViewModel.ProjectedNetProfit);

            ClothingRecycler.Desktop.Helpers.SecondaryWindowManager.Show(
                window,
                "预计收入计算公式",
                preferredWidth: 1120,
                preferredHeight: 780);
        }
    }
}
