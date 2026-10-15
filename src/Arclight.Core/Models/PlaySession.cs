namespace Arclight.Core.Models;

public enum SessionSource
{
    /// <summary>The game was started from Arclight.</summary>
    ArclightLaunch = 0,

    /// <summary>The optional background watcher noticed the game running.</summary>
    BackgroundWatcher = 1,
}

public enum SessionStatus
{
    /// <summary>The game is running; the row is refreshed by heartbeats.</summary>
    Active = 0,

    /// <summary>The game was seen closing and the session was finalized normally.</summary>
    Completed = 1,

    /// <summary>Arclight exited unexpectedly; the session was closed at its last heartbeat.</summary>
    Recovered = 2,
}

public sealed class PlaySession
{
    public long Id { get; set; }

    public long GameId { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? EndedAt { get; set; }

    public DateTimeOffset LastHeartbeatAt { get; set; }

    public long DurationSeconds { get; set; }

    public SessionSource Source { get; set; }

    public SessionStatus Status { get; set; }
}
