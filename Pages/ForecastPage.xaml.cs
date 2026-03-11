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
    }
}
