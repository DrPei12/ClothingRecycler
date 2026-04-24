namespace ClothingRecycler.Desktop.Services;

public interface IAiBusinessContextSource
{
    Task<IReadOnlyList<CategoryModel>> GetActiveCategoriesAsync();

    Task<IReadOnlyList<CustomerModel>> GetCustomersAsync();
}

public sealed class LocalDatabaseAiBusinessContextSource : IAiBusinessContextSource
{
    private readonly LocalDatabaseService _databaseService;

    public LocalDatabaseAiBusinessContextSource(LocalDatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public Task<IReadOnlyList<CategoryModel>> GetActiveCategoriesAsync()
    {
        return _databaseService.GetActiveCategoriesAsync();
    }

    public Task<IReadOnlyList<CustomerModel>> GetCustomersAsync()
    {
        return _databaseService.GetCustomersAsync();
    }
}
