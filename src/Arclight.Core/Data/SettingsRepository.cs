using System.Text.Json;
using System.Text.Json.Serialization;
using Arclight.Core.Models;

namespace Arclight.Core.Data;

/// <summary>Key/value settings plus the scan "ignore" list and artwork cache bookkeeping.</summary>
public sealed class SettingsRepository(ArclightDatabase database)
{
    private const string AppSettingsKey = "app.settings";

    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() },
    };

    public AppSettings LoadAppSettings()
    {
        var json = Get(AppSettingsKey);
        if (json is null)
        {
            return new AppSettings();
        }

        try
        {
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
        }
        catch (JsonException)
        {
            // A damaged settings blob must never stop the app from starting.
            return new AppSettings();
        }
    }

    public void SaveAppSettings(AppSettings settings) => Set(AppSettingsKey, JsonSerializer.Serialize(settings, JsonOptions));

    public string? Get(string key)
    {
        using var connection = database.Open();
        return connection.Scalar("SELECT value FROM settings WHERE key = $k", null, ("$k", key)) as string;
    }

    public void Set(string key, string value)
    {
        using var connection = database.Open();
        connection.Execute(
            "INSERT INTO settings (key, value) VALUES ($k, $v) ON CONFLICT (key) DO UPDATE SET value = excluded.value",
            ("$k", key), ("$v", value));
    }

    public HashSet<string> GetIgnoredIdentityKeys()
    {
        using var connection = database.Open();
        return connection.Query("SELECT identity_key FROM ignored_games", r => r.GetString(0)).ToHashSet();
    }

    public List<(string IdentityKey, string Name)> GetIgnoredGames()
    {
        using var connection = database.Open();
        return connection.Query("SELECT identity_key, name FROM ignored_games ORDER BY name COLLATE NOCASE", r => (r.GetString(0), r.GetString(1)));
    }

    public void Ignore(string identityKey, string name, DateTimeOffset at)
    {
        using var connection = database.Open();
        connection.Execute(
            "INSERT INTO ignored_games (identity_key, name, ignored_at) VALUES ($k, $n, $at) ON CONFLICT (identity_key) DO NOTHING",
            ("$k", identityKey), ("$n", name), ("$at", at.ToDb()));
    }

    public void Unignore(string identityKey)
    {
        using var connection = database.Open();
        connection.Execute("DELETE FROM ignored_games WHERE identity_key = $k", ("$k", identityKey));
    }

    public ImageCacheEntry? GetImageCacheEntry(string cacheKey)
    {
        using var connection = database.Open();
        return connection.Query(
            "SELECT cache_key, source, local_path, status, updated_at FROM image_cache WHERE cache_key = $k",
            r => new ImageCacheEntry(r.GetString(0), r.GetString(1), r.IsDBNull(2) ? null : r.GetString(2), r.GetString(3), SqliteHelpers.ParseDate(r.GetString(4))),
            ("$k", cacheKey)).FirstOrDefault();
    }

    public void SetImageCacheEntry(ImageCacheEntry entry)
    {
        using var connection = database.Open();
        connection.Execute("""
            INSERT INTO image_cache (cache_key, source, local_path, status, updated_at) VALUES ($k, $s, $p, $st, $at)
            ON CONFLICT (cache_key) DO UPDATE SET source = excluded.source, local_path = excluded.local_path,
                status = excluded.status, updated_at = excluded.updated_at
            """,
            ("$k", entry.CacheKey), ("$s", entry.Source), ("$p", entry.LocalPath), ("$st", entry.Status), ("$at", entry.UpdatedAt.ToDb()));
    }

    public void ClearImageCacheEntries()
    {
        using var connection = database.Open();
        connection.Execute("DELETE FROM image_cache");
    }
}

/// <summary>Bookkeeping for one cached image. Status is "ok" or "failed" (failed sources are retried after a cool-down).</summary>
public sealed record ImageCacheEntry(string CacheKey, string Source, string? LocalPath, string Status, DateTimeOffset UpdatedAt);
