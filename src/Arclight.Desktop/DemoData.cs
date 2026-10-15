using Arclight.Core.Data;
using Arclight.Core.Models;
using System.Windows.Media;
using Arclight.Core.Services;

namespace Arclight.Desktop;

/// <summary>
/// Fills a separate, empty data folder with a fictional sample library so documentation screenshots
/// never show a real user's games or third-party artwork. Only reachable with
/// <c>--data-dir &lt;folder&gt; --seed-demo</c>; it refuses to touch a library that already has games.
/// </summary>
internal static class DemoData
{
    private static readonly (string Name, GamePlatform Platform, string Genre, int Year, double Hours, int DaysAgo, bool Favorite, bool Installed, string[] Tags)[] Games =
    [
        ("Nebula Drift", GamePlatform.Steam, "Racing, Arcade", 2024, 42.5, 0, true, true, ["Multiplayer"]),
        ("Ironvale Chronicles", GamePlatform.Gog, "RPG", 2023, 118.0, 1, true, true, ["To Finish"]),
        ("Pixel Harvest", GamePlatform.Xbox, "Simulation", 2022, 64.2, 2, false, true, ["Relaxing"]),
        ("Hollow Signal", GamePlatform.Epic, "Horror, Adventure", 2025, 9.4, 3, false, true, ["Difficult"]),
        ("Starfall Tactics", GamePlatform.Steam, "Strategy", 2021, 27.8, 5, false, true, ["Backlog"]),
        ("Velvet Circuit", GamePlatform.Manual, "Puzzle", 2020, 5.1, 9, false, true, ["Relaxing"]),
        ("Cinder & Ash", GamePlatform.Steam, "Roguelike, Action", 2024, 33.0, 12, true, true, ["Difficult"]),
        ("Tidebound", GamePlatform.Ubisoft, "Adventure", 2019, 0, -1, false, false, ["Backlog"]),
        ("Glass Orbit", GamePlatform.Epic, "Platformer", 2023, 14.6, 20, false, true, []),
        ("Kingsreach Online", GamePlatform.Steam, "MMO, RPG", 2018, 210.4, 26, false, true, ["Multiplayer"]),
        ("Quiet Meadow", GamePlatform.Xbox, "Simulation", 2024, 0, -1, false, true, ["Relaxing", "Backlog"]),
        ("Neon Rally '84", GamePlatform.Manual, "Racing", 2017, 3.2, 40, false, true, []),
        ("Shardfall", GamePlatform.Steam, "Shooter", 2025, 0, -1, false, false, []),
        ("Lantern Keeper", GamePlatform.Gog, "Adventure, Puzzle", 2022, 11.9, 33, false, true, ["To Finish"]),
    ];

    // Art direction per game: motif plus deep / mid / glow colors.
    private static readonly (DemoArt.Motif Motif, string Deep, string Mid, string Glow)[] Art =
    [
        (DemoArt.Motif.Sun, "#12061F", "#3B0F6B", "#F472B6"),
        (DemoArt.Motif.Mountains, "#0A1220", "#24405E", "#FBBF24"),
        (DemoArt.Motif.Hills, "#0F2A1A", "#2F7A4B", "#FDE68A"),
        (DemoArt.Motif.Shards, "#05070C", "#1B2433", "#7DD3FC"),
        (DemoArt.Motif.Rings, "#070B1E", "#1E1B4B", "#A78BFA"),
        (DemoArt.Motif.Waves, "#1A0B16", "#5B1A44", "#F9A8D4"),
        (DemoArt.Motif.Shards, "#170806", "#5A1A0C", "#FB923C"),
        (DemoArt.Motif.Waves, "#041824", "#0E4A63", "#5EEAD4"),
        (DemoArt.Motif.Rings, "#0B1020", "#13355C", "#67E8F9"),
        (DemoArt.Motif.Mountains, "#140A1E", "#3F2364", "#C4B5FD"),
        (DemoArt.Motif.Hills, "#0D1A24", "#3A6E78", "#FEF3C7"),
        (DemoArt.Motif.Sun, "#0B0418", "#2A0B4A", "#22D3EE"),
        (DemoArt.Motif.Shards, "#0A0A0F", "#262633", "#F87171"),
        (DemoArt.Motif.Waves, "#100C04", "#4A3510", "#FCD34D"),
    ];

    public static async Task SeedAsync(LibraryService library, SessionRepository sessions, AppPaths paths)
    {
        if (library.Games.Count > 0)
        {
            return;
        }

        var now = DateTimeOffset.UtcNow;
        var random = new Random(7);
        var added = new List<Game>();
        for (var i = 0; i < Games.Length; i++)
        {
            var data = Games[i];
            var art = Art[i % Art.Length];
            var deep = (Color)ColorConverter.ConvertFromString(art.Deep);
            var mid = (Color)ColorConverter.ConvertFromString(art.Mid);
            var glow = (Color)ColorConverter.ConvertFromString(art.Glow);
            var cover = Path.Combine(paths.UserImages, $"demo-{i}-cover.jpg");
            var background = Path.Combine(paths.UserImages, $"demo-{i}-background.jpg");
            DemoArt.Render(cover, 600, 800, data.Name, art.Motif, deep, mid, glow, withTitle: true);
            DemoArt.Render(background, 1920, 1080, data.Name, art.Motif, deep, mid, glow, withTitle: false);

            var game = await library.AddGameAsync(new Game
            {
                Name = data.Name,
                Platform = data.Platform,
                PlatformIdentifier = $"demo-{i}",
                LaunchMethod = LaunchMethod.Executable,
                ExecutablePath = $@"C:\Games\Sample\{data.Name.Replace("'", string.Empty, StringComparison.Ordinal)}\game.exe",
                CoverImagePath = cover,
                BackgroundImagePath = background,
                Genre = data.Genre,
                ReleaseYear = data.Year,
                IsInstalled = data.Installed,
                IsFavorite = data.Favorite,
                Tags = [.. data.Tags],
                Developer = "Sample Studio",
                Description = "A fictional game from Arclight's sample library.",
                Notes = i == 1 ? "Left off at the Ember Gate. Try the north route next time." : null,
                DateAdded = now.AddDays(-(60 - (i * 3))),
                Source = GameSource.Manual,
            });
            added.Add(game);

            if (data.Hours <= 0)
            {
                continue;
            }

            // Spread the sample hours over several sessions ending on the configured day.
            var remaining = data.Hours * 3600;
            var day = data.DaysAgo;
            while (remaining > 600)
            {
                var length = Math.Min(remaining, 1800 + random.Next(0, 7200));
                // Always in the past: end the session a little before "now minus N days".
                var startOffset = now.AddDays(-day).AddSeconds(-length).AddMinutes(-(20 + random.Next(0, 240)));
                sessions.Start(game.Id, startOffset, SessionSource.ArclightLaunch);
                sessions.Complete(game.Id, startOffset, startOffset.AddSeconds(length));
                remaining -= length;
                day += 1 + random.Next(0, 4);
            }
        }

        var collections = library.Collections;
        if (collections.Count == 0)
        {
            await library.CreateCollectionAsync("Currently Playing", "Games you're in the middle of.");
            await library.CreateCollectionAsync("Backlog", "Games waiting for their turn.");
            await library.CreateCollectionAsync("Weekend Games", "Long sessions for slow weekends.");
        }

        await library.AddToCollectionAsync(library.Collections[0].Id, [added[0].Id, added[1].Id, added[3].Id]);
        await library.AddToCollectionAsync(library.Collections[1].Id, [added[4].Id, added[7].Id, added[10].Id]);
        await library.UpdateSettingsAsync(s => s with { FeaturedGameId = added[0].Id, DisplayName = "Player", ScanOnStartup = false, FirstRunCompleted = true });
    }
}
