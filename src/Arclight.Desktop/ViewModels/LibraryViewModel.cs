using System.Collections.ObjectModel;
using System.ComponentModel;
using Arclight.Core.Formatting;
using Arclight.Core.Library;
using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Desktop.Services;
using Arclight.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arclight.Desktop.ViewModels;

public enum LibraryPageMode { All, Favorites, Collection }

public sealed partial class FilterOption(string label, object value) : ObservableObject
{
    public string Label { get; } = label;

    public object Value { get; } = value;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>A sort order whose label follows the UI language.</summary>
public sealed class SortOption : System.ComponentModel.INotifyPropertyChanged
{
    public SortOption(LibrarySort sort, string labelKey)
    {
        Sort = sort;
        LabelKey = labelKey;
        Loc.LanguageChanged += (_, _) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Label)));
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    public LibrarySort Sort { get; }

    public string LabelKey { get; }

    public string Label => Loc.Get(LabelKey);

    public override string ToString() => Label;
}

/// <summary>
/// The game grid/list with search, filters and sorting. The same view model backs My Library,
/// Favorites and a single collection's page.
/// </summary>
public sealed partial class LibraryViewModel : PageViewModel
{
    private readonly LibraryService _library;
    private readonly GameItemStore _items;
    private readonly GameActions _actions;
    private readonly SearchState _search;
    private readonly DialogService _dialogs;
    private readonly ArtworkService _artwork;
    private readonly Navigator _navigator;
    private readonly NotificationService _notifications;
    private bool _suspendRefresh;
    private bool _refreshQueued;
    private bool _syncingViewMode;

    public LibraryViewModel(
        LibraryService library,
        GameItemStore items,
        GameActions actions,
        SearchState search,
        DialogService dialogs,
        ArtworkService artwork,
        Navigator navigator,
        NotificationService notifications,
        LibraryPageMode mode = LibraryPageMode.All)
    {
        _library = library;
        _items = items;
        _actions = actions;
        _search = search;
        _dialogs = dialogs;
        _artwork = artwork;
        _navigator = navigator;
        _notifications = notifications;
        Mode = mode;
        _viewMode = library.Settings.LibraryViewMode;
        _selectedSort = SortOptions.First(s => s.Sort == library.Settings.LibrarySort);

        _items.Changed += (_, e) =>
        {
            if (e.Kind is LibraryChangeKind.SettingsChanged)
            {
                OnPropertyChanged(nameof(CardWidth));
                OnPropertyChanged(nameof(CardGap));
            }

            QueueRefresh();
        };
        _search.PropertyChanged += (_, _) =>
        {
            if (IsActive)
            {
                QueueRefresh();
            }
        };
        Loc.LanguageChanged += (_, _) =>
        {
            // Platform names are translated, so rebuild the filter chips.
            PlatformFilters.Clear();
            QueueRefresh();
        };
    }

    public override PageKey Key => Mode switch
    {
        LibraryPageMode.Favorites => PageKey.Favorites,
        LibraryPageMode.Collection => PageKey.CollectionDetail,
        _ => PageKey.Library,
    };

    public override PageKey NavKey => Mode == LibraryPageMode.Collection ? PageKey.Collections : Key;

    public override bool UsesSearch => true;

    public LibraryPageMode Mode { get; }

    public bool IsCollection => Mode == LibraryPageMode.Collection;

    public bool ShowFavoritesChip => Mode != LibraryPageMode.Favorites;

    public GameCollection? Collection { get; private set; }

    public static IReadOnlyList<SortOption> SortOptions { get; } =
    [
        new(LibrarySort.NameAscending, "Sort.NameAscending"),
        new(LibrarySort.NameDescending, "Sort.NameDescending"),
        new(LibrarySort.RecentlyAdded, "Sort.RecentlyAdded"),
        new(LibrarySort.RecentlyPlayed, "Sort.RecentlyPlayed"),
        new(LibrarySort.MostPlayed, "Sort.MostPlayed"),
        new(LibrarySort.LeastPlayed, "Sort.LeastPlayed"),
        new(LibrarySort.ReleaseYear, "Sort.ReleaseYear"),
        new(LibrarySort.InstallationStatus, "Sort.InstallationStatus"),
    ];

    public ObservableCollection<FilterOption> PlatformFilters { get; } = [];

    public ObservableCollection<FilterOption> GenreFilters { get; } = [];

    public ObservableCollection<FilterOption> TagFilters { get; } = [];

    [ObservableProperty]
    private IReadOnlyList<GameItemViewModel> _results = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGrid), nameof(IsCompact), nameof(IsList), nameof(CardWidth), nameof(CardGap))]
    private LibraryViewMode _viewMode;

    [ObservableProperty]
    private SortOption _selectedSort;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAllInstallStates))]
    private InstallFilter _installFilter = InstallFilter.All;

    [ObservableProperty]
    private bool _favoritesOnly;

    [ObservableProperty]
    private bool _recentlyPlayedOnly;

    [ObservableProperty]
    private bool _neverPlayedOnly;

    [ObservableProperty]
    private string _title = "My Library";

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private string _resultText = string.Empty;

    [ObservableProperty]
    private bool _isLibraryEmpty;

    [ObservableProperty]
    private bool _hasNoResults;

    [ObservableProperty]
    private bool _hasActiveFilters;

    [ObservableProperty]
    private string _noResultsText = string.Empty;

    [ObservableProperty]
    private string? _collectionCover;

    private bool IsActive { get; set; }

    public bool IsGrid
    {
        get => ViewMode == LibraryViewMode.Grid;
        set { if (value) ViewMode = LibraryViewMode.Grid; }
    }

    public bool IsCompact
    {
        get => ViewMode == LibraryViewMode.CompactGrid;
        set { if (value) ViewMode = LibraryViewMode.CompactGrid; }
    }

    public bool IsList
    {
        get => ViewMode == LibraryViewMode.List;
        set { if (value) ViewMode = LibraryViewMode.List; }
    }

    public bool IsAllInstallStates => InstallFilter == InstallFilter.All;

    public bool IsInstalledOnly
    {
        get => InstallFilter == InstallFilter.Installed;
        set => InstallFilter = value ? InstallFilter.Installed : InstallFilter.All;
    }

    public bool IsNotInstalledOnly
    {
        get => InstallFilter == InstallFilter.NotInstalled;
        set => InstallFilter = value ? InstallFilter.NotInstalled : InstallFilter.All;
    }

    /// <summary>Base card width for the grid; the grid stretches cards to fill each row.</summary>
    public double CardWidth
    {
        get
        {
            var size = _library.Settings.CardSize switch
            {
                CardSize.Small => 150,
                CardSize.Large => 230,
                _ => 188,
            };
            return ViewMode == LibraryViewMode.CompactGrid ? size * 0.72 : size;
        }
    }

    public double CardGap => _library.Settings.GridDensity == GridDensity.Compact || ViewMode == LibraryViewMode.CompactGrid ? 12 : 20;

    public string PlatformFilterText => FilterSummary(PlatformFilters, Loc.Get("Library.Filter.Platform"));

    public string GenreFilterText => FilterSummary(GenreFilters, Loc.Get("Library.Filter.Genre"));

    public string TagFilterText => FilterSummary(TagFilters, Loc.Get("Library.Filter.Tags"));

    public bool HasGenres => GenreFilters.Count > 0;

    public bool HasTags => TagFilters.Count > 0;

    public override void OnNavigatedTo(object? parameter)
    {
        IsActive = true;
        if (Mode == LibraryPageMode.Collection && parameter is long collectionId)
        {
            Collection = _library.Collections.FirstOrDefault(c => c.Id == collectionId);
        }

        _syncingViewMode = true;
        ViewMode = _library.Settings.LibraryViewMode;
        _syncingViewMode = false;
        OnPropertyChanged(string.Empty);
        RebuildFilterOptions();
        Refresh();
    }

    public override void OnNavigatedFrom() => IsActive = false;

    partial void OnViewModeChanged(LibraryViewMode value)
    {
        if (!_syncingViewMode && value != _library.Settings.LibraryViewMode)
        {
            _ = _library.UpdateSettingsAsync(s => s with { LibraryViewMode = value });
        }
    }

    partial void OnSelectedSortChanged(SortOption value)
    {
        _ = _library.UpdateSettingsAsync(s => s with { LibrarySort = value.Sort });
        Refresh();
    }

    partial void OnInstallFilterChanged(InstallFilter value)
    {
        OnPropertyChanged(nameof(IsInstalledOnly));
        OnPropertyChanged(nameof(IsNotInstalledOnly));
        Refresh();
    }

    partial void OnFavoritesOnlyChanged(bool value) => Refresh();

    partial void OnRecentlyPlayedOnlyChanged(bool value)
    {
        if (value)
        {
            NeverPlayedOnly = false;
        }

        Refresh();
    }

    partial void OnNeverPlayedOnlyChanged(bool value)
    {
        if (value)
        {
            RecentlyPlayedOnly = false;
        }

        Refresh();
    }

    [RelayCommand]
    private void ShowAll()
    {
        _suspendRefresh = true;
        InstallFilter = InstallFilter.All;
        FavoritesOnly = RecentlyPlayedOnly = NeverPlayedOnly = false;
        foreach (var option in PlatformFilters.Concat(GenreFilters).Concat(TagFilters))
        {
            option.IsSelected = false;
        }

        _suspendRefresh = false;
        Refresh();
    }

    [RelayCommand]
    private void ClearFilters()
    {
        ShowAll();
        _search.Text = string.Empty;
    }

    [RelayCommand]
    private Task AddGameAsync() => _actions.AddGameAsync();

    [RelayCommand]
    private Task ScanAsync() => _actions.ScanAsync();

    [RelayCommand]
    private async Task EditCollectionAsync()
    {
        if (Collection is null)
        {
            return;
        }

        var editor = new CollectionEditorViewModel(_library, _artwork, _dialogs, Collection);
        if (await _dialogs.ShowAsync<GameCollection>(editor) is not null)
        {
            Collection = _library.Collections.FirstOrDefault(c => c.Id == Collection.Id);
            Refresh();
        }
    }

    [RelayCommand]
    private async Task DeleteCollectionAsync()
    {
        if (Collection is null)
        {
            return;
        }

        var answer = await _dialogs.ConfirmAsync(
            Loc.Format("Collection.Delete.Title", Collection.Name),
            Loc.Get("Collection.Delete.Message"),
            Loc.Get("Library.DeleteCollection"),
            danger: true);
        if (answer.Confirmed)
        {
            await _library.DeleteCollectionAsync(Collection.Id);
            _notifications.Info(Loc.Format("Collection.Deleted.Title", Collection.Name), Loc.Get("Collection.Deleted.Message"));
            _navigator.Go(PageKey.Collections);
        }
    }

    [RelayCommand]
    private async Task AddGamesToCollectionAsync()
    {
        if (Collection is null)
        {
            return;
        }

        var picker = new GamePickerViewModel(_items.All, Loc.Format("Collection.AddGames.Title", Collection.Name), Loc.Get("Common.AddToCollection"), multiSelect: true, Collection.GameIds.ToHashSet());
        if (await _dialogs.ShowAsync<List<Game>>(picker) is { Count: > 0 } games)
        {
            await _library.AddToCollectionAsync(Collection.Id, games.Select(g => g.Id).ToList());
            _notifications.Show(NoticeKind.Success, Loc.Format("Collection.GamesAdded", Loc.Count("Count.Game", games.Count), Collection.Name), null, keepInHistory: false);
        }
    }

    [RelayCommand]
    private async Task RemoveFromCollectionAsync(GameItemViewModel? item)
    {
        if (Collection is not null && item is not null)
        {
            await _library.RemoveFromCollectionAsync(Collection.Id, item.Id);
        }
    }

    private void QueueRefresh()
    {
        if (_refreshQueued || !IsActive)
        {
            return;
        }

        _refreshQueued = true;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _refreshQueued = false;
            RebuildFilterOptions();
            Refresh();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    public void Refresh()
    {
        if (_suspendRefresh)
        {
            return;
        }

        if (Mode == LibraryPageMode.Collection)
        {
            Collection = Collection is null ? null : _library.Collections.FirstOrDefault(c => c.Id == Collection.Id);
            if (Collection is null)
            {
                Results = [];
                Title = Loc.Get("Collection.NotFound");
                return;
            }
        }

        var query = new LibraryQuery
        {
            SearchText = _search.Text,
            Install = InstallFilter,
            FavoritesOnly = FavoritesOnly || Mode == LibraryPageMode.Favorites,
            RecentlyPlayedOnly = RecentlyPlayedOnly,
            NeverPlayedOnly = NeverPlayedOnly,
            Platforms = PlatformFilters.Where(f => f.IsSelected).Select(f => (GamePlatform)f.Value).ToHashSet(),
            Genres = PlatformSet(GenreFilters),
            Tags = PlatformSet(TagFilters),
            CollectionId = Collection?.Id,
            Sort = SelectedSort.Sort,
        };

        var baseCount = Mode switch
        {
            LibraryPageMode.Favorites => _library.Games.Count(g => g.IsFavorite),
            LibraryPageMode.Collection => Collection!.GameIds.Count,
            _ => _library.Games.Count,
        };

        Results = LibraryFilter.Apply(_library.Games, query, _library.CreateContext()).Select(_items.Get).ToList();

        Title = Mode switch
        {
            LibraryPageMode.Favorites => Loc.Get("Common.Favorites"),
            LibraryPageMode.Collection => Collection!.Name,
            _ => Loc.Get("Shell.MyLibrary"),
        };
        Subtitle = Mode switch
        {
            LibraryPageMode.Collection => string.IsNullOrWhiteSpace(Collection!.Description) ? Loc.Count("Count.Game", baseCount) : $"{Collection.Description}  ·  {Loc.Count("Count.Game", baseCount)}",
            _ => Loc.Count("Count.Game", baseCount),
        };
        CollectionCover = Collection?.CoverImagePath;

        var effectiveFilters = query with { FavoritesOnly = FavoritesOnly };
        HasActiveFilters = effectiveFilters.HasActiveFilters;
        ResultText = HasActiveFilters ? Loc.Format("Library.Showing", Results.Count, baseCount) : string.Empty;
        IsLibraryEmpty = baseCount == 0;
        HasNoResults = !IsLibraryEmpty && Results.Count == 0;
        NoResultsText = string.IsNullOrWhiteSpace(_search.Text)
            ? Loc.Get("Library.NoMatchFilters")
            : Loc.Format("Library.NoMatchSearch", _search.Text.Trim());
        OnPropertyChanged(nameof(PlatformFilterText));
        OnPropertyChanged(nameof(GenreFilterText));
        OnPropertyChanged(nameof(TagFilterText));
    }

    private void RebuildFilterOptions()
    {
        Sync(PlatformFilters, _library.Games.Select(g => g.Platform).Distinct().Order()
            .Select(p => ((object)p, Loc.PlatformName(p))));
        Sync(GenreFilters, _library.AllGenres.Select(g => ((object)g, g)));
        Sync(TagFilters, _library.AllTags.Select(t => ((object)t, t)));
        OnPropertyChanged(nameof(HasGenres));
        OnPropertyChanged(nameof(HasTags));
    }

    private void Sync(ObservableCollection<FilterOption> target, IEnumerable<(object Value, string Label)> options)
    {
        var desired = options.ToList();
        var selected = target.Where(o => o.IsSelected).Select(o => o.Value).ToHashSet();
        if (target.Select(o => o.Value).SequenceEqual(desired.Select(d => d.Value)))
        {
            return;
        }

        foreach (var option in target)
        {
            option.PropertyChanged -= OnFilterOptionChanged;
        }

        target.Clear();
        foreach (var (value, label) in desired)
        {
            var option = new FilterOption(label, value) { IsSelected = selected.Contains(value) };
            option.PropertyChanged += OnFilterOptionChanged;
            target.Add(option);
        }
    }

    private void OnFilterOptionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FilterOption.IsSelected))
        {
            Refresh();
        }
    }

    private static HashSet<string> PlatformSet(IEnumerable<FilterOption> options) =>
        options.Where(f => f.IsSelected).Select(f => (string)f.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private static string FilterSummary(IEnumerable<FilterOption> options, string label)
    {
        var selected = options.Where(o => o.IsSelected).ToList();
        return selected.Count switch
        {
            0 => label,
            1 => selected[0].Label,
            _ => $"{label} ({selected.Count})",
        };
    }
}
