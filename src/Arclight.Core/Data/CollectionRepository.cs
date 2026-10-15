using Arclight.Core.Models;
using Microsoft.Data.Sqlite;

namespace Arclight.Core.Data;

/// <summary>Collections and their memberships. Removing a collection only removes the grouping.</summary>
public sealed class CollectionRepository(ArclightDatabase database)
{
    public List<GameCollection> GetAll()
    {
        using var connection = database.Open();
        var collections = connection.Query(
            "SELECT id, name, description, cover_image_path, sort_order, created_at FROM collections ORDER BY sort_order, id",
            Map);
        var members = connection.Query(
            "SELECT collection_id, game_id FROM collection_games ORDER BY added_at",
            r => (CollectionId: r.GetInt64(0), GameId: r.GetInt64(1)));
        var byCollection = members.GroupBy(m => m.CollectionId).ToDictionary(g => g.Key, g => g.Select(m => m.GameId).ToList());
        foreach (var collection in collections)
        {
            collection.GameIds = byCollection.GetValueOrDefault(collection.Id) ?? [];
        }

        return collections;
    }

    public long Insert(GameCollection collection)
    {
        using var connection = database.Open();
        var nextOrder = Convert.ToInt32(connection.Scalar("SELECT COALESCE(MAX(sort_order) + 1, 0) FROM collections", null));
        collection.SortOrder = nextOrder;
        collection.Id = (long)connection.Scalar(
            "INSERT INTO collections (name, description, cover_image_path, sort_order, created_at) VALUES ($n, $d, $c, $o, $at) RETURNING id",
            null,
            ("$n", collection.Name.Trim()), ("$d", collection.Description), ("$c", collection.CoverImagePath),
            ("$o", nextOrder), ("$at", collection.CreatedAt.ToDb()))!;
        return collection.Id;
    }

    public void Update(GameCollection collection)
    {
        using var connection = database.Open();
        connection.Execute(
            "UPDATE collections SET name = $n, description = $d, cover_image_path = $c WHERE id = $id",
            ("$n", collection.Name.Trim()), ("$d", collection.Description), ("$c", collection.CoverImagePath), ("$id", collection.Id));
    }

    public void Delete(long id)
    {
        using var connection = database.Open();
        connection.Execute("DELETE FROM collections WHERE id = $id", ("$id", id));
    }

    public void SetOrder(IReadOnlyList<long> orderedIds)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        for (var i = 0; i < orderedIds.Count; i++)
        {
            connection.Execute("UPDATE collections SET sort_order = $o WHERE id = $id", transaction, ("$o", i), ("$id", orderedIds[i]));
        }

        transaction.Commit();
    }

    public void AddGames(long collectionId, IEnumerable<long> gameIds, DateTimeOffset at)
    {
        using var connection = database.Open();
        using var transaction = connection.BeginTransaction();
        foreach (var gameId in gameIds)
        {
            connection.Execute(
                "INSERT OR IGNORE INTO collection_games (collection_id, game_id, added_at) VALUES ($c, $g, $at)",
                transaction, ("$c", collectionId), ("$g", gameId), ("$at", at.ToDb()));
        }

        transaction.Commit();
    }

    public void RemoveGame(long collectionId, long gameId)
    {
        using var connection = database.Open();
        connection.Execute("DELETE FROM collection_games WHERE collection_id = $c AND game_id = $g", ("$c", collectionId), ("$g", gameId));
    }

    private static GameCollection Map(SqliteDataReader r) => new()
    {
        Id = r.GetInt64("id"),
        Name = r.GetString(r.GetOrdinal("name")),
        Description = r.GetStringOrNull("description"),
        CoverImagePath = r.GetStringOrNull("cover_image_path"),
        SortOrder = (int)r.GetInt64("sort_order"),
        CreatedAt = r.GetDate("created_at"),
    };
}
