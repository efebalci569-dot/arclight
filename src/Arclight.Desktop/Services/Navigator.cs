using CommunityToolkit.Mvvm.ComponentModel;

namespace Arclight.Desktop.Services;

public enum PageKey
{
    Home,
    Library,
    Collections,
    RecentlyPlayed,
    Favorites,
    Statistics,
    Discover,
    Settings,
    GameDetail,
    CollectionDetail,
}

public sealed record NavigationRequest(PageKey Page, object? Parameter = null);

/// <summary>Decouples "go to page X" requests from the shell that performs them.</summary>
public sealed class Navigator
{
    public event EventHandler<NavigationRequest>? Requested;

    public event EventHandler? BackRequested;

    public void Go(PageKey page, object? parameter = null) => Requested?.Invoke(this, new NavigationRequest(page, parameter));

    public void Back() => BackRequested?.Invoke(this, EventArgs.Empty);
}

/// <summary>The global search text shared by the top bar and the library views.</summary>
public sealed partial class SearchState : ObservableObject
{
    [ObservableProperty]
    private string _text = string.Empty;
}
