using Arclight.Core.Data;
using Arclight.Core.Interfaces;
using Arclight.Core.Library;
using Arclight.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arclight.Core.Services;

public enum LibraryChangeKind
{
    Reloaded,
    GamesAdded,
    GamesUpdated,
    GamesRemoved,
    CollectionsChanged,
    SettingsChanged,
    PlaytimeChanged,
}

public sealed class LibraryChangedEventArgs(LibraryChangeKind kind, IReadOnlyCollection<long>? gameIds = null) : EventArgs
{
    public LibraryChangeKind Kind { get; } = kind;

    public IReadOnlyCollection<long> GameIds { get; } = gameIds ?? [];
}

/// <summary>
/// The single in-memory source of truth for games, collections and settings. Database work runs on
/// background threads; in-memory changes and <see cref="Changed"/> events are marshalled through the
/// dispatcher supplied by the host (the WPF UI thread in the desktop app).
/// </summary>
public sealed class LibraryService
{
    private readonly GameRepository _games;
    private readonly CollectionRepository _collections;
    private readonly SessionRepository _sessions;
    private readonly SettingsRepository _settings;
    private readonly IClock _clock;
    private readonly Func<Action, Task> _dispatch;
    private readonly ILogger _logger;
    private volatile List<Game> _gameList = [];
    private volatile Dictionary<long, Game> _gameIndex = [];
    private List<GameCollection> _collectionList = [];

    public LibraryService(
        GameRepository games,
        CollectionRepository collections,
        SessionRepository sessions,
        SettingsRepository settings,
        IClock clock,
        Func<Action, Task>? dispatch = null,
        ILogger<LibraryService>? logger = null)
    {
        _games = games;
        _collections = collections;
        _sessions = sessions;
        _settings = settings;
        _clock = clock;
        _dispatch = dispatch ?? (action => { action(); return Task.CompletedTask; });
        _logger = logger ?? NullLogger<LibraryService>.Instance;
    }

    public event EventHandler<LibraryChangedEventArgs>? Changed;

    public IReadOnlyList<Game> Games => _gameList;

    public IReadOnlyList<GameCollection> Collections => _collectionList;

    public AppSettings Settings { get; private set; } = new();

    public IClock Clock => _clock;

    public Game? FindGame(long id) => _gameIndex.GetValueOrDefault(id);

    public IReadOnlyList<string> AllTags =>
        _gameList.SelectMany(g => g.Tags).Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.OrdinalIgnoreCase).ToList();

    public IReadOnlyList<string> AllGenres =>
        _gameList.SelectMany(g => LibraryFilter.SplitGenres(g.Genre)).Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase).ToList();

    public LibraryContext CreateContext() => LibraryContext.FromCollections(_clock.UtcNow, _collectionList);

    public async Task LoadAsync()
    {
        var (games, collections, settings) = await Task.Run(() => (_games.GetAll(), _collections.GetAll(), _settings.LoadAppSettings()));
        await _dispatch(() =>
        {
            SetGames(games);
            _collectionList = collections;
            Settings = settings;
            Raise(LibraryChangeKind.Reloaded);
        });
    }

    public async Task<Game> AddGameAsync(Game game)
    {
        if (game.DateAdded == default)
        {
            game.DateAdded = _clock.UtcNow;
        }

        await Task.Run(() => _games.Insert(game));
        await _dispatch(() =>
        {
            SetGames([.. _gameList, game]);
            Raise(LibraryChangeKind.GamesAdded, [game.Id]);
        });
        _logger.LogInformation("Added game {Name} ({Platform})", game.Name, game.Platform);
        return game;
    }

    public async Task<IReadOnlyList<Game>> AddGamesAsync(IReadOnlyCollection<Game> games)
    {
        if (games.Count == 0)
        {
            return [];
        }

        var inserted = await Task.Run(() => _games.InsertMany(games));
        await _dispatch(() =>
        {
            SetGames([.. _gameList, .. inserted]);
            Raise(LibraryChangeKind.GamesAdded, inserted.Select(g => g.Id).ToList());
        });
        return inserted;
    }

    /// <summary>Saves user edits. The shared instance is updated in place so every page sees the change.</summary>
    public async Task UpdateGameAsync(Game edited)
    {
        await Task.Run(() => _games.UpdateDetails(edited));
        await _dispatch(() =>
        {
            if (_gameIndex.TryGetValue(edited.Id, out var shared))
            {
                CopyEditableFields(edited, shared);
            }

            Raise(LibraryChangeKind.GamesUpdated, [edited.Id]);
        });
    }

    public Task SetFavoriteAsync(long id, bool value) =>
        Mutate(id, () => _games.SetFavorite(id, value), g => g.IsFavorite = value);

    public Task SetRatingAsync(long id, int? rating) =>
        Mutate(id, () => _games.SetRating(id, rating), g => g.PersonalRating = rating);

    public Task SetTrackingEnabledAsync(long id, bool enabled) =>
        Mutate(id, () => _games.SetTrackingEnabled(id, enabled), g => g.IsTrackingEnabled = enabled);

    public Task SetTagsAsync(long id, IReadOnlyList<string> tags) =>
        Mutate(id, () => _games.SetTags(id, tags), g => g.Tags = tags.Distinct(StringComparer.OrdinalIgnoreCase).ToList());

    public Task SetArtworkAsync(long id, string? cover, string? background, string? icon) =>
        Mutate(id, () => _games.SetArtwork(id, cover, background, icon), g =>
        {
            g.CoverImagePath = cover;
            g.BackgroundImagePath = background;
            g.IconImagePath = icon;
        });

    /// <summary>
    /// Records that a game was launched when its playtime cannot be tracked (tracking off, or the
    /// install folder is unknown). Only the "last played" date changes; no time is added.
    /// </summary>
    public Task MarkLaunchedAsync(long id)
    {
        var now = _clock.UtcNow;
        return Mutate(id, () => _games.SetLastPlayed(id, now), g => g.LastPlayed = now);
    }

    public Task SaveNotesAsync(long id, string? notes)
    {
        var now = _clock.UtcNow;
        return Mutate(id, () => _games.SetNotes(id, notes, now), g =>
        {
            g.Notes = notes;
            g.NotesUpdatedAt = now;
        });
    }

    /// <summary>Removes a game from Arclight. Nothing on disk is deleted or uninstalled.</summary>
    public async Task RemoveGameAsync(long id, bool hideFromFutureScans)
    {
        var game = FindGame(id);
        await Task.Run(() =>
        {
            if (hideFromFutureScans && game is { PlatformIdentifier: { Length: > 0 } platformId })
            {
                _settings.Ignore(ScanMerger.IdentityKey(game.Platform, platformId), game.Name, _clock.UtcNow);
            }

            _games.Delete(id);
        });

        await _dispatch(() =>
        {
            SetGames(_gameList.Where(g => g.Id != id).ToList());

            foreach (var collection in _collectionList)
            {
                collection.GameIds.Remove(id);
            }

            if (Settings.FeaturedGameId == id)
            {
                Settings = Settings with { FeaturedGameId = null };
                _ = Task.Run(() => _settings.SaveAppSettings(Settings));
            }

            Raise(LibraryChangeKind.GamesRemoved, [id]);
        });
    }

    public async Task ApplyInstallUpdatesAsync(IReadOnlyCollection<GameInstallUpdate> updates)
    {
        if (updates.Count == 0)
        {
            return;
        }

        await Task.Run(() => _games.ApplyInstallUpdates(updates));
        await _dispatch(() =>
        {
            foreach (var update in updates)
            {
                if (_gameIndex.TryGetValue(update.GameId, out var game))
                {
                    game.IsInstalled = update.IsInstalled;
                    game.InstallDirectory = update.InstallDirectory;
                    game.ExecutablePath = update.ExecutablePath;
                }
            }

            Raise(LibraryChangeKind.GamesUpdated, updates.Select(u => u.GameId).ToList());
        });
    }

    /// <summary>Re-reads tracking aggregates for a game after a session changed them.</summary>
    public async Task RefreshPlaytimeAsync(IReadOnlyCollection<long> ids)
    {
        var fresh = await Task.Run(() => ids.Select(_games.Get).Where(g => g is not null).Cast<Game>().ToList());
        await _dispatch(() =>
        {
            foreach (var source in fresh)
            {
                if (_gameIndex.TryGetValue(source.Id, out var game))
                {
                    game.TotalPlaytimeSeconds = source.TotalPlaytimeSeconds;
                    game.SessionCount = source.SessionCount;
                    game.LastPlayed = source.LastPlayed;
                }
            }

            Raise(LibraryChangeKind.PlaytimeChanged, ids);
        });
    }

    public Task<List<PlaySession>> GetSessionsAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, long? gameId = null, int? limit = null) =>
        Task.Run(() => _sessions.GetSessions(from, to, gameId, limit));

    public async Task ClearPlayHistoryAsync()
    {
        await Task.Run(_sessions.ClearHistory);
        await _dispatch(() => Raise(LibraryChangeKind.PlaytimeChanged, _gameList.Select(g => g.Id).ToList()));
    }

    public async Task ResetStatisticsAsync()
    {
        await Task.Run(_sessions.ResetStatistics);
        await _dispatch(() =>
        {
            foreach (var game in _gameList)
            {
                game.TotalPlaytimeSeconds = 0;
                game.SessionCount = 0;
                game.LastPlayed = null;
            }

            Raise(LibraryChangeKind.PlaytimeChanged, _gameList.Select(g => g.Id).ToList());
        });
    }

    // ---------------------------------------------------------------- collections

    public async Task<GameCollection> CreateCollectionAsync(string name, string? description = null, string? cover = null)
    {
        var collection = new GameCollection { Name = name.Trim(), Description = description, CoverImagePath = cover, CreatedAt = _clock.UtcNow };
        await Task.Run(() => _collections.Insert(collection));
        await _dispatch(() =>
        {
            _collectionList.Add(collection);
            Raise(LibraryChangeKind.CollectionsChanged);
        });
        return collection;
    }

    public async Task UpdateCollectionAsync(GameCollection edited)
    {
        await Task.Run(() => _collections.Update(edited));
        await _dispatch(() =>
        {
            if (_collectionList.FirstOrDefault(c => c.Id == edited.Id) is { } shared)
            {
                shared.Name = edited.Name.Trim();
                shared.Description = edited.Description;
                shared.CoverImagePath = edited.CoverImagePath;
            }

            Raise(LibraryChangeKind.CollectionsChanged);
        });
    }

    public async Task DeleteCollectionAsync(long id)
    {
        await Task.Run(() => _collections.Delete(id));
        await _dispatch(() =>
        {
            _collectionList.RemoveAll(c => c.Id == id);
            Raise(LibraryChangeKind.CollectionsChanged);
        });
    }

    public async Task ReorderCollectionsAsync(IReadOnlyList<long> orderedIds)
    {
        await Task.Run(() => _collections.SetOrder(orderedIds));
        await _dispatch(() =>
        {
            _collectionList = [.. _collectionList.OrderBy(c => IndexOf(orderedIds, c.Id))];
            for (var i = 0; i < _collectionList.Count; i++)
            {
                _collectionList[i].SortOrder = i;
            }

            Raise(LibraryChangeKind.CollectionsChanged);
        });
    }

    public async Task AddToCollectionAsync(long collectionId, IReadOnlyCollection<long> gameIds)
    {
        await Task.Run(() => _collections.AddGames(collectionId, gameIds, _clock.UtcNow));
        await _dispatch(() =>
        {
            if (_collectionList.FirstOrDefault(c => c.Id == collectionId) is { } collection)
            {
                foreach (var id in gameIds.Where(id => !collection.GameIds.Contains(id)))
                {
                    collection.GameIds.Add(id);
                }
            }

            Raise(LibraryChangeKind.CollectionsChanged, gameIds);
        });
    }

    public async Task RemoveFromCollectionAsync(long collectionId, long gameId)
    {
        await Task.Run(() => _collections.RemoveGame(collectionId, gameId));
        await _dispatch(() =>
        {
            _collectionList.FirstOrDefault(c => c.Id == collectionId)?.GameIds.Remove(gameId);
            Raise(LibraryChangeKind.CollectionsChanged, [gameId]);
        });
    }

    public IReadOnlyList<GameCollection> CollectionsContaining(long gameId) =>
        _collectionList.Where(c => c.GameIds.Contains(gameId)).ToList();

    // ---------------------------------------------------------------- settings

    public async Task UpdateSettingsAsync(Func<AppSettings, AppSettings> change)
    {
        AppSettings updated = Settings;
        await _dispatch(() =>
        {
            updated = change(Settings);
            Settings = updated;
        });
        await Task.Run(() => _settings.SaveAppSettings(updated));
        await _dispatch(() => Raise(LibraryChangeKind.SettingsChanged));
    }

    public HashSet<string> GetIgnoredIdentityKeys() => _settings.GetIgnoredIdentityKeys();

    public Task<List<(string IdentityKey, string Name)>> GetIgnoredGamesAsync() => Task.Run(_settings.GetIgnoredGames);

    public Task UnignoreAsync(string identityKey) => Task.Run(() => _settings.Unignore(identityKey));

    public Task IgnoreAsync(string identityKey, string name) => Task.Run(() => _settings.Ignore(identityKey, name, _clock.UtcNow));

    // ---------------------------------------------------------------- helpers

    /// <summary>
    /// Copy-on-write: the tracker reads the game list from a background thread, so the list and index
    /// are replaced rather than mutated.
    /// </summary>
    private void SetGames(List<Game> games)
    {
        _gameIndex = games.ToDictionary(g => g.Id);
        _gameList = games;
    }

    private async Task Mutate(long id, Action persist, Action<Game> apply)
    {
        await Task.Run(persist);
        await _dispatch(() =>
        {
            if (_gameIndex.TryGetValue(id, out var game))
            {
                apply(game);
            }

            Raise(LibraryChangeKind.GamesUpdated, [id]);
        });
    }

    private void Raise(LibraryChangeKind kind, IReadOnlyCollection<long>? ids = null) =>
        Changed?.Invoke(this, new LibraryChangedEventArgs(kind, ids));

    private static int IndexOf(IReadOnlyList<long> ids, long id)
    {
        for (var i = 0; i < ids.Count; i++)
        {
            if (ids[i] == id)
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private static void CopyEditableFields(Game from, Game to)
    {
        to.Name = from.Name.Trim();
        to.Platform = from.Platform;
        to.PlatformIdentifier = from.PlatformIdentifier;
        to.LaunchMethod = from.LaunchMethod;
        to.LaunchTarget = from.LaunchTarget;
        to.LaunchArguments = from.LaunchArguments;
        to.WorkingDirectory = from.WorkingDirectory;
        to.ExecutablePath = from.ExecutablePath;
        to.InstallDirectory = from.InstallDirectory;
        to.CoverImagePath = from.CoverImagePath;
        to.BackgroundImagePath = from.BackgroundImagePath;
        to.IconImagePath = from.IconImagePath;
        to.Genre = from.Genre;
        to.ReleaseYear = from.ReleaseYear;
        to.Developer = from.Developer;
        to.Publisher = from.Publisher;
        to.Description = from.Description;
        to.IsInstalled = from.IsInstalled;
        to.IsFavorite = from.IsFavorite;
        to.IsTrackingEnabled = from.IsTrackingEnabled;
        to.PersonalRating = from.PersonalRating;
        to.Notes = from.Notes;
        to.NotesUpdatedAt = from.NotesUpdatedAt;
        to.Tags = [.. from.Tags];
    }
}
