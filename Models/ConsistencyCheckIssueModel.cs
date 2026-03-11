namespace ClothingRecycler.Desktop.Models
{
    public sealed class ConsistencyCheckIssueModel
    {
        public string CheckName { get; init; } = string.Empty;

        public string SeverityText { get; init; } = string.Empty;

        public string Title { get; init; } = string.Empty;

        public string DetailText { get; init; } = string.Empty;
    }
}
