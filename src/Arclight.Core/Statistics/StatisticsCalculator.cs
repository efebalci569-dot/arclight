using Arclight.Core.Models;

namespace Arclight.Core.Statistics;

public sealed record GameTotal(long GameId, string Name, long Seconds);

public sealed record PlatformCount(GamePlatform Platform, int Games);

public sealed record DailyTotal(DateOnly Date, long Seconds);

public sealed record SessionHighlight(long GameId, string Name, DateTimeOffset StartedAt, long Seconds);

/// <summary>Everything the Home and Statistics pages show. All values come from local data only.</summary>
public sealed record LibraryStatistics
{
    public int TotalGames { get; init; }
    public int InstalledGames { get; init; }
    public long TotalPlaytimeSeconds { get; init; }
    public long ThisWeekSeconds { get; init; }
    public long ThisMonthSeconds { get; init; }
    public int TotalSessions { get; init; }
    public int GamesPlayedThisWeek { get; init; }
    public int PlayedGames { get; init; }
    public long AverageSecondsPerPlayedGame { get; init; }
    public GameTotal? MostPlayed { get; init; }
    public SessionHighlight? LongestSession { get; init; }
    public IReadOnlyList<GameTotal> TopGames { get; init; } = [];
    public IReadOnlyList<PlatformCount> Platforms { get; init; } = [];
    public IReadOnlyList<DailyTotal> Last30Days { get; init; } = [];

    public bool HasTrackedTime => TotalPlaytimeSeconds > 0;
}

/// <summary>
/// Computes statistics. Totals come from each game's verified aggregate; time-windowed values come
/// from individual sessions clipped to the window in the user's local time zone.
/// </summary>
public static class StatisticsCalculator
{
    public static LibraryStatistics Calculate(
        IReadOnlyCollection<Game> games,
        IReadOnlyCollection<PlaySession> sessions,
        DateTimeOffset now,
        TimeZoneInfo zone,
        int topCount = 5)
    {
        var names = games.ToDictionary(g => g.Id, g => g.Name);
        var localNow = TimeZoneInfo.ConvertTime(now, zone);
        var today = DateOnly.FromDateTime(localNow.DateTime);
        var weekStart = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
        var monthStart = new DateOnly(today.Year, today.Month, 1);

        var weekStartInstant = StartOfDay(weekStart, zone);
        var monthStartInstant = StartOfDay(monthStart, zone);
        var known = sessions.Where(s => names.ContainsKey(s.GameId)).ToList();

        var thisWeek = known.Sum(s => Overlap(s, weekStartInstant, now));
        var thisMonth = known.Sum(s => Overlap(s, monthStartInstant, now));
        var gamesThisWeek = known.Where(s => Overlap(s, weekStartInstant, now) > 0).Select(s => s.GameId).Distinct().Count();

        var days = new List<DailyTotal>(30);
        for (var i = 29; i >= 0; i--)
        {
            var day = today.AddDays(-i);
            var from = StartOfDay(day, zone);
            var to = StartOfDay(day.AddDays(1), zone);
            days.Add(new DailyTotal(day, known.Sum(s => Overlap(s, from, to))));
        }

        var played = games.Where(g => g.TotalPlaytimeSeconds > 0).ToList();
        var total = games.Sum(g => g.TotalPlaytimeSeconds);
        var top = played
            .OrderByDescending(g => g.TotalPlaytimeSeconds)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .Take(topCount)
            .Select(g => new GameTotal(g.Id, g.Name, g.TotalPlaytimeSeconds))
            .ToList();

        var longest = known.OrderByDescending(s => s.DurationSeconds).FirstOrDefault();

        return new LibraryStatistics
        {
            TotalGames = games.Count,
            InstalledGames = games.Count(g => g.IsInstalled),
            TotalPlaytimeSeconds = total,
            ThisWeekSeconds = thisWeek,
            ThisMonthSeconds = thisMonth,
            TotalSessions = games.Sum(g => g.SessionCount),
            GamesPlayedThisWeek = gamesThisWeek,
            PlayedGames = played.Count,
            AverageSecondsPerPlayedGame = played.Count == 0 ? 0 : total / played.Count,
            MostPlayed = top.FirstOrDefault(),
            LongestSession = longest is null || longest.DurationSeconds <= 0
                ? null
                : new SessionHighlight(longest.GameId, names[longest.GameId], longest.StartedAt, longest.DurationSeconds),
            TopGames = top,
            Platforms = games
                .GroupBy(g => g.Platform)
                .Select(g => new PlatformCount(g.Key, g.Count()))
                .OrderByDescending(p => p.Games)
                .ThenBy(p => p.Platform)
                .ToList(),
            Last30Days = days,
        };
    }

    /// <summary>Seconds of <paramref name="session"/> that fall inside [from, to).</summary>
    public static long Overlap(PlaySession session, DateTimeOffset from, DateTimeOffset to)
    {
        var end = session.EndedAt ?? session.LastHeartbeatAt;
        var start = session.StartedAt > from ? session.StartedAt : from;
        var stop = end < to ? end : to;
        return stop > start ? (long)(stop - start).TotalSeconds : 0;
    }

    private static DateTimeOffset StartOfDay(DateOnly date, TimeZoneInfo zone)
    {
        var local = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        // Midnight can fall inside a DST gap in some zones; nudge forward until it is valid.
        while (zone.IsInvalidTime(local))
        {
            local = local.AddMinutes(30);
        }

        return new DateTimeOffset(local, zone.GetUtcOffset(local));
    }
}
