namespace Arclight.Core.Data;

/// <summary>Database migrations. Each entry upgrades the schema by exactly one version.</summary>
internal static class Schema
{
    public static IReadOnlyList<string> Migrations { get; } =
    [
        // v1 — initial schema
        """
        CREATE TABLE games (
            id                      INTEGER PRIMARY KEY AUTOINCREMENT,
            name                    TEXT    NOT NULL,
            platform                TEXT    NOT NULL DEFAULT 'manual',
            platform_id             TEXT,
            launch_method           TEXT    NOT NULL DEFAULT 'executable',
            launch_target           TEXT,
            launch_arguments        TEXT,
            working_directory       TEXT,
            executable_path         TEXT,
            install_directory       TEXT,
            cover_image_path        TEXT,
            background_image_path   TEXT,
            icon_image_path         TEXT,
            genre                   TEXT,
            release_year            INTEGER,
            developer               TEXT,
            publisher               TEXT,
            description             TEXT,
            is_installed            INTEGER NOT NULL DEFAULT 1,
            is_favorite             INTEGER NOT NULL DEFAULT 0,
            tracking_enabled        INTEGER NOT NULL DEFAULT 1,
            date_added              TEXT    NOT NULL,
            last_played             TEXT,
            total_playtime_seconds  INTEGER NOT NULL DEFAULT 0 CHECK (total_playtime_seconds >= 0),
            session_count           INTEGER NOT NULL DEFAULT 0,
            personal_rating         INTEGER CHECK (personal_rating IS NULL OR personal_rating BETWEEN 1 AND 5),
            notes                   TEXT,
            notes_updated_at        TEXT,
            source                  TEXT    NOT NULL DEFAULT 'manual'
        );

        CREATE UNIQUE INDEX ux_games_platform_id ON games (platform, platform_id COLLATE NOCASE) WHERE platform_id IS NOT NULL;
        CREATE INDEX ix_games_last_played ON games (last_played);

        CREATE TABLE tags (
            id    INTEGER PRIMARY KEY AUTOINCREMENT,
            name  TEXT NOT NULL UNIQUE COLLATE NOCASE
        );

        CREATE TABLE game_tags (
            game_id  INTEGER NOT NULL REFERENCES games (id) ON DELETE CASCADE,
            tag_id   INTEGER NOT NULL REFERENCES tags (id) ON DELETE CASCADE,
            PRIMARY KEY (game_id, tag_id)
        );

        CREATE TABLE collections (
            id                INTEGER PRIMARY KEY AUTOINCREMENT,
            name              TEXT    NOT NULL,
            description       TEXT,
            cover_image_path  TEXT,
            sort_order        INTEGER NOT NULL DEFAULT 0,
            created_at        TEXT    NOT NULL
        );

        CREATE TABLE collection_games (
            collection_id  INTEGER NOT NULL REFERENCES collections (id) ON DELETE CASCADE,
            game_id        INTEGER NOT NULL REFERENCES games (id) ON DELETE CASCADE,
            added_at       TEXT    NOT NULL,
            PRIMARY KEY (collection_id, game_id)
        );

        CREATE TABLE play_sessions (
            id                 INTEGER PRIMARY KEY AUTOINCREMENT,
            game_id            INTEGER NOT NULL REFERENCES games (id) ON DELETE CASCADE,
            started_at         TEXT    NOT NULL,
            ended_at           TEXT,
            last_heartbeat_at  TEXT    NOT NULL,
            duration_seconds   INTEGER NOT NULL DEFAULT 0 CHECK (duration_seconds >= 0),
            source             TEXT    NOT NULL,
            status             TEXT    NOT NULL,
            UNIQUE (game_id, started_at)
        );

        CREATE INDEX ix_sessions_started ON play_sessions (started_at);
        CREATE INDEX ix_sessions_status ON play_sessions (status);

        CREATE TABLE settings (
            key    TEXT PRIMARY KEY,
            value  TEXT NOT NULL
        );

        CREATE TABLE ignored_games (
            identity_key  TEXT PRIMARY KEY,
            name          TEXT NOT NULL,
            ignored_at    TEXT NOT NULL
        );

        CREATE TABLE image_cache (
            cache_key    TEXT PRIMARY KEY,
            source       TEXT NOT NULL,
            local_path   TEXT,
            status       TEXT NOT NULL,
            updated_at   TEXT NOT NULL
        );
        """,
    ];

    public static int CurrentVersion => Migrations.Count;
}
