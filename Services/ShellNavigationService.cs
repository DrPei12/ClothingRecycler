namespace ClothingRecycler.Desktop.Services;

public sealed class ShellNavigationService
{
    private Func<string, bool>? _navigator;

    public void RegisterNavigator(Func<string, bool> navigator)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        _navigator = navigator;
    }

    public bool Navigate(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag))
        {
            return false;
        }

        return _navigator?.Invoke(tag) ?? false;
    }
}
