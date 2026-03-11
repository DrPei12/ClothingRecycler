namespace ClothingRecycler.Desktop.Models
{
    public sealed class FontSizeOptionModel
    {
        public FontSizeOptionModel(AppFontSizePreset preset, string displayName, string description)
        {
            Preset = preset;
            DisplayName = displayName;
            Description = description;
        }

        public AppFontSizePreset Preset { get; }

        public string DisplayName { get; }

        public string Description { get; }
    }
}
