namespace ClothingRecycler.Desktop.Models
{
    public sealed class FeaturePlaceholderInfo
    {
        public FeaturePlaceholderInfo(string title, string description)
        {
            Title = title;
            Description = description;
        }

        public string Title { get; }

        public string Description { get; }
    }
}
