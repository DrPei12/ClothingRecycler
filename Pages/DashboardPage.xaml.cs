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
    }
}
