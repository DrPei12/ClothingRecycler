using Microsoft.UI.Xaml.Navigation;

namespace ClothingRecycler.Desktop.Pages
{
    public sealed partial class FeaturePlaceholderPage : Page
    {
        public FeaturePlaceholderPage()
        {
            InitializeComponent();
        }

        public string TitleText { get; private set; } = "即将接入";

        public string DescriptionText { get; private set; } = "这个模块会在后续阶段开发。";

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (e.Parameter is FeaturePlaceholderInfo info)
            {
                TitleText = info.Title;
                DescriptionText = info.Description;
                Bindings.Update();
            }

            base.OnNavigatedTo(e);
        }
    }
}
