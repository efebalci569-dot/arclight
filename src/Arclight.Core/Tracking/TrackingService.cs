using Arclight.Core.Data;
using Arclight.Core.Interfaces;
using Arclight.Core.Models;
using Arclight.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arclight.Core.Tracking;

public sealed class TrackingEventArgs(TrackerEvent trackerEvent, string gameName) : EventArgs
{
    public TrackerEvent Event { get; } = trackerEvent;

    public string GameName { get; } = gameName;
}

/// <summary>
/// Polls running processes while there is something to track, feeds the <see cref="SessionTracker"/>
/// and persists its events. It reads only process names and image paths; it never records input,
/// window titles or anything about unrelated programs.
/// </summary>
public sealed class TrackingService : IAsyncDisposable
{
    private readonly SessionRepository _sessions;
    private readonly LibraryService _library;
    private readonly IProcessSnapshotProvider _processes;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly SessionTracker _tracker;
    private readonly object _gate = new();
    private readonly TimeSpan _pollInterval;
    private GameProcessMatcher _matcher = new();
    private CancellationTokenSource? _loopCancellation;
    private Task? _loop;
    private bool _enabled = true;
    private bool _watcherEnabled;
    private bool _disposed;

    public TrackingService(
        SessionRepository sessions,
        LibraryService library,
        IProcessSnapshotProvider processes,
        IClock clock,
        TrackerOptions? options = null,
        TimeSpan? pollInterval = null,
        ILogger<TrackingService>? logger = null)
    {
        _sessions = sessions;
        _library = library;
        _processes = processes;
        _clock = clock;
        _tracker = new SessionTracker(options);
        _pollInterval = pollInterval ?? TimeSpan.FromSeconds(5);
        _logger = logger ?? NullLogger<TrackingService>.Instance;
    }

    /// <summary>Raised on a background thread for every tracker event.</summary>
    public event EventHandler<TrackingEventArgs>? TrackerEventRaised;

    public bool IsEnabled => _enabled;

    public bool IsBackgroundWatcherEnabled => _watcherEnabled;

    public IReadOnlyCollection<long> RunningGames
    {
        get
        {
            lock (_gate)
            {
                return _tracker.RunningGames.ToList();
            }
        }
    }

    public DateTimeOffset? RunningSince(long gameId)
    {
        lock (_gate)
        {
            return _tracker.RunningSince(gameId);
        }
    }

    /// <summary>Finalizes sessions interrupted by a crash, then starts the polling loop.</summary>
    public async Task StartAsync()
    {
        var recovered = await Task.Run(() => _sessions.RecoverInterrupted(_tracker.Options.MinimumSession));
        if (recovered > 0)
        {
            _logger.LogInformation("Recovered {Count} interrupted play sessions", recovered);
            await _library.RefreshPlaytimeAsync(_library.Games.Select(g => g.Id).ToList());
        }

        _loopCancellation = new CancellationTokenSource();
        _loop = Task.Run(() => RunLoopAsync(_loopCancellation.Token));
    }

    public void ApplySettings(AppSettings settings)
    {
        lock (_gate)
        {
            _enabled = settings.PlaytimeTrackingEnabled;
            _watcherEnabled = settings.PlaytimeTrackingEnabled && settings.BackgroundWatcherEnabled;
            _matcher = new GameProcessMatcher(settings.ExcludedProcesses);
        }

        RefreshWatchedGames();
    }

    /// <summary>Recomputes which games the background watcher follows (call after library changes).</summary>
    public void RefreshWatchedGames()
    {
        var watched = _watcherEnabled
            ? _library.Games.Where(g => g.IsTrackingEnabled && g.IsInstalled && IsTrackable(g)).Select(g => g.Id).ToList()
            : [];
        lock (_gate)
        {
            _tracker.SetWatchedGames(watched);
        }
    }

    /// <summary>Tells the tracker a game was just launched from Arclight.</summary>
    public bool NotifyLaunched(Game game)
    {
        if (!_enabled || !IsTrackable(game))
        {
            return false;
        }

        lock (_gate)
        {
            _tracker.ExpectLaunch(game.Id, _clock.UtcNow);
        }

        return true;
    }

    /// <summary>True when Arclight can recognise the game's process (it knows its exe or install folder).</summary>
    public static bool IsTrackable(Game game) =>
        !string.IsNullOrWhiteSpace(game.ExecutablePath) || Library.PathKeys.IsSpecificGameDirectory(game.InstallDirectory);

    /// <summary>Runs one poll immediately. Exposed for tests and for a responsive "now playing" state.</summary>
    public async Task PollAsync()
    {
        List<TrackerEvent> events;
        Dictionary<long, TrackTarget> targets;
        lock (_gate)
        {
            if (!_tracker.HasWork)
            {
                return;
            }

            targets = _tracker.GamesOfInterest
                .Select(_library.FindGame)
                .Where(g => g is not null)
                .Select(g => new TrackTarget(g!.Id, g.ExecutablePath, g.InstallDirectory))
                .ToDictionary(t => t.GameId);
        }

        IReadOnlyList<RunningProcess> snapshot;
        try
        {
            snapshot = _processes.GetRunningProcesses();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the process list");
            return;
        }

        lock (_gate)
        {
            var running = _matcher.FindRunning(snapshot, targets.Values);
            events = [.. _tracker.Update(_clock.UtcNow, running)];
        }

        await PersistAsync(events);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        if (_loopCancellation is not null)
        {
            await _loopCancellation.CancelAsync();
            if (_loop is not null)
            {
                // Don't wait forever on a poll that is mid-flight; the final state is persisted below.
                await Task.WhenAny(_loop, Task.Delay(TimeSpan.FromSeconds(2)));
            }
        }

        List<TrackerEvent> events;
        lock (_gate)
        {
            events = [.. _tracker.StopAll()];
        }

        await PersistAsync(events, refreshLibrary: false);
    }

    private async Task RunLoopAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(_pollInterval);
        while (await timer.WaitForNextTickAsync(cancellationToken))
        {
            try
            {
                await PollAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Playtime tracking poll failed");
            }
        }
    }

    private async Task PersistAsync(IReadOnlyList<TrackerEvent> events, bool refreshLibrary = true)
    {
        if (events.Count == 0)
        {
            return;
        }

        var changed = new HashSet<long>();
        foreach (var trackerEvent in events)
        {
            try
            {
                switch (trackerEvent)
                {
                    case SessionStarted started:
                        _sessions.Start(started.GameId, started.StartedAt, started.Source);
                        changed.Add(started.GameId);
                        break;
                    case SessionHeartbeat heartbeat:
                        _sessions.Heartbeat(heartbeat.GameId, heartbeat.StartedAt, heartbeat.LastSeenAt);
                        break;
                    case SessionEnded ended:
                        _sessions.Complete(ended.GameId, ended.StartedAt, ended.EndedAt);
                        changed.Add(ended.GameId);
                        _logger.LogInformation("Session recorded for game {GameId}: {Seconds}s", ended.GameId, ended.DurationSeconds);
                        break;
                    case SessionDiscarded discarded:
                        _sessions.Discard(discarded.GameId, discarded.StartedAt);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not save tracking event {Event}", trackerEvent);
            }

            var name = _library.FindGame(trackerEvent.GameId)?.Name ?? "Game";
            TrackerEventRaised?.Invoke(this, new TrackingEventArgs(trackerEvent, name));
        }

        if (refreshLibrary && changed.Count > 0)
        {
            await _library.RefreshPlaytimeAsync(changed);
        }
    }
}
