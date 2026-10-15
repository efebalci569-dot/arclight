using Arclight.Core.Models;
using Arclight.Platforms.Steam;

namespace Arclight.Platforms.Tests;

public sealed class SteamScannerTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    private static string Manifest(string appId, string name, string installDir, int stateFlags = 4) => $$"""
        "AppState"
        {
            "appid"      "{{appId}}"
            "name"       "{{name}}"
            "StateFlags" "{{stateFlags}}"
            "installdir" "{{installDir}}"
        }
        """;

    private string CreateSteam()
    {
        var steam = _temp.Dir("Steam");
        var library2 = _temp.Dir("SteamLibrary");
        _temp.File("Steam/steamapps/libraryfolders.vdf", $$"""
            "libraryfolders"
            {
                "0" { "path" "{{steam.Replace("\\", "\\\\")}}" }
                "1" { "path" "{{library2.Replace("\\", "\\\\")}}" }
            }
            """);

        _temp.File("Steam/steamapps/appmanifest_730.acf", Manifest("730", "Counter-Strike 2", "Counter-Strike Global Offensive"));
        _temp.Dir("Steam/steamapps/common/Counter-Strike Global Offensive");
        _temp.File("Steam/steamapps/appmanifest_228980.acf", Manifest("228980", "Steamworks Common Redistributables", "Steamworks Shared"));
        _temp.Dir("Steam/steamapps/common/Steamworks Shared");

        _temp.File("SteamLibrary/steamapps/appmanifest_1145360.acf", Manifest("1145360", "Hades", "Hades"));
        _temp.Dir("SteamLibrary/steamapps/common/Hades");

        // Still downloading: StateFlags without the "fully installed" bit.
        _temp.File("SteamLibrary/steamapps/appmanifest_413150.acf", Manifest("413150", "Stardew Valley", "Stardew Valley", stateFlags: 1026));

        _temp.File("SteamLibrary/steamapps/appmanifest_broken.acf", "\"AppState\" {");

        _temp.File("Steam/appcache/librarycache/1145360/abc123/library_600x900.jpg", "jpg");
        _temp.File("Steam/appcache/librarycache/730_library_600x900.jpg", "jpg");
        _temp.File("Steam/appcache/librarycache/413150/701632e4/library_capsule.jpg", "jpg");
        return steam;
    }

    [Fact]
    public async Task Finds_games_in_every_library_folder()
    {
        var steam = CreateSteam();
        var scanner = new SteamScanner(new FakeRegistry(), steam);

        var result = await scanner.ScanAsync(new ScanOptions(), CancellationToken.None);

        Assert.Equal(PlatformScanStatus.Ok, result.Status);
        Assert.Equal(["1145360", "228980", "413150", "730"], result.Games.Select(g => g.PlatformIdentifier).Order());

        var hades = result.Games.Single(g => g.Name == "Hades");
        Assert.True(hades.IsInstalled);
        Assert.Equal("steam://rungameid/1145360", hades.LaunchTarget);
        Assert.Equal(LaunchMethod.LauncherUri, hades.LaunchMethod);
        Assert.EndsWith(@"SteamLibrary\steamapps\common\Hades", hades.InstallDirectory);

        Assert.False(result.Games.Single(g => g.Name == "Stardew Valley").IsInstalled);
        Assert.True(result.Games.Single(g => g.PlatformIdentifier == "228980").LikelyNotAGame);
        Assert.False(hades.LikelyNotAGame);
        Assert.Single(result.Errors); // the broken manifest
    }

    [Fact]
    public async Task Prefers_local_artwork_and_falls_back_to_the_cdn()
    {
        var steam = CreateSteam();
        var result = await new SteamScanner(new FakeRegistry(), steam).ScanAsync(new ScanOptions(), CancellationToken.None);

        var hades = result.Games.Single(g => g.Name == "Hades");
        var firstCover = hades.Artwork.First(a => a.Kind == ArtworkKind.Cover);
        Assert.False(firstCover.IsRemote);
        Assert.EndsWith("library_600x900.jpg", firstCover.Source);

        var cs = result.Games.Single(g => g.PlatformIdentifier == "730");
        Assert.EndsWith("730_library_600x900.jpg", cs.Artwork.First(a => a.Kind == ArtworkKind.Cover).Source);

        // Newer Steam clients name the portrait cover library_capsule.jpg.
        var stardew = result.Games.Single(g => g.PlatformIdentifier == "413150");
        Assert.EndsWith("library_capsule.jpg", stardew.Artwork.First(a => a.Kind == ArtworkKind.Cover).Source);

        var starfall = result.Games.Single(g => g.PlatformIdentifier == "228980");
        var remote = starfall.Artwork.First(a => a.Kind == ArtworkKind.Cover);
        Assert.True(remote.IsRemote);
        Assert.Equal("https://shared.steamstatic.com/store_item_assets/steam/apps/228980/library_600x900.jpg", remote.Source);
    }

    [Fact]
    public async Task Extra_library_folders_from_settings_are_scanned()
    {
        var steam = _temp.Dir("OnlySteam");
        var extra = _temp.Dir("Extra");
        _temp.File("Extra/steamapps/appmanifest_10.acf", Manifest("10", "Counter-Strike", "Half-Life"));
        _temp.Dir("Extra/steamapps/common/Half-Life");

        var result = await new SteamScanner(new FakeRegistry(), steam)
            .ScanAsync(new ScanOptions { ExtraSteamLibraryFolders = [extra] }, CancellationToken.None);

        Assert.Equal("Counter-Strike", Assert.Single(result.Games).Name);
    }

    [Fact]
    public void Old_libraryfolders_format_is_supported()
    {
        var steam = _temp.Dir("OldSteam");
        _temp.File("OldSteam/steamapps/libraryfolders.vdf", """
            "LibraryFolders"
            {
                "TimeNextStatsReport"  "1234"
                "ContentStatsID"       "-1"
                "1"                    "D:\\Games\\SteamLibrary"
            }
            """);

        var folders = SteamScanner.GetLibraryFolders(steam, [], []);

        Assert.Equal(2, folders.Count);
        Assert.Equal(@"D:\Games\SteamLibrary", folders[1]);
    }

    [Fact]
    public async Task Missing_steam_is_reported_as_not_found()
    {
        var result = await new SteamScanner(new FakeRegistry(), Path.Combine(_temp.Path, "nope")).ScanAsync(new ScanOptions(), CancellationToken.None);
        Assert.Equal(PlatformScanStatus.LauncherNotFound, result.Status);
    }
}
