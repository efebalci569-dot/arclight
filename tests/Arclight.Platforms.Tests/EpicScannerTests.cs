using Arclight.Core.Models;
using Arclight.Platforms.Epic;

namespace Arclight.Platforms.Tests;

public sealed class EpicScannerTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static string Item(string name, string app, string location, string categories = "\"public\", \"games\", \"applications\"", string main = "", bool incomplete = false) => $$"""
        {
            "FormatVersion": 0,
            "bIsIncompleteInstall": {{(incomplete ? "true" : "false")}},
            "LaunchExecutable": "Game.exe",
            "DisplayName": "{{name}}",
            "InstallLocation": "{{location.Replace("\\", "\\\\")}}",
            "CatalogNamespace": "ns-{{app}}",
            "CatalogItemId": "item-{{app}}",
            "AppName": "{{app}}",
            "AppCategories": [ {{categories}} ],
            "MainGameAppName": "{{main}}"
        }
        """;

    [Fact]
    public async Task Reads_installed_games_and_skips_dlc_and_engines()
    {
        var gameDir = _temp.Dir(@"Games\Fortnite");
        var manifests = _temp.Dir("Manifests");
        _temp.File("Manifests/a.item", Item("Fortnite", "Fortnite", gameDir));
        _temp.File("Manifests/b.item", Item("Some DLC", "dlc1", gameDir, main: "Fortnite"));
        _temp.File("Manifests/c.item", Item("Unreal Engine", "UE_5", gameDir, categories: "\"engines\""));
        _temp.File("Manifests/d.item", Item("Downloading", "dl", gameDir, incomplete: true));
        _temp.File("Manifests/e.item", "{ not json");

        var result = await new EpicScanner(new FakeRegistry(), manifests).ScanAsync(new ScanOptions(), CancellationToken.None);

        Assert.Equal(PlatformScanStatus.Ok, result.Status);
        Assert.Equal(["Downloading", "Fortnite"], result.Games.Select(g => g.Name).Order());
        Assert.Single(result.Errors);

        var fortnite = result.Games.Single(g => g.Name == "Fortnite");
        Assert.True(fortnite.IsInstalled);
        Assert.Equal("ns-Fortnite:item-Fortnite:Fortnite", fortnite.PlatformIdentifier);
        Assert.Equal("com.epicgames.launcher://apps/ns-Fortnite%3Aitem-Fortnite%3AFortnite?action=launch&silent=true", fortnite.LaunchTarget);
        Assert.Equal(Path.Combine(gameDir, "Game.exe"), fortnite.ExecutablePath);

        Assert.False(result.Games.Single(g => g.Name == "Downloading").IsInstalled);
    }

    [Fact]
    public async Task Covers_and_backgrounds_come_from_the_launchers_catalog_cache()
    {
        var gameDir = _temp.Dir(@"Games\GTAV");
        var manifests = _temp.Dir("Manifests");
        _temp.File("Manifests/a.item", Item("Grand Theft Auto V", "gta", gameDir));
        _temp.File("Manifests/b.item", Item("Unknown To Catalog", "other", gameDir));
        var catalog = """
            [
              { "id": "item-gta", "title": "Grand Theft Auto V", "keyImages": [
                { "type": "Thumbnail", "url": "https://cdn1.epicgames.com/x/thumb.jpg" },
                { "type": "DieselGameBox", "url": "https://cdn1.epicgames.com/ns/item/GTAV Landscape-2560x1440.jpg" },
                { "type": "DieselGameBoxTall", "url": "https://cdn1.epicgames.com/ns/item/GTAV Portrait-1200x1600.jpg" },
                { "type": "OfferImageTall", "url": "https://evil.example.com/tall.jpg" }
              ] },
              { "id": "item-broken", "keyImages": "nope" }
            ]
            """;
        _temp.File("Catalog/catcache.bin", Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(catalog)));

        var result = await new EpicScanner(new FakeRegistry(), manifests).ScanAsync(new ScanOptions(), CancellationToken.None);

        var gta = result.Games.Single(g => g.Name == "Grand Theft Auto V");
        Assert.Equal("https://cdn1.epicgames.com/ns/item/GTAV%20Portrait-1200x1600.jpg", gta.Artwork.Single(a => a.Kind == ArtworkKind.Cover).Source);
        Assert.Equal("https://cdn1.epicgames.com/ns/item/GTAV%20Landscape-2560x1440.jpg", gta.Artwork.Single(a => a.Kind == ArtworkKind.Background).Source);
        Assert.Contains(gta.Artwork, a => a.Kind == ArtworkKind.Icon);
        Assert.DoesNotContain(result.Games.Single(g => g.Name == "Unknown To Catalog").Artwork, a => a.IsRemote);
    }

    [Fact]
    public async Task A_damaged_catalog_cache_only_means_no_artwork()
    {
        var gameDir = _temp.Dir(@"Games\Game");
        var manifests = _temp.Dir("Manifests");
        _temp.File("Manifests/a.item", Item("Game", "game", gameDir));
        _temp.File("Catalog/catcache.bin", "this is not base64!");

        var result = await new EpicScanner(new FakeRegistry(), manifests).ScanAsync(new ScanOptions(), CancellationToken.None);

        Assert.Empty(result.Errors);
        Assert.DoesNotContain(Assert.Single(result.Games).Artwork, a => a.IsRemote);
    }

    [Fact]
    public async Task Missing_manifest_folder_means_launcher_not_found()
    {
        var result = await new EpicScanner(new FakeRegistry(), Path.Combine(_temp.Path, "missing")).ScanAsync(new ScanOptions(), CancellationToken.None);
        Assert.Equal(PlatformScanStatus.LauncherNotFound, result.Status);
    }
}
