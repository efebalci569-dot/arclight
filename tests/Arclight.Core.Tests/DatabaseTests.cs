using Arclight.Core.Data;
using Arclight.Core.Models;

namespace Arclight.Core.Tests;

public sealed class DatabaseTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private static readonly DateTimeOffset T0 = Games.BaseTime;

    public void Dispose() => _db.Dispose();

    private Game Insert(string name, GamePlatform platform = GamePlatform.Manual, string? platformId = null, params string[] tags)
    {
        var game = new Game
        {
            Name = name,
            Platform = platform,
            PlatformIdentifier = platformId,
            LaunchMethod = LaunchMethod.Executable,
            ExecutablePath = $@"C:\Games\{name}\{name}.exe",
            DateAdded = T0,
            Tags = [.. tags],
        };
        _db.Games.Insert(game);
        return game;
    }

    [Fact]
    public void Games_survive_a_reopen()
    {
        var game = Insert("Hades", GamePlatform.Steam, "1145360", "Roguelike", "Favorites");

        var reopened = new GameRepository(new ArclightDatabase(_db.Database.FilePath));
        var loaded = Assert.Single(reopened.GetAll());

        Assert.Equal(game.Id, loaded.Id);
        Assert.Equal("Hades", loaded.Name);
        Assert.Equal(GamePlatform.Steam, loaded.Platform);
        Assert.Equal("1145360", loaded.PlatformIdentifier);
        Assert.Equal(["Favorites", "Roguelike"], loaded.Tags);
        Assert.Equal(T0, loaded.DateAdded);
    }

    [Fact]
    public void Initialize_is_idempotent()
    {
        Insert("Celeste");
        _db.Database.Initialize();
        Assert.Single(_db.Games.GetAll());
    }

    [Fact]
    public void Duplicate_platform_ids_are_rejected_with_a_readable_error()
    {
        Insert("Hades", GamePlatform.Steam, "1145360");
        var ex = Assert.Throws<DuplicateGameException>(() => Insert("Hades copy", GamePlatform.Steam, "1145360"));
        Assert.Contains("already in your library", ex.Message);
    }

    [Fact]
    public void Editing_a_game_never_overwrites_tracked_playtime()
    {
        var game = Insert("Hades");
        _db.Sessions.Start(game.Id, T0, SessionSource.ArclightLaunch);
        _db.Sessions.Complete(game.Id, T0, T0.AddHours(1));

        // A stale in-memory copy (playtime 0) is saved after the session finished.
        game.Name = "Hades (renamed)";
        game.TotalPlaytimeSeconds = 0;
        _db.Games.UpdateDetails(game);

        var loaded = _db.Games.Get(game.Id)!;
        Assert.Equal("Hades (renamed)", loaded.Name);
        Assert.Equal(3600, loaded.TotalPlaytimeSeconds);
        Assert.Equal(1, loaded.SessionCount);
        Assert.Equal(T0.AddHours(1), loaded.LastPlayed);
    }

    [Fact]
    public void A_session_is_counted_exactly_once()
    {
        var game = Insert("Hades");
        _db.Sessions.Start(game.Id, T0, SessionSource.ArclightLaunch);
        _db.Sessions.Start(game.Id, T0, SessionSource.ArclightLaunch); // duplicate start is ignored

        Assert.True(_db.Sessions.Complete(game.Id, T0, T0.AddMinutes(30)));
        Assert.False(_db.Sessions.Complete(game.Id, T0, T0.AddMinutes(30)));

        Assert.Equal(1800, _db.Games.Get(game.Id)!.TotalPlaytimeSeconds);
        Assert.Single(_db.Sessions.GetSessions());
    }

    [Fact]
    public void Interrupted_sessions_are_recovered_at_their_last_heartbeat()
    {
        var game = Insert("Hades");
        _db.Sessions.Start(game.Id, T0, SessionSource.ArclightLaunch);
        _db.Sessions.Heartbeat(game.Id, T0, T0.AddMinutes(20));

        var shortGame = Insert("Celeste");
        _db.Sessions.Start(shortGame.Id, T0, SessionSource.BackgroundWatcher);
        _db.Sessions.Heartbeat(shortGame.Id, T0, T0.AddSeconds(5));

        // Arclight crashed; hours pass before it starts again.
        Assert.Equal(1, _db.Sessions.RecoverInterrupted(TimeSpan.FromSeconds(30)));
        Assert.Equal(0, _db.Sessions.RecoverInterrupted(TimeSpan.FromSeconds(30)));

        var session = Assert.Single(_db.Sessions.GetSessions());
        Assert.Equal(SessionStatus.Recovered, session.Status);
        Assert.Equal(T0.AddMinutes(20), session.EndedAt);
        Assert.Equal(1200, _db.Games.Get(game.Id)!.TotalPlaytimeSeconds);
        Assert.Equal(0, _db.Games.Get(shortGame.Id)!.TotalPlaytimeSeconds);
    }

    [Fact]
    public void Clearing_history_keeps_totals_but_reset_clears_everything()
    {
        var game = Insert("Hades");
        _db.Sessions.Start(game.Id, T0, SessionSource.ArclightLaunch);
        _db.Sessions.Complete(game.Id, T0, T0.AddHours(2));

        _db.Sessions.ClearHistory();
        Assert.Empty(_db.Sessions.GetSessions());
        Assert.Equal(7200, _db.Games.Get(game.Id)!.TotalPlaytimeSeconds);

        _db.Sessions.ResetStatistics();
        var reset = _db.Games.Get(game.Id)!;
        Assert.Equal(0, reset.TotalPlaytimeSeconds);
        Assert.Equal(0, reset.SessionCount);
        Assert.Null(reset.LastPlayed);
    }

    [Fact]
    public void Deleting_a_collection_keeps_its_games()
    {
        var a = Insert("A");
        var b = Insert("B");
        var collection = new GameCollection { Name = "Backlog", CreatedAt = T0 };
        _db.Collections.Insert(collection);
        _db.Collections.AddGames(collection.Id, [a.Id, b.Id], T0);

        Assert.Equal([a.Id, b.Id], _db.Collections.GetAll().Single().GameIds);

        _db.Collections.Delete(collection.Id);
        Assert.Empty(_db.Collections.GetAll());
        Assert.Equal(2, _db.Games.GetAll().Count);
    }

    [Fact]
    public void Removing_a_game_removes_its_memberships_tags_and_sessions_only()
    {
        var a = Insert("A", tags: "Solo");
        var b = Insert("B", tags: "Coop");
        var collection = new GameCollection { Name = "Backlog", CreatedAt = T0 };
        _db.Collections.Insert(collection);
        _db.Collections.AddGames(collection.Id, [a.Id, b.Id], T0);

        _db.Games.Delete(a.Id);

        Assert.Equal([b.Id], _db.Collections.GetAll().Single().GameIds);
        Assert.Equal(["Coop"], _db.Games.GetAllTagNames());
    }

    [Fact]
    public void Collections_can_be_reordered()
    {
        var first = new GameCollection { Name = "First", CreatedAt = T0 };
        var second = new GameCollection { Name = "Second", CreatedAt = T0 };
        _db.Collections.Insert(first);
        _db.Collections.Insert(second);

        _db.Collections.SetOrder([second.Id, first.Id]);

        Assert.Equal(["Second", "First"], _db.Collections.GetAll().Select(c => c.Name));
    }

    [Fact]
    public void Settings_roundtrip_and_defaults()
    {
        Assert.Equal("Player", _db.Settings.LoadAppSettings().DisplayName);

        var settings = new AppSettings { DisplayName = "Efe", LibraryViewMode = LibraryViewMode.List, ExcludedProcesses = ["helper.exe"] };
        _db.Settings.SaveAppSettings(settings);

        var loaded = _db.Settings.LoadAppSettings();
        Assert.Equal("Efe", loaded.DisplayName);
        Assert.Equal(LibraryViewMode.List, loaded.LibraryViewMode);
        Assert.Equal(["helper.exe"], loaded.ExcludedProcesses);
        Assert.True(loaded.ScanOnStartup);
    }

    [Fact]
    public void Corrupt_settings_fall_back_to_defaults()
    {
        _db.Settings.Set("app.settings", "{not json");
        Assert.Equal("Player", _db.Settings.LoadAppSettings().DisplayName);
    }

    [Fact]
    public void Ignored_games_roundtrip()
    {
        _db.Settings.Ignore("steam:228980", "Steamworks", T0);
        Assert.Contains("steam:228980", _db.Settings.GetIgnoredIdentityKeys());
        _db.Settings.Unignore("steam:228980");
        Assert.Empty(_db.Settings.GetIgnoredIdentityKeys());
    }

    [Fact]
    public void Newer_schema_is_refused()
    {
        using (var connection = _db.Database.Open())
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 999;";
            command.ExecuteNonQuery();
        }

        var ex = Assert.Throws<DatabaseException>(() => new ArclightDatabase(_db.Database.FilePath).Initialize());
        Assert.Contains("newer version", ex.Message);
    }
}
