using Arclight.Core.Library;
using Arclight.Core.Models;

namespace Arclight.Core.Tests;

public class LibraryFilterTests
{
    private static readonly DateTimeOffset Now = Games.BaseTime;

    private static readonly List<Game> Library =
    [
        Games.Make(1, "Hades", GamePlatform.Steam, playtime: 36_000, lastPlayed: Now.AddDays(-1), genre: "Roguelike, Action", year: 2020, tags: ["Relaxing"], favorite: true),
        Games.Make(2, "Stardew Valley", GamePlatform.Xbox, playtime: 7_200, lastPlayed: Now.AddDays(-40), genre: "Simulation", year: 2016, tags: ["Relaxing", "Multiplayer"]),
        Games.Make(3, "Grand Theft Auto V", GamePlatform.Epic, installed: false, genre: "Action", year: 2013, notes: "Finish the heist missions"),
        Games.Make(4, "Game 10", year: null),
        Games.Make(5, "Game 2", tags: ["Backlog"]),
        Games.Make(6, "İstanbul Kebab", notes: null),
    ];

    private static LibraryContext Context() => LibraryContext.FromCollections(Now,
    [
        new GameCollection { Id = 100, Name = "Weekend Games", GameIds = [2, 3] },
    ]);

    private static List<string> Names(LibraryQuery query) =>
        LibraryFilter.Apply(Library, query, Context()).Select(g => g.Name).ToList();

    [Theory]
    [InlineData("hades", "Hades")]
    [InlineData("HADES", "Hades")]
    [InlineData("relaxing valley", "Stardew Valley")]
    [InlineData("heist", "Grand Theft Auto V")]
    [InlineData("weekend", "Grand Theft Auto V,Stardew Valley")]
    [InlineData("epic", "Grand Theft Auto V")]
    [InlineData("istanbul", "İstanbul Kebab")]
    [InlineData("ıstanbul", "İstanbul Kebab")]
    public void Search_matches_name_tags_notes_collections_and_platform(string text, string expected)
    {
        var result = Names(new LibraryQuery { SearchText = text });
        Assert.Equal(expected.Split(','), result);
    }

    [Fact]
    public void Search_with_no_match_returns_empty()
    {
        Assert.Empty(Names(new LibraryQuery { SearchText = "zelda" }));
    }

    [Fact]
    public void Filters_combine()
    {
        var result = Names(new LibraryQuery
        {
            Install = InstallFilter.Installed,
            Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "relaxing" },
            Platforms = new HashSet<GamePlatform> { GamePlatform.Steam },
        });

        Assert.Equal(["Hades"], result);
    }

    [Fact]
    public void Tag_filter_requires_every_selected_tag()
    {
        var result = Names(new LibraryQuery { Tags = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "Relaxing", "Multiplayer" } });
        Assert.Equal(["Stardew Valley"], result);
    }

    [Fact]
    public void Genre_filter_understands_comma_separated_genres()
    {
        var result = Names(new LibraryQuery { Genres = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "action" } });
        Assert.Equal(["Grand Theft Auto V", "Hades"], result);
    }

    [Fact]
    public void Not_installed_never_played_and_recently_played_filters()
    {
        Assert.Equal(["Grand Theft Auto V"], Names(new LibraryQuery { Install = InstallFilter.NotInstalled }));
        Assert.Equal(["Hades"], Names(new LibraryQuery { RecentlyPlayedOnly = true }));
        Assert.Equal(["Game 2", "Game 10", "Grand Theft Auto V", "İstanbul Kebab"], Names(new LibraryQuery { NeverPlayedOnly = true }));
        Assert.Equal(["Hades"], Names(new LibraryQuery { FavoritesOnly = true }));
    }

    [Fact]
    public void Collection_filter_limits_to_members()
    {
        Assert.Equal(["Grand Theft Auto V", "Stardew Valley"], Names(new LibraryQuery { CollectionId = 100 }));
        Assert.Empty(Names(new LibraryQuery { CollectionId = 999 }));
    }

    [Fact]
    public void Name_sort_is_natural()
    {
        var result = Names(new LibraryQuery());
        Assert.Equal(["Game 2", "Game 10", "Grand Theft Auto V", "Hades", "İstanbul Kebab", "Stardew Valley"], result);
        Assert.Equal("Stardew Valley", Names(new LibraryQuery { Sort = LibrarySort.NameDescending })[0]);
    }

    [Fact]
    public void Playtime_and_date_sorts()
    {
        Assert.Equal("Hades", Names(new LibraryQuery { Sort = LibrarySort.MostPlayed })[0]);
        Assert.Equal(0, LibraryFilter.Apply(Library, new LibraryQuery { Sort = LibrarySort.LeastPlayed }, Context())[0].TotalPlaytimeSeconds);
        Assert.Equal(["Hades", "Stardew Valley"], Names(new LibraryQuery { Sort = LibrarySort.RecentlyPlayed }).Take(2));
        Assert.Equal("Hades", Names(new LibraryQuery { Sort = LibrarySort.RecentlyAdded })[0]);
        Assert.Equal(["Hades", "Stardew Valley", "Grand Theft Auto V"], Names(new LibraryQuery { Sort = LibrarySort.ReleaseYear }).Take(3));
        Assert.Equal("Grand Theft Auto V", Names(new LibraryQuery { Sort = LibrarySort.InstallationStatus })[^1]);
    }

    [Theory]
    [InlineData("Ğame", "game")]
    [InlineData("ÇÖÜŞ", "cous")]
    [InlineData("Pokémon", "pokemon")]
    public void Normalize_strips_case_and_accents(string input, string expected)
    {
        Assert.Equal(expected, TextSearch.Normalize(input));
    }

    [Fact]
    public void Natural_comparer_orders_numbers_by_value()
    {
        var items = new[] { "Far Cry 10", "Far Cry 3", "far cry 2", "Far Cry" };
        Assert.Equal(["Far Cry", "far cry 2", "Far Cry 3", "Far Cry 10"], items.Order(NaturalStringComparer.Instance));
    }
}
