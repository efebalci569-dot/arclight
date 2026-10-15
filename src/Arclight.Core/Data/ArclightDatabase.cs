using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arclight.Core.Data;

public sealed class DatabaseException(string message, Exception? inner = null) : Exception(message, inner);

/// <summary>
/// Owns the SQLite file: creates it, applies migrations (after taking a backup) and hands out
/// connections. WAL journaling keeps reads non-blocking while the tracker writes heartbeats.
/// </summary>
public sealed class ArclightDatabase
{
    private readonly ILogger _logger;
    private readonly string _connectionString;

    public ArclightDatabase(string filePath, ILogger<ArclightDatabase>? logger = null)
    {
        FilePath = Path.GetFullPath(filePath);
        _logger = logger ?? NullLogger<ArclightDatabase>.Instance;
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = FilePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            ForeignKeys = true,
            Pooling = true,
            DefaultTimeout = 15,
        }.ToString();
    }

    public string FilePath { get; }

    public SqliteConnection Open()
    {
        var connection = new SqliteConnection(_connectionString);
        connection.Open();
        connection.Execute("PRAGMA synchronous = NORMAL;");
        return connection;
    }

    /// <summary>Creates or upgrades the database. Throws <see cref="DatabaseException"/> with a readable message on failure.</summary>
    public void Initialize()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            using var connection = Open();
            connection.Execute("PRAGMA journal_mode = WAL;");

            var integrity = connection.Scalar("PRAGMA quick_check;", null) as string;
            if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new DatabaseException($"The library database failed its integrity check ({integrity}).");
            }

            var version = Convert.ToInt32(connection.Scalar("PRAGMA user_version;", null));
            if (version > Schema.CurrentVersion)
            {
                throw new DatabaseException(
                    $"This library was created by a newer version of Arclight (schema {version}). Update Arclight to open it.");
            }

            if (version > 0 && version < Schema.CurrentVersion)
            {
                Backup(connection, $"before-v{Schema.CurrentVersion}");
            }

            for (var next = version + 1; next <= Schema.CurrentVersion; next++)
            {
                using var transaction = connection.BeginTransaction();
                connection.Execute(Schema.Migrations[next - 1], transaction);
                connection.Execute($"PRAGMA user_version = {next};", transaction);
                transaction.Commit();
                _logger.LogInformation("Database migrated to schema version {Version}", next);
            }
        }
        catch (SqliteException ex)
        {
            _logger.LogError(ex, "Database initialization failed");
            throw new DatabaseException($"Arclight could not open its library database: {ex.Message}", ex);
        }
    }

    /// <summary>Writes a consistent copy of the database next to it using SQLite's online backup.</summary>
    public string Backup(string label)
    {
        using var connection = Open();
        return Backup(connection, label);
    }

    private string Backup(SqliteConnection source, string label)
    {
        var directory = Path.Combine(Path.GetDirectoryName(FilePath)!, "Backups");
        Directory.CreateDirectory(directory);
        var target = Path.Combine(directory, $"arclight-{DateTime.Now:yyyyMMdd-HHmmss}-{label}.db");
        using (var destination = new SqliteConnection($"Data Source={target};Pooling=False"))
        {
            destination.Open();
            source.BackupDatabase(destination);
        }

        _logger.LogInformation("Database backed up to {Path}", target);
        return target;
    }
}
