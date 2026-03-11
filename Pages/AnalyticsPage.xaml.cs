namespace ClothingRecycler.Desktop.Pages
{
    public sealed partial class AnalyticsPage : Page
    {
        public AnalyticsPage()
        {
            ViewModel = App.GetService<AnalyticsViewModel>();
            InitializeComponent();
            Loaded += OnLoaded;
        }

        public AnalyticsViewModel ViewModel { get; }

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
