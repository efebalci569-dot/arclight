using Arclight.Core.Interfaces;
using Arclight.Core.Models;
using Arclight.Platforms.Windows;
using Microsoft.Win32;

namespace Arclight.Platforms.Ea;

/// <summary>
/// The EA app keeps its game list in encrypted local storage that Arclight does not read.
/// This scanner only reports whether the EA app is installed so the scan window can explain
/// that EA games have to be added manually.
/// </summary>
public sealed class EaAppScanner(IRegistryReader registry) : IPlatformScanner
{
    public GamePlatform Platform => GamePlatform.EA;

    public string Id => "ea";

    public string DisplayName => "EA app";

    public PlatformSupportLevel SupportLevel => PlatformSupportLevel.ManualOnly;

    public Task<PlatformScanResult> ScanAsync(ScanOptions options, CancellationToken cancellationToken)
    {
        var installed =
            registry.KeyExists(RegistryHive.LocalMachine, @"SOFTWARE\Electronic Arts\EA Desktop") ||
            registry.KeyExists(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Electronic Arts\EA Desktop") ||
            Directory.Exists(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Electronic Arts", "EA Desktop"));

        return Task.FromResult(installed
            ? new PlatformScanResult(Platform, DisplayName, SupportLevel, PlatformScanStatus.Unsupported, [],
                ["Automatic detection for EA app games is not available yet. Add them with \"Add a Game\"."])
            : PlatformScanResult.NotFound(Platform, DisplayName, SupportLevel));
    }
}
