using Arclight.Core.Library;
using Arclight.Core.Models;
using Microsoft.Data.Sqlite;

namespace Arclight.Core.Data;

public sealed class DuplicateGameException(string message) : Exception(message);

/// <summary>
/// Persists games. Playtime aggregates (total, session count, last played) are written only by
/// <see cref="SessionRepository"/> so editing a game can never overwrite tracked time.
/// </summary>
public sealed class GameRepository(ArclightDatabase database)
{
    private const string SelectColumns = """
        id, name, platform, platform_id, launch_method, launch_target, launch_arguments, working_directory,
        executable_path, install_directory, cover_image_path, background_image_path, icon_image_path, genre,
        release_year, developer, publisher, description, is_installed, is_favorite, tracking_enabled, date_added,
        last_played, total_playtime_seconds, session_count, personal_rating, notes, notes_updated_at, source
        """;

    public List<Game> GetAll()
    {
        using var connection = database.Open();
        var games = connection.Query($"SELECT {SelectColumns} FROM games", Map);
        var tags = LoadTags(connection, null);
        foreach (var game in games)
        {
            if (tags.TryGetValue(game.Id, out var list))
            {
                game.Tags = list;
            }
        }

        return games;
    }

    public Game? Get(long id)
    {
        using var connection = database.Open();
        var game = connection.Query($"SELECT {SelectColumns} FROM games WHERE id = $id", Map, ("$id", id)).FirstOrDefault();
        if (game is not null && LoadTags(connection, id).TryGetValue(id, out var tags))
        {
            game.Tags = tags;
        }

        return game;
    }

    public long Insert(Game game)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        var id = Insert(connection, transaction, game);
        transaction.Commit();
        return id;
    }

    /// <summary>Adds several games atomically (used by the scan window).</summary>
    public IReadOnlyList<Game> InsertMany(IEnumerable<Game> games)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        var inserted = new List<Game>();
        foreach (var game in games)
        {
            Insert(connection, transaction, game);
            inserted.Add(game);
        }

        transaction.Commit();
        return inserted;
    }

    /// <summary>Saves everything the user can edit. Does not touch playtime aggregates.</summary>
    public void UpdateDetails(Game game)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        try
        {
            connection.Execute("""
                UPDATE games SET
                    name = $name, platform = $platform, platform_id = $platform_id, launch_method = $launch_method,
                    launch_target = $launch_target, launch_arguments = $launch_arguments, working_directory = $working_directory,
                    executable_path = $executable_path, install_directory = $install_directory, cover_image_path = $cover,
                    background_image_path = $background, icon_image_path = $icon, genre = $genre, release_year = $release_year,
                    developer = $developer, publisher = $publisher, description = $description, is_installed = $is_installed,
                    is_favorite = $is_favorite, tracking_enabled = $tracking_enabled, personal_rating = $rating,
                    notes = $notes, notes_updated_at = $notes_updated_at
                WHERE id = $id
                """, transaction, [.. Parameters(game), ("$id", game.Id)]);
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new DuplicateGameException("Another game in your library already uses this platform id.");
        }

        SaveTags(connection, transaction, game.Id, game.Tags);
        transaction.Commit();
    }

    public void SetFavorite(long id, bool isFavorite)
    {
        using var connection = database.Open();
        connection.Execute("UPDATE games SET is_favorite = $v WHERE id = $id", ("$v", isFavorite), ("$id", id));
    }

    public void SetRating(long id, int? rating)
    {
        using var connection = database.Open();
        connection.Execute("UPDATE games SET personal_rating = $v WHERE id = $id", ("$v", rating), ("$id", id));
    }

    public void SetNotes(long id, string? notes, DateTimeOffset updatedAt)
    {
        using var connection = database.Open();
        connection.Execute("UPDATE games SET notes = $notes, notes_updated_at = $at WHERE id = $id",
            ("$notes", notes), ("$at", updatedAt.ToDb()), ("$id", id));
    }

    public void SetLastPlayed(long id, DateTimeOffset at)
    {
        using var connection = database.Open();
        connection.Execute("UPDATE games SET last_played = $at WHERE id = $id AND (last_played IS NULL OR last_played < $at)",
            ("$at", at.ToDb()), ("$id", id));
    }

    public void SetTrackingEnabled(long id, bool enabled)
    {
        using var connection = database.Open();
        connection.Execute("UPDATE games SET tracking_enabled = $v WHERE id = $id", ("$v", enabled), ("$id", id));
    }

    public void SetTags(long id, IEnumerable<string> tags)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        SaveTags(connection, transaction, id, tags);
        transaction.Commit();
    }

    public void SetArtwork(long id, string? cover, string? background, string? icon)
    {
        using var connection = database.Open();
        connection.Execute(
            "UPDATE games SET cover_image_path = $c, background_image_path = $b, icon_image_path = $i WHERE id = $id",
            ("$c", cover), ("$b", background), ("$i", icon), ("$id", id));
    }

    public void ApplyInstallUpdates(IEnumerable<GameInstallUpdate> updates)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        foreach (var update in updates)
        {
            connection.Execute(
                "UPDATE games SET is_installed = $installed, install_directory = $dir, executable_path = $exe WHERE id = $id",
                transaction,
                ("$installed", update.IsInstalled), ("$dir", update.InstallDirectory), ("$exe", update.ExecutablePath), ("$id", update.GameId));
        }

        transaction.Commit();
    }

    /// <summary>Removes the game from the library only. Files on disk are never touched.</summary>
    public void Delete(long id)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        connection.Execute("DELETE FROM games WHERE id = $id", transaction, ("$id", id));
        DeleteOrphanTags(connection, transaction);
        transaction.Commit();
    }

    public List<string> GetAllTagNames()
    {
        using var connection = database.Open();
        return connection.Query("SELECT name FROM tags ORDER BY name COLLATE NOCASE", r => r.GetString(0));
    }

    private static long Insert(SqliteConnection connection, SqliteTransaction transaction, Game game)
    {
        try
        {
            var id = (long)connection.Scalar("""
                INSERT INTO games (
                    name, platform, platform_id, launch_method, launch_target, launch_arguments, working_directory,
                    executable_path, install_directory, cover_image_path, background_image_path, icon_image_path, genre,
                    release_year, developer, publisher, description, is_installed, is_favorite, tracking_enabled, personal_rating,
                    date_added, notes, notes_updated_at, source)
                VALUES (
                    $name, $platform, $platform_id, $launch_method, $launch_target, $launch_arguments, $working_directory,
                    $executable_path, $install_directory, $cover, $background, $icon, $genre,
                    $release_year, $developer, $publisher, $description, $is_installed, $is_favorite, $tracking_enabled, $rating,
                    $date_added, $notes, $notes_updated_at, $source)
                RETURNING id
                """,
                transaction,
                [.. Parameters(game), ("$date_added", game.DateAdded.ToDb()), ("$source", game.Source == GameSource.Scan ? "scan" : "manual")])!;
            game.Id = id;
        }
        catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
        {
            throw new DuplicateGameException($"\"{game.Name}\" is already in your library.");
        }

        SaveTags(connection, transaction, game.Id, game.Tags);
        return game.Id;
    }

    private static (string, object?)[] Parameters(Game game) =>
    [
        ("$name", game.Name.Trim()),
        ("$platform", PlatformInfo.Code(game.Platform)),
        ("$platform_id", string.IsNullOrWhiteSpace(game.PlatformIdentifier) ? null : game.PlatformIdentifier.Trim()),
        ("$launch_method", LaunchMethodCode(game.LaunchMethod)),
        ("$launch_target", game.LaunchTarget),
        ("$launch_arguments", game.LaunchArguments),
        ("$working_directory", game.WorkingDirectory),
        ("$executable_path", game.ExecutablePath),
        ("$install_directory", game.InstallDirectory),
        ("$cover", game.CoverImagePath),
        ("$background", game.BackgroundImagePath),
        ("$icon", game.IconImagePath),
        ("$genre", game.Genre),
        ("$release_year", game.ReleaseYear),
        ("$developer", game.Developer),
        ("$publisher", game.Publisher),
        ("$description", game.Description),
        ("$is_installed", game.IsInstalled),
        ("$is_favorite", game.IsFavorite),
        ("$tracking_enabled", game.IsTrackingEnabled),
        ("$rating", game.PersonalRating is >= 1 and <= 5 ? game.PersonalRating : null),
        ("$notes", game.Notes),
        ("$notes_updated_at", game.NotesUpdatedAt.ToDb()),
    ];

    private static void SaveTags(SqliteConnection connection, SqliteTransaction transaction, long gameId, IEnumerable<string> tags)
    {
        connection.Execute("DELETE FROM game_tags WHERE game_id = $id", transaction, ("$id", gameId));
        foreach (var tag in tags.Select(t => t.Trim()).Where(t => t.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            connection.Execute("INSERT INTO tags (name) VALUES ($name) ON CONFLICT (name) DO NOTHING", transaction, ("$name", tag));
            connection.Execute(
                "INSERT OR IGNORE INTO game_tags (game_id, tag_id) SELECT $game, id FROM tags WHERE name = $name",
                transaction, ("$game", gameId), ("$name", tag));
        }

        DeleteOrphanTags(connection, transaction);
    }

    private static void DeleteOrphanTags(SqliteConnection connection, SqliteTransaction transaction) =>
        connection.Execute("DELETE FROM tags WHERE id NOT IN (SELECT tag_id FROM game_tags)", transaction);

    private static Dictionary<long, List<string>> LoadTags(SqliteConnection connection, long? gameId)
    {
        var sql = "SELECT gt.game_id, t.name FROM game_tags gt JOIN tags t ON t.id = gt.tag_id" +
                  (gameId is null ? string.Empty : " WHERE gt.game_id = $id") + " ORDER BY t.name COLLATE NOCASE";
        var rows = connection.Query(sql, r => (GameId: r.GetInt64(0), Name: r.GetString(1)), ("$id", gameId));
        return rows.GroupBy(r => r.GameId).ToDictionary(g => g.Key, g => g.Select(r => r.Name).ToList());
    }

    internal static string LaunchMethodCode(LaunchMethod method) => method switch
    {
        LaunchMethod.Executable => "executable",
        LaunchMethod.LauncherUri => "uri",
        LaunchMethod.CustomCommand => "command",
        _ => "none",
    };

    internal static LaunchMethod ParseLaunchMethod(string? code) => code switch
    {
        "executable" => LaunchMethod.Executable,
        "uri" => LaunchMethod.LauncherUri,
        "command" => LaunchMethod.CustomCommand,
        _ => LaunchMethod.None,
    };

    private static Game Map(SqliteDataReader r) => new()
    {
        Id = r.GetInt64("id"),
        Name = r.GetString(r.GetOrdinal("name")),
        Platform = PlatformInfo.Parse(r.GetStringOrNull("platform")),
        PlatformIdentifier = r.GetStringOrNull("platform_id"),
        LaunchMethod = ParseLaunchMethod(r.GetStringOrNull("launch_method")),
        LaunchTarget = r.GetStringOrNull("launch_target"),
        LaunchArguments = r.GetStringOrNull("launch_arguments"),
        WorkingDirectory = r.GetStringOrNull("working_directory"),
        ExecutablePath = r.GetStringOrNull("executable_path"),
        InstallDirectory = r.GetStringOrNull("install_directory"),
        CoverImagePath = r.GetStringOrNull("cover_image_path"),
        BackgroundImagePath = r.GetStringOrNull("background_image_path"),
        IconImagePath = r.GetStringOrNull("icon_image_path"),
        Genre = r.GetStringOrNull("genre"),
        ReleaseYear = r.GetInt32OrNull("release_year"),
        Developer = r.GetStringOrNull("developer"),
        Publisher = r.GetStringOrNull("publisher"),
        Description = r.GetStringOrNull("description"),
        IsInstalled = r.GetBool("is_installed"),
        IsFavorite = r.GetBool("is_favorite"),
        IsTrackingEnabled = r.GetBool("tracking_enabled"),
        DateAdded = r.GetDate("date_added"),
        LastPlayed = r.GetDateOrNull("last_played"),
        TotalPlaytimeSeconds = r.GetInt64("total_playtime_seconds"),
        SessionCount = (int)r.GetInt64("session_count"),
        PersonalRating = r.GetInt32OrNull("personal_rating"),
        Notes = r.GetStringOrNull("notes"),
        NotesUpdatedAt = r.GetDateOrNull("notes_updated_at"),
        Source = r.GetStringOrNull("source") == "scan" ? GameSource.Scan : GameSource.Manual,
    };
}
