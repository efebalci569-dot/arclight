using Arclight.Core.Artwork;
using Arclight.Core.Interfaces;
using Arclight.Core.Launching;
using Arclight.Core.Library;
using Arclight.Core.Models;
using Arclight.Platforms.Windows;
using Microsoft.Win32;

namespace Arclight.Platforms.Steam;

/// <summary>
/// Finds installed Steam games by reading <c>libraryfolders.vdf</c> and the <c>appmanifest_*.acf</c>
/// files in every library. Read-only: no credentials, no Steam API, no changes to Steam's files.
/// Only installed games are visible this way; owned-but-not-installed games need the Steam Web API.
/// </summary>
public sealed class SteamScanner(IRegistryReader registry, string? steamPathOverride = null) : IPlatformScanner
{
    /// <summary>App ids that are Steam runtime components or tools rather than games.</summary>
    internal static readonly IReadOnlySet<string> KnownNonGames = new HashSet<string>
    {
        "228980",  // Steamworks Common Redistributables
        "1070560", "1391110", "1628350", // Steam Linux Runtime
        "1493710", "1826330", "1161040", "2180100", // Proton & runtimes
        "961940", "1054830", "1113280", "1245040", "1420170", "1580130", "1887720", "2348590", "2805730", "3658110",
        "250820",  // SteamVR
        "431960",  // Wallpaper Engine
        "1905180", // OBS Studio
        "223850",  // 3DMark (benchmark)
    };

    private static readonly string[] NonGameNameHints =
        ["Redistributable", "Steam Linux Runtime", "Proton ", "Steamworks", "Dedicated Server", " SDK", "SteamVR"];

    public GamePlatform Platform => GamePlatform.Steam;

    public string Id => "steam";

    public string DisplayName => "Steam";

    public PlatformSupportLevel SupportLevel => PlatformSupportLevel.Supported;

    public Task<PlatformScanResult> ScanAsync(ScanOptions options, CancellationToken cancellationToken)
    {
        var steamPath = FindSteamPath();
        if (steamPath is null)
        {
            return Task.FromResult(PlatformScanResult.NotFound(Platform, DisplayName, SupportLevel));
        }

        var errors = new List<string>();
        var games = new List<DetectedGame>();
        var seen = new HashSet<string>();

        foreach (var library in GetLibraryFolders(steamPath, options.ExtraSteamLibraryFolders, errors))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var steamApps = Path.Combine(library, "steamapps");
            if (!Directory.Exists(steamApps))
            {
                continue;
            }

            IEnumerable<string> manifests;
            try
            {
                manifests = Directory.EnumerateFiles(steamApps, "appmanifest_*.acf").ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"Could not read the Steam library at {library}: {ex.Message}");
                continue;
            }

            foreach (var manifest in manifests)
            {
                try
                {
                    if (ReadManifest(manifest, steamApps, steamPath) is { } game && seen.Add(game.PlatformIdentifier))
                    {
                        games.Add(game);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or VdfFormatException)
                {
                    errors.Add($"Skipped {Path.GetFileName(manifest)}: {ex.Message}");
                }
            }
        }

        return Task.FromResult(new PlatformScanResult(Platform, DisplayName, SupportLevel, PlatformScanStatus.Ok, games, errors));
    }

    public string? FindSteamPath()
    {
        if (steamPathOverride is not null)
        {
            return Directory.Exists(steamPathOverride) ? steamPathOverride : null;
        }

        string?[] candidates =
        [
            registry.GetString(RegistryHive.CurrentUser, @"Software\Valve\Steam", "SteamPath"),
            registry.GetString(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam", "InstallPath"),
            registry.GetString(RegistryHive.LocalMachine, @"SOFTWARE\Valve\Steam", "InstallPath"),
        ];

        return candidates
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => Path.GetFullPath(c!.Replace('/', '\\')))
            .FirstOrDefault(Directory.Exists);
    }

    internal static IReadOnlyList<string> GetLibraryFolders(string steamPath, IReadOnlyList<string> extra, List<string> errors)
    {
        var folders = new List<string> { steamPath };
        var file = Path.Combine(steamPath, "steamapps", "libraryfolders.vdf");
        if (File.Exists(file))
        {
            try
            {
                var root = VdfParser.ParseFile(file);
                var libraries = root["libraryfolders"] ?? root["LibraryFolders"];
                foreach (var (key, node) in libraries?.Children ?? new Dictionary<string, VdfNode>())
                {
                    // Current format: "0" { "path" "D:\\SteamLibrary" ... }. Old format: "1" "D:\\SteamLibrary".
                    var path = node.IsObject ? node.GetString("path") : (int.TryParse(key, out _) ? node.Value : null);
                    if (!string.IsNullOrWhiteSpace(path))
                    {
                        folders.Add(path);
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or VdfFormatException)
            {
                errors.Add($"Could not read libraryfolders.vdf: {ex.Message}");
            }
        }

        folders.AddRange(extra);
        return folders
            .Where(f => !string.IsNullOrWhiteSpace(f))
            .Select(f => f.Replace('/', '\\').TrimEnd('\\'))
            .DistinctBy(f => PathKeys.Normalize(f) ?? f)
            .ToList();
    }

    internal static DetectedGame? ReadManifest(string manifestPath, string steamAppsDirectory, string steamPath)
    {
        var state = VdfParser.ParseFile(manifestPath)["AppState"];
        var appId = state?.GetString("appid");
        var name = state?.GetString("name");
        if (string.IsNullOrWhiteSpace(appId) || !appId.All(char.IsAsciiDigit) || string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var installDirName = state!.GetString("installdir");
        var installDirectory = string.IsNullOrWhiteSpace(installDirName)
            ? null
            : Path.Combine(steamAppsDirectory, "common", installDirName);

        _ = int.TryParse(state.GetString("StateFlags"), out var stateFlags);
        const int FullyInstalled = 4;
        var installed = (stateFlags & FullyInstalled) != 0 && installDirectory is not null && Directory.Exists(installDirectory);

        return new DetectedGame
        {
            Platform = GamePlatform.Steam,
            PlatformIdentifier = appId,
            Name = name.Trim(),
            InstallDirectory = installDirectory,
            LaunchMethod = LaunchMethod.LauncherUri,
            LaunchTarget = LauncherUris.SteamRun(appId),
            IsInstalled = installed,
            LikelyNotAGame = KnownNonGames.Contains(appId) || NonGameNameHints.Any(h => name.Contains(h, StringComparison.OrdinalIgnoreCase)),
            Artwork = FindArtwork(steamPath, appId),
        };
    }

    /// <summary>Steam's own library cache first (no network), then Steam's public CDN for the same app id.</summary>
    internal static IReadOnlyList<ArtworkCandidate> FindArtwork(string steamPath, string appId)
    {
        var candidates = new List<ArtworkCandidate>();
        var cache = Path.Combine(steamPath, "appcache", "librarycache");

        void AddLocal(ArtworkKind kind, string fileName)
        {
            var perApp = Path.Combine(cache, appId);
            if (Directory.Exists(perApp))
            {
                try
                {
                    var match = Directory.EnumerateFiles(perApp, fileName, SearchOption.AllDirectories).FirstOrDefault();
                    if (match is not null)
                    {
                        candidates.Add(new ArtworkCandidate(kind, match));
                        return;
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                }
            }

            var legacy = Path.Combine(cache, $"{appId}_{fileName}");
            if (File.Exists(legacy))
            {
                candidates.Add(new ArtworkCandidate(kind, legacy));
            }
        }

        // Older Steam clients use library_600x900/header; newer ones store library_capsule/library_header in hashed subfolders.
        AddLocal(ArtworkKind.Cover, "library_600x900.jpg");
        AddLocal(ArtworkKind.Cover, "library_capsule.jpg");
        AddLocal(ArtworkKind.Background, "library_hero.jpg");
        AddLocal(ArtworkKind.Background, "library_header.jpg");
        AddLocal(ArtworkKind.Background, "header.jpg");
        AddLocal(ArtworkKind.Logo, "logo.png");

        candidates.AddRange(SteamStore.CdnArtwork(appId));
        return candidates;
    }
}
