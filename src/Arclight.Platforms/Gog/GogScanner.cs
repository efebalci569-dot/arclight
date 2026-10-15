using Arclight.Core.Interfaces;
using Arclight.Core.Models;
using Arclight.Platforms.Windows;
using Microsoft.Win32;

namespace Arclight.Platforms.Gog;

/// <summary>
/// GOG installers (with or without GOG Galaxy) register each game under
/// HKLM\SOFTWARE\WOW6432Node\GOG.com\Games\{id}. Games are started directly from their exe.
/// </summary>
public sealed class GogScanner(IRegistryReader registry) : IPlatformScanner
{
    internal const string GamesKey = @"SOFTWARE\WOW6432Node\GOG.com\Games";

    public GamePlatform Platform => GamePlatform.Gog;

    public string Id => "gog";

    public string DisplayName => "GOG";

    public PlatformSupportLevel SupportLevel => PlatformSupportLevel.Supported;

    public Task<PlatformScanResult> ScanAsync(ScanOptions options, CancellationToken cancellationToken)
    {
        if (!registry.KeyExists(RegistryHive.LocalMachine, GamesKey))
        {
            return Task.FromResult(PlatformScanResult.NotFound(Platform, DisplayName, SupportLevel));
        }

        var games = new List<DetectedGame>();
        foreach (var id in registry.GetSubKeyNames(RegistryHive.LocalMachine, GamesKey))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var key = $@"{GamesKey}\{id}";
            string? Read(string value) => registry.GetString(RegistryHive.LocalMachine, key, value);

            // DLC entries name the game they belong to.
            if (!string.IsNullOrWhiteSpace(Read("dependsOn")))
            {
                continue;
            }

            var name = Read("gameName") ?? Read("GAMENAME");
            var path = Read("path") ?? Read("PATH");
            var exe = Read("exe") ?? Read("EXE");
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(exe))
            {
                continue;
            }

            var gameId = Read("gameID") ?? id;
            var artwork = new List<ArtworkCandidate>();
            if (!string.IsNullOrWhiteSpace(path))
            {
                var icon = Path.Combine(path, $"goggame-{gameId}.ico");
                if (File.Exists(icon))
                {
                    artwork.Add(new ArtworkCandidate(ArtworkKind.Icon, icon));
                }
            }

            artwork.Add(new ArtworkCandidate(ArtworkKind.Icon, exe));

            games.Add(new DetectedGame
            {
                Platform = GamePlatform.Gog,
                PlatformIdentifier = gameId,
                Name = name.Trim(),
                InstallDirectory = path,
                ExecutablePath = exe,
                LaunchMethod = LaunchMethod.Executable,
                LaunchArguments = Read("launchParam"),
                IsInstalled = File.Exists(exe),
                Artwork = artwork,
            });
        }

        return Task.FromResult(new PlatformScanResult(Platform, DisplayName, SupportLevel, PlatformScanStatus.Ok, games, []));
    }
}
