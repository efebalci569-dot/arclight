using Arclight.Core.Models;
using Arclight.Platforms.Ea;
using Arclight.Platforms.Gog;
using Arclight.Platforms.Ubisoft;
using Microsoft.Win32;

namespace Arclight.Platforms.Tests;

public sealed class RegistryScannerTests : IDisposable
{
    private readonly TempFolder _temp = new();

    public void Dispose() => _temp.Dispose();

    [Fact]
    public async Task Gog_games_come_from_the_registry_and_dlc_is_skipped()
    {
        var exe = _temp.File(@"GOG Games\Witcher 3\bin\x64\witcher3.exe");
        var icon = _temp.File(@"GOG Games\Witcher 3\goggame-1207664663.ico");
        var registry = new FakeRegistry()
            .Set(RegistryHive.LocalMachine, GogScanner.GamesKey + @"\1207664663", "gameName", "The Witcher 3")
            .Set(RegistryHive.LocalMachine, GogScanner.GamesKey + @"\1207664663", "gameID", "1207664663")
            .Set(RegistryHive.LocalMachine, GogScanner.GamesKey + @"\1207664663", "path", Path.Combine(_temp.Path, @"GOG Games\Witcher 3"))
            .Set(RegistryHive.LocalMachine, GogScanner.GamesKey + @"\1207664663", "exe", exe)
            .Set(RegistryHive.LocalMachine, GogScanner.GamesKey + @"\1207664663", "launchParam", "-skipIntro")
            .Set(RegistryHive.LocalMachine, GogScanner.GamesKey + @"\1640424747", "gameName", "Witcher 3 DLC")
            .Set(RegistryHive.LocalMachine, GogScanner.GamesKey + @"\1640424747", "exe", exe)
            .Set(RegistryHive.LocalMachine, GogScanner.GamesKey + @"\1640424747", "dependsOn", "1207664663");

        var result = await new GogScanner(registry).ScanAsync(new ScanOptions(), CancellationToken.None);

        var game = Assert.Single(result.Games);
        Assert.Equal("The Witcher 3", game.Name);
        Assert.Equal(exe, game.ExecutablePath);
        Assert.Equal(LaunchMethod.Executable, game.LaunchMethod);
        Assert.Equal("-skipIntro", game.LaunchArguments);
        Assert.True(game.IsInstalled);
        Assert.Equal(icon, game.Artwork[0].Source);
    }

    [Fact]
    public async Task Gog_not_installed_when_registry_key_is_missing()
    {
        var result = await new GogScanner(new FakeRegistry()).ScanAsync(new ScanOptions(), CancellationToken.None);
        Assert.Equal(PlatformScanStatus.LauncherNotFound, result.Status);
    }

    [Fact]
    public async Task Ubisoft_installs_use_uninstall_names_and_uplay_links()
    {
        var dir = _temp.Dir(@"Ubisoft\Far Cry 5");
        var registry = new FakeRegistry()
            .Set(RegistryHive.LocalMachine, UbisoftScanner.LauncherKey, "InstallDir", @"C:\Ubisoft\")
            .Set(RegistryHive.LocalMachine, UbisoftScanner.InstallsKey + @"\635", "InstallDir", dir.Replace('\\', '/') + "/")
            .Set(RegistryHive.LocalMachine, UbisoftScanner.UninstallKey + @"\Uplay Install 635", "DisplayName", "Far Cry 5");

        var result = await new UbisoftScanner(registry).ScanAsync(new ScanOptions(), CancellationToken.None);

        var game = Assert.Single(result.Games);
        Assert.Equal("Far Cry 5", game.Name);
        Assert.Equal("uplay://launch/635/0", game.LaunchTarget);
        Assert.True(game.IsInstalled);
        Assert.Equal(PlatformSupportLevel.Experimental, result.SupportLevel);
    }

    [Fact]
    public async Task Ea_app_is_reported_honestly_as_unsupported()
    {
        var registry = new FakeRegistry().Set(RegistryHive.LocalMachine, @"SOFTWARE\Electronic Arts\EA Desktop", "InstallLocation", @"C:\EA");
        var result = await new EaAppScanner(registry).ScanAsync(new ScanOptions(), CancellationToken.None);

        Assert.Equal(PlatformScanStatus.Unsupported, result.Status);
        Assert.Empty(result.Games);
    }
}
