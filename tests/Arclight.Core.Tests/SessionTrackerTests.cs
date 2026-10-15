using Arclight.Core.Models;
using Arclight.Core.Tracking;

namespace Arclight.Core.Tests;

public class SessionTrackerTests
{
    private static readonly DateTimeOffset T0 = Games.BaseTime;
    private static readonly HashSet<long> None = [];
    private static HashSet<long> Running(params long[] ids) => [.. ids];

    [Fact]
    public void Launched_game_starts_a_session_when_its_process_appears()
    {
        var tracker = new SessionTracker();
        tracker.ExpectLaunch(1, T0);

        Assert.Empty(tracker.Update(T0.AddSeconds(5), None));
        var events = tracker.Update(T0.AddSeconds(10), Running(1));

        var started = Assert.IsType<SessionStarted>(Assert.Single(events));
        Assert.Equal(SessionSource.ArclightLaunch, started.Source);
        Assert.Equal(T0.AddSeconds(10), started.StartedAt);
    }

    [Fact]
    public void Session_ends_at_last_seen_time_after_grace_period()
    {
        var tracker = new SessionTracker();
        tracker.ExpectLaunch(1, T0);
        tracker.Update(T0, Running(1));
        for (var s = 5; s <= 600; s += 5)
        {
            tracker.Update(T0.AddSeconds(s), Running(1));
        }

        // Closed at 600s; the grace period keeps the session open briefly.
        Assert.Empty(tracker.Update(T0.AddSeconds(605), None).OfType<SessionEnded>());
        var ended = Assert.Single(tracker.Update(T0.AddSeconds(625), None).OfType<SessionEnded>());

        Assert.Equal(T0.AddSeconds(600), ended.EndedAt);
        Assert.Equal(600, ended.DurationSeconds);
        Assert.False(tracker.IsRunning(1));
    }

    [Fact]
    public void Launcher_to_game_handoff_within_grace_keeps_one_session()
    {
        var tracker = new SessionTracker();
        tracker.ExpectLaunch(1, T0);
        tracker.Update(T0, Running(1));
        tracker.Update(T0.AddSeconds(60), Running(1));
        tracker.Update(T0.AddSeconds(65), None); // launcher exits, game not yet visible
        var events = tracker.Update(T0.AddSeconds(75), Running(1));

        Assert.DoesNotContain(events, e => e is SessionEnded or SessionStarted);
        Assert.True(tracker.IsRunning(1));
    }

    [Fact]
    public void Very_short_sessions_are_discarded()
    {
        var tracker = new SessionTracker();
        tracker.ExpectLaunch(1, T0);
        tracker.Update(T0, Running(1));
        tracker.Update(T0.AddSeconds(10), Running(1));
        var events = tracker.Update(T0.AddSeconds(40), None);

        Assert.IsType<SessionDiscarded>(Assert.Single(events));
    }

    [Fact]
    public void Launch_that_never_shows_up_is_reported_and_dropped()
    {
        var tracker = new SessionTracker(new TrackerOptions { LaunchDetectionWindow = TimeSpan.FromMinutes(2) });
        tracker.ExpectLaunch(1, T0);

        Assert.Empty(tracker.Update(T0.AddMinutes(1), None));
        Assert.IsType<LaunchNotDetected>(Assert.Single(tracker.Update(T0.AddMinutes(3), None)));
        Assert.False(tracker.HasWork);
    }

    [Fact]
    public void Games_not_launched_or_watched_are_ignored()
    {
        var tracker = new SessionTracker();
        Assert.Empty(tracker.Update(T0, Running(42)));
    }

    [Fact]
    public void Background_watcher_starts_sessions_for_watched_games()
    {
        var tracker = new SessionTracker();
        tracker.SetWatchedGames([5]);

        var started = Assert.IsType<SessionStarted>(Assert.Single(tracker.Update(T0, Running(5, 6))));
        Assert.Equal(SessionSource.BackgroundWatcher, started.Source);
        Assert.Equal(5, started.GameId);
    }

    [Fact]
    public void Sleep_gap_is_not_counted_as_playtime()
    {
        var tracker = new SessionTracker(new TrackerOptions { MaxUpdateGap = TimeSpan.FromSeconds(90) });
        tracker.ExpectLaunch(1, T0);
        tracker.Update(T0, Running(1));
        tracker.Update(T0.AddMinutes(30), Running(1)); // 30 minutes with no polls = the PC slept

        // That update closes the first session at its last poll and restarts it after waking.
        var events = tracker.Update(T0.AddMinutes(30).AddSeconds(5), Running(1));
        Assert.Empty(events.OfType<SessionEnded>());

        var tracker2 = new SessionTracker(new TrackerOptions { MaxUpdateGap = TimeSpan.FromSeconds(90) });
        tracker2.ExpectLaunch(1, T0);
        tracker2.Update(T0, Running(1));
        for (var s = 5; s <= 120; s += 5)
        {
            tracker2.Update(T0.AddSeconds(s), Running(1));
        }

        var afterSleep = tracker2.Update(T0.AddHours(3), Running(1));
        var ended = Assert.Single(afterSleep.OfType<SessionEnded>());
        Assert.Equal(120, ended.DurationSeconds);
        var resumed = Assert.Single(afterSleep.OfType<SessionStarted>());
        Assert.Equal(T0.AddHours(3), resumed.StartedAt);
        Assert.Equal(SessionSource.ArclightLaunch, resumed.Source);
    }

    [Fact]
    public void Heartbeats_are_throttled()
    {
        var tracker = new SessionTracker(new TrackerOptions { HeartbeatInterval = TimeSpan.FromSeconds(30) });
        tracker.ExpectLaunch(1, T0);
        tracker.Update(T0, Running(1));

        var heartbeats = 0;
        for (var s = 5; s <= 120; s += 5)
        {
            heartbeats += tracker.Update(T0.AddSeconds(s), Running(1)).OfType<SessionHeartbeat>().Count();
        }

        Assert.Equal(4, heartbeats);
    }

    [Fact]
    public void StopAll_closes_running_sessions_at_last_seen()
    {
        var tracker = new SessionTracker();
        tracker.ExpectLaunch(1, T0);
        for (var s = 0; s <= 600; s += 5)
        {
            tracker.Update(T0.AddSeconds(s), Running(1));
        }

        var ended = Assert.IsType<SessionEnded>(Assert.Single(tracker.StopAll()));
        Assert.Equal(T0.AddMinutes(10), ended.EndedAt);
    }
}
