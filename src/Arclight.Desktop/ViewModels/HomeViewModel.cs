using System.Collections.ObjectModel;
using Arclight.Core.Formatting;
using Arclight.Core.Library;
using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Core.Statistics;
using Arclight.Desktop.Services;
using Arclight.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arclight.Desktop.ViewModels;

public sealed record StatTile(string Label, string Value, string Detail, string Glyph);

/// <summary>The personal overview shown when Arclight opens.</summary>
public sealed partial class HomeViewModel : PageViewModel
{
    private readonly LibraryService _library;
    private readonly GameItemStore _items;
    private readonly GameActions _actions;
    private readonly ScanService _scans;
    private readonly Navigator _navigator;
    private readonly DialogService _dialogs;
    private bool _refreshQueued;

    public HomeViewModel(LibraryService library, GameItemStore items, GameActions actions, ScanService scans, Navigator navigator, DialogService dialogs)
    {
        _library = library;
        _items = items;
        _actions = actions;
        _scans = scans;
        _navigator = navigator;
        _dialogs = dialogs;
        _items.Changed += (_, _) => QueueRefresh();
        _items.RunningChanged += (_, _) => QueueRefresh();
        _scans.Completed += (_, _) => System.Windows.Application.Current.Dispatcher.BeginInvoke(QueueRefresh);
        Loc.LanguageChanged += (_, _) => QueueRefresh();
    }

    public override PageKey Key => PageKey.Home;

    public ObservableCollection<GameItemViewModel> ContinuePlaying { get; } = [];

    public ObservableCollection<GameItemViewModel> RecentlyAdded { get; } = [];

    public ObservableCollection<StatTile> Stats { get; } = [];

    [ObservableProperty]
    private string _greeting = string.Empty;

    [ObservableProperty]
    private bool _hasGames;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasFeatured))]
    private GameItemViewModel? _featured;

    [ObservableProperty]
    private int _newGamesDetected;

    public bool HasFeatured => Featured is not null;

    public bool HasContinuePlaying => ContinuePlaying.Count > 0;

    public bool HasNewGames => NewGamesDetected > 0;

    public override void OnNavigatedTo(object? parameter) => Refresh();

    private void QueueRefresh()
    {
        if (_refreshQueued)
        {
            return;
        }

        _refreshQueued = true;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(() =>
        {
            _refreshQueued = false;
            Refresh();
        }, System.Windows.Threading.DispatcherPriority.Background);
    }

    public async void Refresh()
    {
        var now = DateTimeOffset.Now;
        Greeting = Loc.Format(GreetingFor(now.Hour), DisplayNameFor(_library.Settings.DisplayName));

        var games = _library.Games;
        HasGames = games.Count > 0;
        Featured = _library.Settings.FeaturedGameId is { } featuredId ? _items.Find(featuredId) : null;

        Replace(ContinuePlaying, games
            .Where(g => g.LastPlayed is not null || _items.Get(g).IsRunning)
            .OrderByDescending(g => _items.Get(g).IsRunning)
            .ThenByDescending(g => g.LastPlayed)
            .Take(12)
            .Select(_items.Get));
        OnPropertyChanged(nameof(HasContinuePlaying));

        Replace(RecentlyAdded, games.OrderByDescending(g => g.DateAdded).ThenBy(g => g.Name, NaturalStringComparer.Instance).Take(14).Select(_items.Get));

        NewGamesDetected = _scans.LastReport?.Candidates.Count(c =>
            c.Status == ScanCandidateStatus.New && !c.Detected.LikelyNotAGame &&
            !_library.Games.Any(g => g.Platform == c.Detected.Platform &&
                                     string.Equals(g.PlatformIdentifier, c.Detected.PlatformIdentifier, StringComparison.OrdinalIgnoreCase))) ?? 0;
        OnPropertyChanged(nameof(HasNewGames));

        var weekAgo = now.AddDays(-8);
        var sessions = await _library.GetSessionsAsync(from: weekAgo);
        var stats = StatisticsCalculator.Calculate(_library.Games.ToList(), sessions, DateTimeOffset.UtcNow, TimeZoneInfo.Local);
        Replace(Stats,
        [
            new StatTile(Loc.Get("Home.Tile.TotalGames"), stats.TotalGames.ToString("#,0", Loc.Culture),
                Loc.Format("Home.Tile.Installed", stats.InstalledGames), ""),
            new StatTile(Loc.Get("Home.Tile.TotalPlaytime"), Loc.Format("Unit.HoursValue", stats.HasTrackedTime ? Format.Hours(stats.TotalPlaytimeSeconds) : "0"),
                stats.HasTrackedTime ? Loc.Get("Home.Tile.TrackedByArclight") : Format.Strings.NoPlaytime, ""),
            new StatTile(Loc.Get("Home.Tile.PlayedThisWeek"), stats.GamesPlayedThisWeek.ToString(Loc.Culture),
                stats.ThisWeekSeconds > 0 ? Format.Playtime(stats.ThisWeekSeconds) : Loc.Get("Home.Tile.NoSessionsYet"), ""),
            new StatTile(Loc.Get("Home.Tile.MostPlayed"), stats.MostPlayed?.Name ?? "—",
                stats.MostPlayed is null ? Loc.Get("Home.Tile.PlayToSee") : Format.Playtime(stats.MostPlayed.Seconds), ""),
        ]);
    }

    [RelayCommand]
    private Task PlayFeatured() => Featured is null ? Task.CompletedTask : _actions.PrimaryActionAsync(Featured.Game);

    [RelayCommand]
    private void FeaturedDetails()
    {
        if (Featured is not null)
        {
            _actions.OpenDetails(Featured.Game);
        }
    }

    [RelayCommand]
    private async Task ChooseFeaturedAsync()
    {
        var picker = new GamePickerViewModel(_items.All, Loc.Get("Home.PickFeatured.Title"), Loc.Get("Home.PickFeatured.Confirm"), multiSelect: false);
        if (await _dialogs.ShowAsync<List<Game>>(picker) is [var game, ..])
        {
            await _actions.FeatureOnHomeAsync(game);
            Refresh();
        }
    }

    [RelayCommand]
    private Task ScanAsync() => _actions.ScanAsync();

    [RelayCommand]
    private Task ReviewNewGamesAsync() => _actions.ScanAsync(showLastReport: true);

    [RelayCommand]
    private Task AddGameAsync() => _actions.AddGameAsync();

    [RelayCommand]
    private Task LearnMoreAsync() => _actions.ShowAboutDetectionAsync();

    [RelayCommand]
    private void OpenLibrary() => _navigator.Go(PageKey.Library);

    [RelayCommand]
    private void OpenRecentlyPlayed() => _navigator.Go(PageKey.RecentlyPlayed);

    private static string GreetingFor(int hour) => hour switch
    {
        >= 5 and < 12 => "Home.Greeting.Morning",
        >= 12 and < 18 => "Home.Greeting.Afternoon",
        _ => "Home.Greeting.Evening",
    };

    /// <summary>The default name "Player" is shown in the UI language; anything the user typed is shown as is.</summary>
    internal static string DisplayNameFor(string? name) =>
        string.IsNullOrWhiteSpace(name) || name.Trim() == "Player" ? Loc.Get("Home.DefaultName") : name.Trim();

    internal static void Replace<T>(ObservableCollection<T> target, IEnumerable<T> items)
    {
        var list = items.ToList();
        if (target.SequenceEqual(list))
        {
            return;
        }

        target.Clear();
        foreach (var item in list)
        {
            target.Add(item);
        }
    }
}
