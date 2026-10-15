using Arclight.Core.Models;

namespace Arclight.Core.Library;

public enum InstallFilter { All, Installed, NotInstalled }

/// <summary>Everything that narrows and orders the library view. All filters combine with AND.</summary>
public sealed record LibraryQuery
{
    public static TimeSpan RecentlyPlayedWindow { get; } = TimeSpan.FromDays(14);

    public string? SearchText { get; init; }

    public InstallFilter Install { get; init; } = InstallFilter.All;

    public bool FavoritesOnly { get; init; }

    public bool RecentlyPlayedOnly { get; init; }

    public bool NeverPlayedOnly { get; init; }

    public IReadOnlySet<GamePlatform> Platforms { get; init; } = new HashSet<GamePlatform>();

    public IReadOnlySet<string> Genres { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlySet<string> Tags { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public long? CollectionId { get; init; }

    public LibrarySort Sort { get; init; } = LibrarySort.NameAscending;

    public bool HasActiveFilters =>
        !string.IsNullOrWhiteSpace(SearchText) || Install != InstallFilter.All || FavoritesOnly || RecentlyPlayedOnly ||
        NeverPlayedOnly || Platforms.Count > 0 || Genres.Count > 0 || Tags.Count > 0;
}

/// <summary>Context the filter needs beyond the games themselves.</summary>
public sealed record LibraryContext(
    DateTimeOffset Now,
    IReadOnlyDictionary<long, IReadOnlyList<string>> CollectionNamesByGame,
    IReadOnlyDictionary<long, IReadOnlySet<long>> GameIdsByCollection)
{
    public static LibraryContext Empty(DateTimeOffset now) =>
        new(now, new Dictionary<long, IReadOnlyList<string>>(), new Dictionary<long, IReadOnlySet<long>>());

    public static LibraryContext FromCollections(DateTimeOffset now, IEnumerable<GameCollection> collections)
    {
        var namesByGame = new Dictionary<long, List<string>>();
        var gamesByCollection = new Dictionary<long, IReadOnlySet<long>>();
        foreach (var collection in collections)
        {
            gamesByCollection[collection.Id] = collection.GameIds.ToHashSet();
            foreach (var gameId in collection.GameIds)
            {
                if (!namesByGame.TryGetValue(gameId, out var names))
                {
                    namesByGame[gameId] = names = [];
                }

                names.Add(collection.Name);
            }
        }

        return new LibraryContext(
            now,
            namesByGame.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value),
            gamesByCollection);
    }
}
