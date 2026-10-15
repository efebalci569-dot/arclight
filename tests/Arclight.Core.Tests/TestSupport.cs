using Arclight.Core.Data;
using Arclight.Core.Interfaces;
using Arclight.Core.Models;

namespace Arclight.Core.Tests;

internal sealed class FakeClock(DateTimeOffset start, TimeZoneInfo? zone = null) : IClock
{
    public DateTimeOffset UtcNow { get; set; } = start;

    public TimeZoneInfo LocalZone { get; } = zone ?? TimeZoneInfo.Utc;

    public void Advance(TimeSpan by) => UtcNow += by;
}

internal sealed class FakeProcesses : IProcessSnapshotProvider
{
    public List<RunningProcess> Running { get; } = [];

    public IReadOnlyList<RunningProcess> GetRunningProcesses() => Running.ToList();
}

/// <summary>A database in a unique temp file, deleted when the test finishes.</summary>
internal sealed class TempDatabase : IDisposable
{
    public TempDatabase()
    {
        Directory = Path.Combine(Path.GetTempPath(), "arclight-tests", Guid.NewGuid().ToString("N"));
        System.IO.Directory.CreateDirectory(Directory);
        Database = new ArclightDatabase(Path.Combine(Directory, "test.db"));
        Database.Initialize();
        Games = new GameRepository(Database);
        Sessions = new SessionRepository(Database);
        Collections = new CollectionRepository(Database);
        Settings = new SettingsRepository(Database);
    }

    public string Directory { get; }

    public ArclightDatabase Database { get; }

    public GameRepository Games { get; }

    public SessionRepository Sessions { get; }

    public CollectionRepository Collections { get; }

    public SettingsRepository Settings { get; }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        try
        {
            System.IO.Directory.Delete(Directory, recursive: true);
        }
        catch (IOException)
        {
        }
    }
}

internal static class Games
{
    public static readonly DateTimeOffset BaseTime = new(2026, 10, 14, 12, 0, 0, TimeSpan.Zero);

    public static Game Make(
        long id,
        string name,
        GamePlatform platform = GamePlatform.Manual,
        bool installed = true,
        bool favorite = false,
        long playtime = 0,
        DateTimeOffset? lastPlayed = null,
        string? genre = null,
        int? year = null,
        string[]? tags = null,
        string? notes = null,
        DateTimeOffset? added = null) => new()
        {
            Id = id,
            Name = name,
            Platform = platform,
            IsInstalled = installed,
            IsFavorite = favorite,
            TotalPlaytimeSeconds = playtime,
            LastPlayed = lastPlayed,
            Genre = genre,
            ReleaseYear = year,
            Tags = [.. tags ?? []],
            Notes = notes,
            DateAdded = added ?? BaseTime.AddDays(-id),
        };
}
