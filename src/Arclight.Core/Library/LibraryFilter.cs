using Arclight.Core.Models;

namespace Arclight.Core.Library;

/// <summary>Applies a <see cref="LibraryQuery"/> to a set of games. Pure and allocation-light so it can run on every keystroke.</summary>
public static class LibraryFilter
{
    public static IReadOnlyList<Game> Apply(IEnumerable<Game> games, LibraryQuery query, LibraryContext context)
    {
        var terms = TextSearch.Tokenize(query.SearchText);
        var recentCutoff = context.Now - LibraryQuery.RecentlyPlayedWindow;
        IReadOnlySet<long>? collectionMembers = null;
        if (query.CollectionId is { } collectionId)
        {
            collectionMembers = context.GameIdsByCollection.TryGetValue(collectionId, out var members)
                ? members
                : new HashSet<long>();
        }

        var filtered = games.Where(game =>
        {
            if (collectionMembers is not null && !collectionMembers.Contains(game.Id)) return false;
            if (query.Install == InstallFilter.Installed && !game.IsInstalled) return false;
            if (query.Install == InstallFilter.NotInstalled && game.IsInstalled) return false;
            if (query.FavoritesOnly && !game.IsFavorite) return false;
            if (query.RecentlyPlayedOnly && (game.LastPlayed is null || game.LastPlayed < recentCutoff)) return false;
            if (query.NeverPlayedOnly && game.HasBeenPlayed) return false;
            if (query.Platforms.Count > 0 && !query.Platforms.Contains(game.Platform)) return false;
            if (query.Genres.Count > 0 && !MatchesGenre(game, query.Genres)) return false;
            if (query.Tags.Count > 0 && !query.Tags.All(tag => game.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase))) return false;
            return terms.Count == 0 || MatchesSearch(game, terms, context);
        });

        return Sort(filtered, query.Sort);
    }

    public static bool MatchesSearch(Game game, IReadOnlyList<string> terms, LibraryContext context)
    {
        // Build the searchable text once per game; every term must appear somewhere in it.
        var haystack = new List<string>(8)
        {
            TextSearch.Normalize(game.Name),
            TextSearch.Normalize(PlatformInfo.DisplayName(game.Platform)),
            TextSearch.Normalize(game.Genre),
            TextSearch.Normalize(game.Notes),
            TextSearch.Normalize(game.Developer),
            TextSearch.Normalize(game.Publisher),
        };
        haystack.AddRange(game.Tags.Select(TextSearch.Normalize));
        if (context.CollectionNamesByGame.TryGetValue(game.Id, out var collectionNames))
        {
            haystack.AddRange(collectionNames.Select(TextSearch.Normalize));
        }

        return terms.All(term => haystack.Any(field => field.Contains(term, StringComparison.Ordinal)));
    }

    public static IReadOnlyList<Game> Sort(IEnumerable<Game> games, LibrarySort sort)
    {
        var byName = NaturalStringComparer.Instance;
        IOrderedEnumerable<Game> ordered = sort switch
        {
            LibrarySort.NameDescending => games.OrderByDescending(g => g.Name, byName),
            LibrarySort.RecentlyAdded => games.OrderByDescending(g => g.DateAdded).ThenBy(g => g.Name, byName),
            LibrarySort.RecentlyPlayed => games
                .OrderBy(g => g.LastPlayed is null)
                .ThenByDescending(g => g.LastPlayed)
                .ThenBy(g => g.Name, byName),
            LibrarySort.MostPlayed => games.OrderByDescending(g => g.TotalPlaytimeSeconds).ThenBy(g => g.Name, byName),
            LibrarySort.LeastPlayed => games.OrderBy(g => g.TotalPlaytimeSeconds).ThenBy(g => g.Name, byName),
            LibrarySort.ReleaseYear => games
                .OrderBy(g => g.ReleaseYear is null)
                .ThenByDescending(g => g.ReleaseYear)
                .ThenBy(g => g.Name, byName),
            LibrarySort.InstallationStatus => games.OrderByDescending(g => g.IsInstalled).ThenBy(g => g.Name, byName),
            _ => games.OrderBy(g => g.Name, byName),
        };

        return ordered.ToList();
    }

    /// <summary>Genres are free text; a game may list several separated by commas.</summary>
    public static IEnumerable<string> SplitGenres(string? genre) =>
        string.IsNullOrWhiteSpace(genre)
            ? []
            : genre.Split([',', ';', '/'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool MatchesGenre(Game game, IReadOnlySet<string> genres) =>
        SplitGenres(game.Genre).Any(genres.Contains);
}
