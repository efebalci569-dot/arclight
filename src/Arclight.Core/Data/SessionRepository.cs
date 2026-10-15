using Arclight.Core.Models;
using Microsoft.Data.Sqlite;

namespace Arclight.Core.Data;

/// <summary>
/// Play session storage. A session is inserted as <c>active</c> when a game is first seen, refreshed by
/// heartbeats, and finalized exactly once: the status check in <see cref="Complete"/> makes double
/// counting impossible even if Arclight restarts mid-session.
/// </summary>
public sealed class SessionRepository(ArclightDatabase database)
{
    public void Start(long gameId, DateTimeOffset startedAt, SessionSource source)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        connection.Execute("""
            INSERT OR IGNORE INTO play_sessions (game_id, started_at, last_heartbeat_at, duration_seconds, source, status)
            VALUES ($game, $start, $start, 0, $source, 'active')
            """, transaction, ("$game", gameId), ("$start", startedAt.ToDb()), ("$source", SourceCode(source)));
        connection.Execute(
            "UPDATE games SET last_played = $at WHERE id = $game AND (last_played IS NULL OR last_played < $at)",
            transaction, ("$at", startedAt.ToDb()), ("$game", gameId));
        transaction.Commit();
    }

    public void Heartbeat(long gameId, DateTimeOffset startedAt, DateTimeOffset lastSeen)
    {
        using var connection = database.Open();
        connection.Execute("""
            UPDATE play_sessions SET last_heartbeat_at = $seen, duration_seconds = $duration
            WHERE game_id = $game AND started_at = $start AND status = 'active'
            """,
            ("$seen", lastSeen.ToDb()), ("$duration", Seconds(startedAt, lastSeen)), ("$game", gameId), ("$start", startedAt.ToDb()));
    }

    /// <summary>Finalizes an active session and adds its time to the game. Returns false if it was already finalized.</summary>
    public bool Complete(long gameId, DateTimeOffset startedAt, DateTimeOffset endedAt, SessionStatus status = SessionStatus.Completed)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        var completed = Complete(connection, transaction, gameId, startedAt, endedAt, status);
        transaction.Commit();
        return completed;
    }

    /// <summary>Drops an active session that ended too quickly to count.</summary>
    public void Discard(long gameId, DateTimeOffset startedAt)
    {
        using var connection = database.Open();
        connection.Execute("DELETE FROM play_sessions WHERE game_id = $game AND started_at = $start AND status = 'active'",
            ("$game", gameId), ("$start", startedAt.ToDb()));
    }

    /// <summary>
    /// Closes sessions left active by a crash or power loss at their last heartbeat, so time while
    /// Arclight was not running is never counted. Returns the number of sessions kept.
    /// </summary>
    public int RecoverInterrupted(TimeSpan minimumSession)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        var active = connection.Query(
            "SELECT game_id, started_at, last_heartbeat_at FROM play_sessions WHERE status = 'active'",
            transaction,
            r => (GameId: r.GetInt64(0), Start: SqliteHelpers.ParseDate(r.GetString(1)), Heartbeat: SqliteHelpers.ParseDate(r.GetString(2))));

        var kept = 0;
        foreach (var session in active)
        {
            if (session.Heartbeat - session.Start >= minimumSession)
            {
                if (Complete(connection, transaction, session.GameId, session.Start, session.Heartbeat, SessionStatus.Recovered))
                {
                    kept++;
                }
            }
            else
            {
                connection.Execute("DELETE FROM play_sessions WHERE game_id = $game AND started_at = $start AND status = 'active'",
                    transaction, ("$game", session.GameId), ("$start", session.Start.ToDb()));
            }
        }

        transaction.Commit();
        return kept;
    }

    /// <summary>Sessions overlapping the given range (or all when no range is given), newest first.</summary>
    public List<PlaySession> GetSessions(DateTimeOffset? from = null, DateTimeOffset? to = null, long? gameId = null, int? limit = null)
    {
        using var connection = database.Open();
        var sql = "SELECT * FROM play_sessions WHERE status <> 'active'";
        if (from is not null) sql += " AND COALESCE(ended_at, last_heartbeat_at) >= $from";
        if (to is not null) sql += " AND started_at < $to";
        if (gameId is not null) sql += " AND game_id = $game";
        sql += " ORDER BY started_at DESC";
        if (limit is not null) sql += $" LIMIT {limit.Value}";
        return connection.Query(sql, Map, ("$from", from.ToDb()), ("$to", to.ToDb()), ("$game", gameId));
    }

    /// <summary>Deletes the session log but keeps each game's accumulated totals.</summary>
    public void ClearHistory()
    {
        using var connection = database.Open();
        connection.Execute("DELETE FROM play_sessions WHERE status <> 'active'");
    }

    /// <summary>Deletes all sessions and resets every game's playtime, session count and last played date.</summary>
    public void ResetStatistics()
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        connection.Execute("DELETE FROM play_sessions WHERE status <> 'active'", transaction);
        connection.Execute("UPDATE games SET total_playtime_seconds = 0, session_count = 0, last_played = NULL", transaction);
        transaction.Commit();
    }

    private static bool Complete(SqliteConnection connection, SqliteTransaction transaction, long gameId, DateTimeOffset startedAt, DateTimeOffset endedAt, SessionStatus status)
    {
        var duration = Seconds(startedAt, endedAt);
        var changed = connection.Execute("""
            UPDATE play_sessions
            SET ended_at = $end, last_heartbeat_at = $end, duration_seconds = $duration, status = $status
            WHERE game_id = $game AND started_at = $start AND status = 'active'
            """, transaction,
            ("$end", endedAt.ToDb()), ("$duration", duration), ("$status", StatusCode(status)),
            ("$game", gameId), ("$start", startedAt.ToDb()));

        if (changed == 0)
        {
            return false;
        }

        connection.Execute("""
            UPDATE games SET
                total_playtime_seconds = total_playtime_seconds + $duration,
                session_count = session_count + 1,
                last_played = CASE WHEN last_played IS NULL OR last_played < $end THEN $end ELSE last_played END
            WHERE id = $game
            """, transaction, ("$duration", duration), ("$end", endedAt.ToDb()), ("$game", gameId));
        return true;
    }

    private static long Seconds(DateTimeOffset from, DateTimeOffset to) => (long)Math.Max(0, (to - from).TotalSeconds);

    private static string SourceCode(SessionSource source) => source == SessionSource.BackgroundWatcher ? "watcher" : "arclight";

    private static string StatusCode(SessionStatus status) => status switch
    {
        SessionStatus.Active => "active",
        SessionStatus.Recovered => "recovered",
        _ => "completed",
    };

    private static PlaySession Map(SqliteDataReader r) => new()
    {
        Id = r.GetInt64("id"),
        GameId = r.GetInt64("game_id"),
        StartedAt = r.GetDate("started_at"),
        EndedAt = r.GetDateOrNull("ended_at"),
        LastHeartbeatAt = r.GetDate("last_heartbeat_at"),
        DurationSeconds = r.GetInt64("duration_seconds"),
        Source = r.GetStringOrNull("source") == "watcher" ? SessionSource.BackgroundWatcher : SessionSource.ArclightLaunch,
        Status = r.GetStringOrNull("status") switch
        {
            "active" => SessionStatus.Active,
            "recovered" => SessionStatus.Recovered,
            _ => SessionStatus.Completed,
        },
    };
}
