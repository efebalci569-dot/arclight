using Arclight.Core.Formatting;
using Arclight.Core.Models;
using Arclight.Core.Statistics;

namespace Arclight.Core.Tests;

public class StatisticsCalculatorTests
{
    // Wednesday 14 October 2026, 12:00 UTC.
    private static readonly DateTimeOffset Now = Games.BaseTime;

    private static PlaySession Session(long gameId, DateTimeOffset start, TimeSpan length) => new()
    {
        GameId = gameId,
        StartedAt = start,
        EndedAt = start + length,
        LastHeartbeatAt = start + length,
        DurationSeconds = (long)length.TotalSeconds,
        Status = SessionStatus.Completed,
    };

    [Fact]
    public void Empty_library_has_no_tracked_time()
    {
        var stats = StatisticsCalculator.Calculate([], [], Now, TimeZoneInfo.Utc);
        Assert.False(stats.HasTrackedTime);
        Assert.Null(stats.MostPlayed);
        Assert.Equal(30, stats.Last30Days.Count);
        Assert.All(stats.Last30Days, d => Assert.Equal(0, d.Seconds));
        Assert.Equal("0 hours tracked", Format.Playtime(stats.TotalPlaytimeSeconds));
    }

    [Fact]
    public void Week_and_month_windows_clip_sessions()
    {
        var games = new List<Game>
        {
            Games.Make(1, "Hades", playtime: 10 * 3600),
            Games.Make(2, "Celeste", playtime: 3600, installed: false),
        };
        games[0].SessionCount = 3;
        games[1].SessionCount = 1;

        var sessions = new List<PlaySession>
        {
            // Sunday 11 Oct 23:00 → Monday 12 Oct 01:00: only the hour after midnight is "this week".
            Session(1, new DateTimeOffset(2026, 10, 11, 23, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(2)),
            // Monday 12 Oct.
            Session(2, new DateTimeOffset(2026, 10, 12, 18, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(1)),
            // 30 Sep 23:30 → 1 Oct 00:30: 30 minutes count for October.
            Session(1, new DateTimeOffset(2026, 9, 30, 23, 30, 0, TimeSpan.Zero), TimeSpan.FromHours(1)),
        };

        var stats = StatisticsCalculator.Calculate(games, sessions, Now, TimeZoneInfo.Utc);

        Assert.Equal(2 * 3600, stats.ThisWeekSeconds);
        Assert.Equal(2 * 3600 + 3600 + 1800, stats.ThisMonthSeconds);
        Assert.Equal(2, stats.GamesPlayedThisWeek);
        Assert.Equal(11 * 3600, stats.TotalPlaytimeSeconds);
        Assert.Equal(4, stats.TotalSessions);
        Assert.Equal(1, stats.InstalledGames);
        Assert.Equal("Hades", stats.MostPlayed!.Name);
        Assert.Equal(2 * 3600, stats.LongestSession!.Seconds);
        Assert.Equal(11 * 3600 / 2, stats.AverageSecondsPerPlayedGame);
    }

    [Fact]
    public void Daily_totals_split_sessions_across_midnight()
    {
        var games = new List<Game> { Games.Make(1, "Hades", playtime: 7200) };
        var sessions = new List<PlaySession> { Session(1, new DateTimeOffset(2026, 10, 13, 23, 0, 0, TimeSpan.Zero), TimeSpan.FromHours(2)) };

        var stats = StatisticsCalculator.Calculate(games, sessions, Now, TimeZoneInfo.Utc);

        Assert.Equal(new DateOnly(2026, 10, 14), stats.Last30Days[^1].Date);
        Assert.Equal(3600, stats.Last30Days[^1].Seconds);
        Assert.Equal(3600, stats.Last30Days[^2].Seconds);
    }

    [Fact]
    public void Platform_distribution_counts_games()
    {
        var games = new List<Game>
        {
            Games.Make(1, "A", GamePlatform.Steam),
            Games.Make(2, "B", GamePlatform.Steam),
            Games.Make(3, "C", GamePlatform.Epic),
        };

        var stats = StatisticsCalculator.Calculate(games, [], Now, TimeZoneInfo.Utc);

        Assert.Equal([new PlatformCount(GamePlatform.Steam, 2), new PlatformCount(GamePlatform.Epic, 1)], stats.Platforms);
    }

    [Fact]
    public void Sessions_for_removed_games_are_ignored()
    {
        var sessions = new List<PlaySession> { Session(99, Now.AddHours(-2), TimeSpan.FromHours(1)) };
        var stats = StatisticsCalculator.Calculate([], sessions, Now, TimeZoneInfo.Utc);
        Assert.Equal(0, stats.ThisWeekSeconds);
        Assert.Null(stats.LongestSession);
    }

    [Theory]
    [InlineData(0, "0 hours tracked")]
    [InlineData(30, "< 1 min")]
    [InlineData(45 * 60, "45 min")]
    [InlineData(2 * 3600 + 5 * 60, "2 h 5 min")]
    [InlineData(123 * 3600, "123 h")]
    public void Playtime_formatting(long seconds, string expected)
    {
        Assert.Equal(expected, Format.Playtime(seconds));
    }
}
