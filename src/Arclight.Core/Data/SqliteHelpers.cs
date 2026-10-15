using System.Globalization;
using Microsoft.Data.Sqlite;

namespace Arclight.Core.Data;

/// <summary>Small helpers that keep the repositories readable without pulling in an ORM.</summary>
internal static class SqliteHelpers
{
    public static SqliteCommand Command(this SqliteConnection connection, string sql, SqliteTransaction? transaction, params (string Name, object? Value)[] parameters)
    {
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        foreach (var (name, value) in parameters)
        {
            command.Parameters.AddWithValue(name, value ?? DBNull.Value);
        }

        return command;
    }

    public static int Execute(this SqliteConnection connection, string sql, params (string Name, object? Value)[] parameters) =>
        connection.Execute(sql, null, parameters);

    public static int Execute(this SqliteConnection connection, string sql, SqliteTransaction? transaction, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.Command(sql, transaction, parameters);
        return command.ExecuteNonQuery();
    }

    public static object? Scalar(this SqliteConnection connection, string sql, SqliteTransaction? transaction, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.Command(sql, transaction, parameters);
        var value = command.ExecuteScalar();
        return value is DBNull ? null : value;
    }

    public static List<T> Query<T>(this SqliteConnection connection, string sql, Func<SqliteDataReader, T> map, params (string Name, object? Value)[] parameters) =>
        connection.Query(sql, null, map, parameters);

    public static List<T> Query<T>(this SqliteConnection connection, string sql, SqliteTransaction? transaction, Func<SqliteDataReader, T> map, params (string Name, object? Value)[] parameters)
    {
        using var command = connection.Command(sql, transaction, parameters);
        using var reader = command.ExecuteReader();
        var list = new List<T>();
        while (reader.Read())
        {
            list.Add(map(reader));
        }

        return list;
    }

    public static string ToDb(this DateTimeOffset value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);

    public static string? ToDb(this DateTimeOffset? value) => value?.ToDb();

    public static DateTimeOffset ParseDate(string value) =>
        DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);

    public static string? GetStringOrNull(this SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    public static DateTimeOffset? GetDateOrNull(this SqliteDataReader reader, string column) =>
        reader.GetStringOrNull(column) is { } value ? ParseDate(value) : null;

    public static DateTimeOffset GetDate(this SqliteDataReader reader, string column) => ParseDate(reader.GetString(reader.GetOrdinal(column)));

    public static long GetInt64(this SqliteDataReader reader, string column) => reader.GetInt64(reader.GetOrdinal(column));

    public static int? GetInt32OrNull(this SqliteDataReader reader, string column)
    {
        var ordinal = reader.GetOrdinal(column);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    public static bool GetBool(this SqliteDataReader reader, string column) => reader.GetInt64(reader.GetOrdinal(column)) != 0;
}
