using Arclight.Core.Interfaces;
using Arclight.Core.Launching;
using Arclight.Core.Models;
using Arclight.Platforms.Windows;
using Microsoft.Win32;

namespace Arclight.Platforms.Ubisoft;

/// <summary>
/// Ubisoft Connect lists installs under HKLM\SOFTWARE\WOW6432Node\Ubisoft\Launcher\Installs\{id}.
/// Names come from the matching uninstall entry. Experimental: the layout is undocumented.
/// </summary>
public sealed class UbisoftScanner(IRegistryReader registry) : IPlatformScanner
{
    internal const string LauncherKey = @"SOFTWARE\WOW6432Node\Ubisoft\Launcher";
    internal const string InstallsKey = LauncherKey + @"\Installs";
    internal const string UninstallKey = @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall";

    public GamePlatform Platform => GamePlatform.Ubisoft;

    public string Id => "ubisoft";

    public string DisplayName => "Ubisoft Connect";

    public PlatformSupportLevel SupportLevel => PlatformSupportLevel.Experimental;

    public Task<PlatformScanResult> ScanAsync(ScanOptions options, CancellationToken cancellationToken)
    {
        if (!registry.KeyExists(RegistryHive.LocalMachine, LauncherKey))
        {
            return Task.FromResult(PlatformScanResult.NotFound(Platform, DisplayName, SupportLevel));
        }

        var games = new List<DetectedGame>();
        foreach (var id in registry.GetSubKeyNames(RegistryHive.LocalMachine, InstallsKey))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!id.All(char.IsAsciiDigit))
            {
                continue;
            }

            var installDir = registry.GetString(RegistryHive.LocalMachine, $@"{InstallsKey}\{id}", "InstallDir");
            if (string.IsNullOrWhiteSpace(installDir))
            {
                continue;
            }

            installDir = Path.GetFullPath(installDir.Replace('/', '\\'));
            var name = registry.GetString(RegistryHive.LocalMachine, $@"{UninstallKey}\Uplay Install {id}", "DisplayName")
                       ?? new DirectoryInfo(installDir).Name;

            games.Add(new DetectedGame
            {
                Platform = GamePlatform.Ubisoft,
                PlatformIdentifier = id,
                Name = name.Trim(),
                InstallDirectory = installDir,
                LaunchMethod = LaunchMethod.LauncherUri,
                LaunchTarget = LauncherUris.UbisoftRun(id),
                IsInstalled = Directory.Exists(installDir),
            });
        }

        return Task.FromResult(new PlatformScanResult(Platform, DisplayName, SupportLevel, PlatformScanStatus.Ok, games, []));
    }
}
