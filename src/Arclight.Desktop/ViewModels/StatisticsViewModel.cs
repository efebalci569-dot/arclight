using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Arclight.Core.Formatting;
using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Core.Statistics;
using Arclight.Desktop.Controls;
using Arclight.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Arclight.Desktop.ViewModels;

public sealed record TopGameRow(int Rank, GameItemViewModel? Game, string Name, string Hours, double Fraction)
{
    public GridLength FilledWidth => new(Math.Max(0.001, Fraction), GridUnitType.Star);

    public GridLength EmptyWidth => new(Math.Max(0.001, 1 - Fraction), GridUnitType.Star);
}

public sealed record LegendRow(string Label, string Value, Brush Brush);

/// <summary>Charts and totals computed only from what Arclight recorded on this PC.</summary>
public sealed partial class StatisticsViewModel : PageViewModel
{
    private static readonly Color[] SegmentColors =
    [
        Color.FromRgb(0x8B, 0x5C, 0xF6), Color.FromRgb(0x38, 0xBD, 0xF8), Color.FromRgb(0x34, 0xD3, 0x99),
        Color.FromRgb(0xFB, 0xBF, 0x24), Color.FromRgb(0xF8, 0x71, 0x71), Color.FromRgb(0xE8, 0x79, 0xF9),
        Color.FromRgb(0xA1, 0xA1, 0xAA), Color.FromRgb(0x6E, 0xE7, 0xB7),
    ];

    private readonly LibraryService _library;
    private readonly GameItemStore _items;
    private bool _isActive;

    public StatisticsViewModel(LibraryService library, GameItemStore items)
    {
        _library = library;
        _items = items;
        _items.Changed += (_, e) =>
        {
            if (_isActive && e.Kind is LibraryChangeKind.PlaytimeChanged or LibraryChangeKind.GamesAdded or LibraryChangeKind.GamesRemoved)
            {
                Refresh();
            }
        };
        Loc.LanguageChanged += (_, _) =>
        {
            if (_isActive)
            {
                Refresh();
            }
        };
    }

    public override PageKey Key => PageKey.Statistics;

    public ObservableCollection<StatTile> Tiles { get; } = [];

    public ObservableCollection<TopGameRow> TopGames { get; } = [];

    public ObservableCollection<LegendRow> PlatformLegend { get; } = [];

    [ObservableProperty]
    private IReadOnlyList<ChartPoint> _activity = [];

    [ObservableProperty]
    private IReadOnlyList<ChartSegment> _platformSegments = [];

    [ObservableProperty]
    private string _platformTotal = "0";

    [ObservableProperty]
    private bool _hasTrackedTime;

    [ObservableProperty]
    private string _activitySummary = string.Empty;

    [ObservableProperty]
    private bool _hasGames;

    public bool TrackingEnabled => _library.Settings.PlaytimeTrackingEnabled;

    public override void OnNavigatedTo(object? parameter)
    {
        _isActive = true;
        Refresh();
    }

    public override void OnNavigatedFrom() => _isActive = false;

    public async void Refresh()
    {
        var sessions = await _library.GetSessionsAsync();
        var games = _library.Games.ToList();
        var stats = StatisticsCalculator.Calculate(games, sessions, DateTimeOffset.UtcNow, TimeZoneInfo.Local, topCount: 6);
        HasGames = stats.TotalGames > 0;
        HasTrackedTime = stats.HasTrackedTime;
        OnPropertyChanged(nameof(TrackingEnabled));

        var inv = Loc.Culture;
        HomeViewModel.Replace(Tiles,
        [
            new StatTile(Loc.Get("Stats.Tile.TotalGames"), stats.TotalGames.ToString("#,0", inv), Loc.Format("Stats.Tile.Played", Loc.Count("Count.Game", stats.PlayedGames)), ""),
            new StatTile(Loc.Get("Library.Installed"), stats.InstalledGames.ToString("#,0", inv), Loc.Format("Stats.Tile.NotInstalled", stats.TotalGames - stats.InstalledGames), ""),
            new StatTile(Loc.Get("Stats.Tile.TrackedPlaytime"), Loc.Format("Unit.HoursValue", Format.Hours(stats.TotalPlaytimeSeconds)), stats.HasTrackedTime ? Loc.Get("Stats.Tile.VerifiedOnly") : Format.Strings.NoPlaytime, ""),
            new StatTile(Loc.Get("Stats.Tile.ThisWeek"), Loc.Format("Unit.HoursValue", Format.Hours(stats.ThisWeekSeconds)), Loc.Format("Stats.Tile.Played", Loc.Count("Count.Game", stats.GamesPlayedThisWeek)), ""),
            new StatTile(Loc.Get("Stats.Tile.ThisMonth"), Loc.Format("Unit.HoursValue", Format.Hours(stats.ThisMonthSeconds)), DateTime.Now.ToString("MMMM yyyy", inv), ""),
            new StatTile(Loc.Get("Stats.Tile.Average"), stats.PlayedGames == 0 ? "—" : Loc.Format("Unit.HoursValue", Format.Hours(stats.AverageSecondsPerPlayedGame)), Loc.Get("Stats.Tile.AverageDetail"), ""),
            new StatTile(Loc.Get("Detail.Sessions"), stats.TotalSessions.ToString("#,0", inv), Loc.Get("Stats.Tile.SessionsDetail"), ""),
            new StatTile(Loc.Get("Stats.Tile.Longest"), stats.LongestSession is null ? "—" : Format.Playtime(stats.LongestSession.Seconds),
                stats.LongestSession?.Name ?? Loc.Get("Home.Tile.NoSessionsYet"), ""),
        ]);

        Activity = stats.Last30Days
            .Select((d, i) => new ChartPoint(
                d.Date.Day.ToString(inv),
                d.Seconds,
                $"{d.Date.ToString(Loc.Get("Stats.ChartDayFormat"), inv)} · {(d.Seconds > 0 ? Format.Playtime(d.Seconds) : Loc.Get("Stats.NoPlay"))}",
                i % 5 == 4 || i == 0))
            .ToList();
        var activeDays = stats.Last30Days.Count(d => d.Seconds > 0);
        var monthTotal = stats.Last30Days.Sum(d => d.Seconds);
        ActivitySummary = monthTotal > 0
            ? Loc.Format("Stats.ActivitySummary", Format.Playtime(monthTotal), Loc.Count("Count.Day", activeDays))
            : Loc.Get("Stats.NoSessions30");

        var max = stats.TopGames.Count == 0 ? 1 : stats.TopGames.Max(t => t.Seconds);
        HomeViewModel.Replace(TopGames, stats.TopGames.Select((t, i) =>
            new TopGameRow(i + 1, _items.Find(t.GameId), t.Name, Format.Playtime(t.Seconds), (double)t.Seconds / max)));

        var segments = new List<ChartSegment>();
        PlatformLegend.Clear();
        for (var i = 0; i < stats.Platforms.Count; i++)
        {
            var platform = stats.Platforms[i];
            var brush = new SolidColorBrush(SegmentColors[i % SegmentColors.Length]);
            brush.Freeze();
            segments.Add(new ChartSegment(Loc.PlatformName(platform.Platform), platform.Games, brush));
            var share = stats.TotalGames == 0 ? 0 : 100.0 * platform.Games / stats.TotalGames;
            PlatformLegend.Add(new LegendRow(Loc.PlatformName(platform.Platform), $"{platform.Games}  ·  {Loc.Format("Stats.Share", share.ToString("0", inv))}", brush));
        }

        PlatformSegments = segments;
        PlatformTotal = stats.TotalGames.ToString("#,0", inv);
    }
}

public sealed partial class DiscoverViewModel(Navigator navigator) : PageViewModel
{
    public override PageKey Key => PageKey.Discover;

    [CommunityToolkit.Mvvm.Input.RelayCommand]
    private void OpenLibrary() => navigator.Go(PageKey.Library);
}
