using System.Collections.ObjectModel;
using Arclight.Core.Formatting;
using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arclight.Desktop.ViewModels;

public sealed record SessionRow(GameItemViewModel? Game, string GameName, string DateText, string TimeText, string DurationText, string SourceText, string SourceGlyph);

public sealed partial class RecentlyPlayedViewModel : PageViewModel
{
    private readonly LibraryService _library;
    private readonly GameItemStore _items;
    private readonly Navigator _navigator;
    private bool _refreshQueued;
    private bool _isActive;

    public RecentlyPlayedViewModel(LibraryService library, GameItemStore items, Navigator navigator)
    {
        _library = library;
        _items = items;
        _navigator = navigator;
        _items.Changed += (_, _) => QueueRefresh();
        _items.RunningChanged += (_, _) => QueueRefresh();
        Loc.LanguageChanged += (_, _) => QueueRefresh();
    }

    public override PageKey Key => PageKey.RecentlyPlayed;

    public ObservableCollection<GameItemViewModel> Games { get; } = [];

    public ObservableCollection<SessionRow> Sessions { get; } = [];

    [ObservableProperty]
    private bool _isEmpty;

    [ObservableProperty]
    private bool _hasSessions;

    public override void OnNavigatedTo(object? parameter)
    {
        _isActive = true;
        Refresh();
    }

    public override void OnNavigatedFrom() => _isActive = false;

    private void QueueRefresh()
    {
        if (_refreshQueued || !_isActive)
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
        HomeViewModel.Replace(Games, _library.Games
            .Where(g => g.LastPlayed is not null || _items.Get(g).IsRunning)
            .OrderByDescending(g => _items.Get(g).IsRunning)
            .ThenByDescending(g => g.LastPlayed)
            .Select(_items.Get));
        IsEmpty = Games.Count == 0;

        var sessions = await _library.GetSessionsAsync(limit: 40);
        Sessions.Clear();
        foreach (var session in sessions)
        {
            var item = _items.Find(session.GameId);
            var local = session.StartedAt.ToLocalTime();
            Sessions.Add(new SessionRow(
                item,
                item?.Name ?? Loc.Get("Recent.RemovedGame"),
                local.Date == DateTime.Today ? Loc.Get("Recent.Today") : local.Date == DateTime.Today.AddDays(-1) ? Format.Strings.Yesterday : local.ToString(Loc.Get("Recent.DayFormat"), Loc.Culture),
                local.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture),
                Format.Playtime(session.DurationSeconds),
                Loc.Get(session.Source == SessionSource.BackgroundWatcher ? "Session.Source.Watcher" : "Session.Source.Arclight"),
                session.Source == SessionSource.BackgroundWatcher ? "" : ""));
        }

        HasSessions = Sessions.Count > 0;
    }

    [RelayCommand]
    private void OpenLibrary() => _navigator.Go(PageKey.Library);
}
