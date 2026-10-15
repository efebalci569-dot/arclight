using System.Text;
using Arclight.Core.Launching;
using Arclight.Core.Models;
using Arclight.Platforms.Manual;
using Arclight.Platforms.Windows;
using Arclight.Platforms.Xbox;
using Microsoft.Win32;

namespace Arclight.Platforms.Tests;

public sealed class XboxAndFolderScannerTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Theory]
    [InlineData("CN=Microsoft Corporation, O=Microsoft Corporation, L=Redmond, S=Washington, C=US", "8wekyb3d8bbwe")]
    [InlineData("CN=D966EC41-E11D-4B7C-9DBB-07C0BA1F6997", "0c8vynj4cqe4e")]
    public void Publisher_id_matches_windows(string publisher, string expected)
    {
        Assert.Equal(expected, PackageIdentity.PublisherId(publisher));
    }

    [Fact]
    public async Task Reads_games_from_gaming_root_folders()
    {
        var drive = _temp.Dir("Drive");
        var header = Encoding.ASCII.GetBytes("RGBX").Concat(BitConverter.GetBytes(1)).Concat(Encoding.Unicode.GetBytes("MyGames\0")).ToArray();
        File.WriteAllBytes(Path.Combine(drive, ".GamingRoot"), header);

        _temp.File(@"Drive\MyGames\Stardew Valley\Content\MicrosoftGame.config", """
            <?xml version="1.0" encoding="utf-8"?>
            <Game configVersion="0">
              <Identity Name="ConcernedApe.StardewValleyPC" Publisher="CN=D966EC41-E11D-4B7C-9DBB-07C0BA1F6997" Version="1.6.0.0" />
              <ExecutableList><Executable Name="Stardew Valley.exe" Id="Game" /></ExecutableList>
              <ShellVisuals DefaultDisplayName="Stardew Valley" Square150x150Logo="Square150x150Logo.png" />
            </Game>
            """);
        _temp.File(@"Drive\MyGames\Stardew Valley\Content\Stardew Valley.exe");
        _temp.File(@"Drive\MyGames\Stardew Valley\Content\Square150x150Logo.scale-200.png", "png");
        _temp.Dir(@"Drive\MyGames\GameSave");

        var result = await new XboxScanner(() => [drive]).ScanAsync(new ScanOptions(), CancellationToken.None);

        var game = Assert.Single(result.Games);
        Assert.Equal("Stardew Valley", game.Name);
        Assert.Equal("ConcernedApe.StardewValleyPC_0c8vynj4cqe4e", game.PlatformIdentifier);
        Assert.Equal(@"shell:AppsFolder\ConcernedApe.StardewValleyPC_0c8vynj4cqe4e!Game", game.LaunchTarget);
        Assert.True(LaunchValidator.ValidateLauncherUri(game.LaunchTarget).IsValid);
        Assert.EndsWith("Stardew Valley.exe", game.ExecutablePath);
        Assert.EndsWith("Square150x150Logo.scale-200.png", Assert.Single(game.Artwork).Source);
    }

    [Fact]
    public async Task No_gaming_roots_means_not_found()
    {
        var result = await new XboxScanner(() => [_temp.Dir("Empty")]).ScanAsync(new ScanOptions(), CancellationToken.None);
        Assert.Equal(PlatformScanStatus.LauncherNotFound, result.Status);
    }

    [Fact]
    public async Task Folder_scan_suggests_the_main_executable_of_each_game()
    {
        var root = _temp.Dir("DRM-Free");
        _temp.Binary(@"DRM-Free\Celeste\Celeste.exe", 10);
        _temp.Binary(@"DRM-Free\Celeste\unins000.exe", 5000);
        _temp.Binary(@"DRM-Free\Celeste\UnityCrashHandler64.exe", 9000);
        _temp.Binary(@"DRM-Free\Hollow Knight\bin\game.exe", 3000);
        _temp.Binary(@"DRM-Free\Hollow Knight\bin\small.exe", 10);
        _temp.Dir(@"DRM-Free\Empty Folder");

        var result = await new FolderScanner().ScanAsync(new ScanOptions { CustomGameFolders = [root] }, CancellationToken.None);

        Assert.Equal(["Celeste", "Hollow Knight"], result.Games.Select(g => g.Name).Order());
        Assert.EndsWith(@"Celeste\Celeste.exe", result.Games.Single(g => g.Name == "Celeste").ExecutablePath);
        Assert.EndsWith(@"bin\game.exe", result.Games.Single(g => g.Name == "Hollow Knight").ExecutablePath);
        Assert.All(result.Games, g => Assert.Equal(GamePlatform.Manual, g.Platform));
    }

    [Fact]
    public async Task Folder_scan_is_disabled_without_folders()
    {
        var result = await new FolderScanner().ScanAsync(new ScanOptions(), CancellationToken.None);
        Assert.Equal(PlatformScanStatus.Disabled, result.Status);
    }

    [Fact]
    public async Task Launcher_explains_missing_files_without_starting_anything()
    {
        var launcher = new WindowsGameLauncher(new FakeRegistry());
        var game = new Game
        {
            Name = "Gone",
            LaunchMethod = LaunchMethod.Executable,
            ExecutablePath = Path.Combine(_temp.Path, "missing", "game.exe"),
            InstallDirectory = Path.Combine(_temp.Path, "missing"),
        };

        var result = await launcher.LaunchAsync(game, new Arclight.Core.Interfaces.LaunchOptions());

        Assert.False(result.Success);
        Assert.Equal(LaunchError.InstallDirectoryMissing, result.Error);

        game.InstallDirectory = _temp.Path;
        Assert.Equal(LaunchError.ExecutableNotFound, (await launcher.LaunchAsync(game, new Arclight.Core.Interfaces.LaunchOptions())).Error);
    }

    [Fact]
    public async Task Launcher_refuses_links_without_a_registered_handler()
    {
        var launcher = new WindowsGameLauncher(new FakeRegistry());
        var game = new Game { Name = "CS2", Platform = GamePlatform.Steam, LaunchMethod = LaunchMethod.LauncherUri, LaunchTarget = "steam://rungameid/730" };

        var result = await launcher.LaunchAsync(game, new Arclight.Core.Interfaces.LaunchOptions());

        Assert.Equal(LaunchError.LauncherNotInstalled, result.Error);
        Assert.False(launcher.IsLauncherAvailable(GamePlatform.Steam));
        Assert.True(new WindowsGameLauncher(new FakeRegistry().Set(RegistryHive.ClassesRoot, "steam", "URL Protocol", "")).IsLauncherAvailable(GamePlatform.Steam));
    }

    [Fact]
    public async Task Launcher_rejects_invalid_configuration_and_uninstalled_games()
    {
        var launcher = new WindowsGameLauncher(new FakeRegistry().Set(RegistryHive.ClassesRoot, "steam", "URL Protocol", ""));

        var invalid = await launcher.LaunchAsync(new Game { Name = "X", LaunchMethod = LaunchMethod.LauncherUri, LaunchTarget = "http://evil" }, new());
        Assert.Equal(LaunchError.InvalidConfiguration, invalid.Error);

        var notInstalled = await launcher.LaunchAsync(
            new Game { Name = "X", LaunchMethod = LaunchMethod.LauncherUri, LaunchTarget = "steam://rungameid/1", IsInstalled = false }, new());
        Assert.Equal(LaunchError.NotInstalled, notInstalled.Error);
    }
}
