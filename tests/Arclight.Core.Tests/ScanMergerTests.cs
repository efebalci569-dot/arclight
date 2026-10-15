using Arclight.Core.Library;
using Arclight.Core.Models;

namespace Arclight.Core.Tests;

public class ScanMergerTests
{
    private static DetectedGame Steam(string id, string name, string? dir = null, bool installed = true) => new()
    {
        Platform = GamePlatform.Steam,
        PlatformIdentifier = id,
        Name = name,
        InstallDirectory = dir,
        IsInstalled = installed,
    };

    private static PlatformScanResult Ok(GamePlatform platform, params DetectedGame[] games) =>
        new(platform, platform.ToString(), PlatformSupportLevel.Supported, PlatformScanStatus.Ok, games, []);

    private static readonly HashSet<string> NoIgnores = [];

    [Fact]
    public void Classifies_new_and_existing_games_by_platform_id()
    {
        var library = new List<Game>
        {
            new() { Id = 1, Name = "Hades", Platform = GamePlatform.Steam, PlatformIdentifier = "1145360", Source = GameSource.Scan },
        };

        var result = ScanMerger.Merge(library, [Ok(GamePlatform.Steam, Steam("1145360", "Hades"), Steam("730", "Counter-Strike 2"))], NoIgnores, true);

        Assert.Equal(ScanCandidateStatus.InLibrary, result.Candidates.Single(c => c.Detected.PlatformIdentifier == "1145360").Status);
        Assert.Equal(1, result.Candidates.Single(c => c.Detected.PlatformIdentifier == "1145360").ExistingGameId);
        Assert.Equal(ScanCandidateStatus.New, result.Candidates.Single(c => c.Detected.PlatformIdentifier == "730").Status);
        Assert.Empty(result.Updates);
    }

    [Fact]
    public void Matches_manually_added_game_by_executable_path()
    {
        var library = new List<Game>
        {
            new() { Id = 7, Name = "My GOG game", ExecutablePath = @"C:\Games\Witcher\witcher3.exe" },
        };
        var detected = new DetectedGame
        {
            Platform = GamePlatform.Gog,
            PlatformIdentifier = "1207664663",
            Name = "The Witcher 3",
            ExecutablePath = @"c:\games\witcher\WITCHER3.EXE",
        };

        var result = ScanMerger.Merge(library, [Ok(GamePlatform.Gog, detected)], NoIgnores, true);

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(ScanCandidateStatus.InLibrary, candidate.Status);
        Assert.Equal("Same executable", candidate.MatchReason);
    }

    [Fact]
    public void Ignored_games_are_reported_as_ignored()
    {
        var ignored = new HashSet<string> { ScanMerger.IdentityKey(GamePlatform.Steam, "228980") };
        var result = ScanMerger.Merge([], [Ok(GamePlatform.Steam, Steam("228980", "Steamworks Common Redistributables"))], ignored, true);
        Assert.Equal(ScanCandidateStatus.Ignored, Assert.Single(result.Candidates).Status);
    }

    [Fact]
    public void Folder_scan_duplicates_of_launcher_games_are_dropped()
    {
        var steam = Steam("1145360", "Hades", dir: @"D:\Steam\steamapps\common\Hades");
        var folder = new DetectedGame
        {
            Platform = GamePlatform.Manual,
            PlatformIdentifier = @"folder:D:\STEAM\STEAMAPPS\COMMON\HADES\HADES.EXE",
            Name = "Hades",
            InstallDirectory = @"D:\Steam\steamapps\common\Hades",
            ExecutablePath = @"D:\Steam\steamapps\common\Hades\Hades.exe",
        };

        var result = ScanMerger.Merge([], [Ok(GamePlatform.Manual, folder), Ok(GamePlatform.Steam, steam)], NoIgnores, true);

        var candidate = Assert.Single(result.Candidates);
        Assert.Equal(GamePlatform.Steam, candidate.Detected.Platform);
    }

    [Fact]
    public void Missing_scanned_games_are_marked_uninstalled_only_for_successfully_scanned_platforms()
    {
        var library = new List<Game>
        {
            new() { Id = 1, Name = "Gone", Platform = GamePlatform.Steam, PlatformIdentifier = "10", Source = GameSource.Scan, IsInstalled = true },
            new() { Id = 2, Name = "Epic game", Platform = GamePlatform.Epic, PlatformIdentifier = "a:b:c", Source = GameSource.Scan, IsInstalled = true },
            new() { Id = 3, Name = "Manual Steam entry", Platform = GamePlatform.Steam, PlatformIdentifier = "20", Source = GameSource.Manual, IsInstalled = true },
        };
        var epicFailed = new PlatformScanResult(GamePlatform.Epic, "Epic", PlatformSupportLevel.Supported, PlatformScanStatus.Failed, [], ["boom"]);

        var result = ScanMerger.Merge(library, [Ok(GamePlatform.Steam), epicFailed], NoIgnores, markMissingAsUninstalled: true);

        var update = Assert.Single(result.Updates);
        Assert.Equal(1, update.GameId);
        Assert.False(update.IsInstalled);

        var disabled = ScanMerger.Merge(library, [Ok(GamePlatform.Steam)], NoIgnores, markMissingAsUninstalled: false);
        Assert.Empty(disabled.Updates);
    }

    [Fact]
    public void Reinstalled_game_produces_an_install_update()
    {
        var library = new List<Game>
        {
            new() { Id = 1, Name = "Hades", Platform = GamePlatform.Steam, PlatformIdentifier = "1145360", Source = GameSource.Scan, IsInstalled = false },
        };

        var result = ScanMerger.Merge(library, [Ok(GamePlatform.Steam, Steam("1145360", "Hades", dir: @"E:\Steam\common\Hades"))], NoIgnores, true);

        var update = Assert.Single(result.Updates);
        Assert.True(update.IsInstalled);
        Assert.Equal(@"E:\Steam\common\Hades", update.InstallDirectory);
    }

    [Fact]
    public void Same_game_twice_in_one_scan_is_listed_once()
    {
        var result = ScanMerger.Merge([], [Ok(GamePlatform.Steam, Steam("730", "CS2"), Steam("730", "CS2"))], NoIgnores, true);
        Assert.Single(result.Candidates);
    }
}
