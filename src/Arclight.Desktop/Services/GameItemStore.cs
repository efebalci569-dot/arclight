using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Core.Tracking;
using Arclight.Desktop.ViewModels;

namespace Arclight.Desktop.Services;

/// <summary>Keeps one <see cref="GameItemViewModel"/> per game and keeps them in sync with the library and tracker.</summary>
public sealed class GameItemStore
{
    private readonly LibraryService _library;
    private readonly GameActions _actions;
    private readonly TrackingService _tracking;
    private readonly Dictionary<long, GameItemViewModel> _items = [];

    public GameItemStore(LibraryService library, GameActions actions, TrackingService tracking)
    {
        _library = library;
        _actions = actions;
        _tracking = tracking;
        _library.Changed += OnLibraryChanged;
        _tracking.TrackerEventRaised += OnTrackerEvent;
        Loc.LanguageChanged += (_, _) =>
        {
            foreach (var item in _items.Values)
            {
                item.Refresh();
            }
        };
    }

    /// <summary>Raised on the UI thread after items changed (pages re-run their queries).</summary>
    public event EventHandler<LibraryChangedEventArgs>? Changed;

    /// <summary>Raised on the UI thread when a game starts or stops running.</summary>
    public event EventHandler? RunningChanged;

    public IReadOnlyList<GameItemViewModel> All => _library.Games.Select(Get).ToList();

    public IReadOnlyList<GameItemViewModel> Running => _items.Values.Where(i => i.IsRunning).ToList();

    public GameItemViewModel Get(Game game)
    {
        if (!_items.TryGetValue(game.Id, out var item))
        {
            item = new GameItemViewModel(game, _actions) { IsRunning = _tracking.RunningGames.Contains(game.Id) };
            _items[game.Id] = item;
        }

        return item;
    }

    public GameItemViewModel? Find(long id) => _library.FindGame(id) is { } game ? Get(game) : null;

    private void OnLibraryChanged(object? sender, LibraryChangedEventArgs e)
    {
        switch (e.Kind)
        {
            case LibraryChangeKind.Reloaded:
                _items.Clear();
                break;
            case LibraryChangeKind.GamesRemoved:
                foreach (var id in e.GameIds)
                {
                    _items.Remove(id);
                }

                break;
            case LibraryChangeKind.CollectionsChanged:
                foreach (var item in _items.Values)
                {
                    item.RefreshCollections();
                }

                break;
            case LibraryChangeKind.PlaytimeChanged or LibraryChangeKind.GamesUpdated:
                foreach (var id in e.GameIds)
                {
                    if (_items.TryGetValue(id, out var item))
                    {
                        item.Refresh();
                    }
                }

                break;
        }

        Changed?.Invoke(this, e);
    }

    private void OnTrackerEvent(object? sender, TrackingEventArgs e)
    {
        if (e.Event is not (SessionStarted or SessionEnded or SessionDiscarded))
        {
            return;
        }

        System.Windows.Application.Current?.Dispatcher.BeginInvoke(() =>
        {
            if (_library.FindGame(e.Event.GameId) is { } game)
            {
                Get(game).IsRunning = e.Event is SessionStarted;
            }

            RunningChanged?.Invoke(this, EventArgs.Empty);
        });
    }
}
