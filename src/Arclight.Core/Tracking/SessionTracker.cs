using Arclight.Core.Models;

namespace Arclight.Core.Tracking;

public sealed record TrackerOptions
{
    /// <summary>Sessions shorter than this are discarded (crashes, failed starts).</summary>
    public TimeSpan MinimumSession { get; init; } = TimeSpan.FromSeconds(30);

    /// <summary>How long a game may be missing before its session ends. Covers launcher-to-game hand-offs.</summary>
    public TimeSpan EndGrace { get; init; } = TimeSpan.FromSeconds(20);

    /// <summary>How long Arclight waits for a launched game to appear (launchers may update first).</summary>
    public TimeSpan LaunchDetectionWindow { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// A gap between two updates longer than this means the PC slept or Arclight was suspended.
    /// The session is split at the gap so sleep time is never counted.
    /// </summary>
    public TimeSpan MaxUpdateGap { get; init; } = TimeSpan.FromSeconds(90);

    /// <summary>How often a running session is persisted so a crash loses at most this much.</summary>
    public TimeSpan HeartbeatInterval { get; init; } = TimeSpan.FromSeconds(30);
}

public abstract record TrackerEvent(long GameId);

public sealed record SessionStarted(long GameId, DateTimeOffset StartedAt, SessionSource Source) : TrackerEvent(GameId);

public sealed record SessionHeartbeat(long GameId, DateTimeOffset StartedAt, DateTimeOffset LastSeenAt) : TrackerEvent(GameId);

public sealed record SessionEnded(long GameId, DateTimeOffset StartedAt, DateTimeOffset EndedAt, SessionSource Source) : TrackerEvent(GameId)
{
    public long DurationSeconds => (long)Math.Max(0, (EndedAt - StartedAt).TotalSeconds);
}

/// <summary>The session ended too quickly to count and was dropped.</summary>
public sealed record SessionDiscarded(long GameId, DateTimeOffset StartedAt) : TrackerEvent(GameId);

/// <summary>A game launched from Arclight never showed up as a running process.</summary>
public sealed record LaunchNotDetected(long GameId) : TrackerEvent(GameId);

/// <summary>
/// Pure state machine for play sessions. It is fed the set of running games at each poll and
/// emits events; persistence and process inspection live elsewhere so this stays testable.
/// </summary>
public sealed class SessionTracker
{
    private readonly TrackerOptions _options;
    private readonly Dictionary<long, DateTimeOffset> _expected = [];
    private readonly Dictionary<long, RunningState> _running = [];
    private HashSet<long> _watched = [];
    private DateTimeOffset? _lastUpdate;

    public SessionTracker(TrackerOptions? options = null) => _options = options ?? new TrackerOptions();

    public TrackerOptions Options => _options;

    /// <summary>Games the caller should check on the next poll.</summary>
    public IReadOnlySet<long> GamesOfInterest => _expected.Keys.Concat(_running.Keys).Concat(_watched).ToHashSet();

    public IReadOnlyCollection<long> RunningGames => _running.Keys;

    public bool HasWork => _expected.Count > 0 || _running.Count > 0 || _watched.Count > 0;

    public bool IsRunning(long gameId) => _running.ContainsKey(gameId);

    public DateTimeOffset? RunningSince(long gameId) => _running.TryGetValue(gameId, out var state) ? state.StartedAt : null;

    /// <summary>Called when the user starts a game from Arclight.</summary>
    public void ExpectLaunch(long gameId, DateTimeOffset at)
    {
        if (!_running.ContainsKey(gameId))
        {
            _expected[gameId] = at;
        }
    }

    /// <summary>Games the optional background watcher should pick up even when not launched from Arclight.</summary>
    public void SetWatchedGames(IEnumerable<long> gameIds) => _watched = gameIds.ToHashSet();

    public IReadOnlyList<TrackerEvent> Update(DateTimeOffset now, IReadOnlySet<long> runningGameIds)
    {
        var events = new List<TrackerEvent>();
        var resumable = new Dictionary<long, SessionSource>();

        // A long gap means sleep/hibernate: close sessions at the last time we actually saw them.
        // A game that is still running afterwards continues in a new session with the same source.
        if (_lastUpdate is { } last && now - last > _options.MaxUpdateGap)
        {
            foreach (var (gameId, state) in _running.ToList())
            {
                resumable[gameId] = state.Source;
                events.Add(End(gameId));
            }
        }

        _lastUpdate = now;

        foreach (var gameId in runningGameIds)
        {
            if (_running.TryGetValue(gameId, out var state))
            {
                state.LastSeenAt = now;
                if (now - state.LastHeartbeatAt >= _options.HeartbeatInterval)
                {
                    state.LastHeartbeatAt = now;
                    events.Add(new SessionHeartbeat(gameId, state.StartedAt, now));
                }

                continue;
            }

            SessionSource? source = null;
            if (resumable.TryGetValue(gameId, out var resumedSource))
            {
                source = resumedSource;
            }
            else if (_expected.Remove(gameId))
            {
                source = SessionSource.ArclightLaunch;
            }
            else if (_watched.Contains(gameId))
            {
                source = SessionSource.BackgroundWatcher;
            }

            if (source is { } s)
            {
                _running[gameId] = new RunningState(now, s);
                events.Add(new SessionStarted(gameId, now, s));
            }
        }

        foreach (var (gameId, state) in _running.ToList())
        {
            if (!runningGameIds.Contains(gameId) && now - state.LastSeenAt >= _options.EndGrace)
            {
                events.Add(End(gameId));
            }
        }

        foreach (var (gameId, since) in _expected.ToList())
        {
            if (now - since > _options.LaunchDetectionWindow)
            {
                _expected.Remove(gameId);
                events.Add(new LaunchNotDetected(gameId));
            }
        }

        return events;
    }

    /// <summary>Ends every running session at the time it was last seen (used when Arclight exits).</summary>
    public IReadOnlyList<TrackerEvent> StopAll()
    {
        var events = _running.Keys.ToList().Select(End).ToList();
        _expected.Clear();
        return events;
    }

    private TrackerEvent End(long gameId)
    {
        var state = _running[gameId];
        _running.Remove(gameId);
        var duration = state.LastSeenAt - state.StartedAt;
        return duration < _options.MinimumSession
            ? new SessionDiscarded(gameId, state.StartedAt)
            : new SessionEnded(gameId, state.StartedAt, state.LastSeenAt, state.Source);
    }

    private sealed class RunningState(DateTimeOffset startedAt, SessionSource source)
    {
        public DateTimeOffset StartedAt { get; } = startedAt;

        public SessionSource Source { get; } = source;

        public DateTimeOffset LastSeenAt { get; set; } = startedAt;

        public DateTimeOffset LastHeartbeatAt { get; set; } = startedAt;
    }
}
