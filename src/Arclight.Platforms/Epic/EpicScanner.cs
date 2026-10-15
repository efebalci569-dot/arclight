using System.Text.Json;
using Arclight.Core.Interfaces;
using Arclight.Core.Launching;
using Arclight.Core.Models;
using Arclight.Platforms.Windows;
using Microsoft.Win32;

namespace Arclight.Platforms.Epic;

/// <summary>
/// Reads the Epic Games Launcher's install manifests (<c>*.item</c> JSON files under
/// ProgramData), plus the launcher's catalog cache for official artwork (<see cref="EpicCatalog"/>).
/// The formats are undocumented, so parsing is defensive and isolated here.
/// </summary>
public sealed class EpicScanner(IRegistryReader registry, string? manifestDirectoryOverride = null) : IPlatformScanner
{
    public GamePlatform Platform => GamePlatform.Epic;

    public string Id => "epic";

    public string DisplayName => "Epic Games";

    public PlatformSupportLevel SupportLevel => PlatformSupportLevel.Supported;

    public Task<PlatformScanResult> ScanAsync(ScanOptions options, CancellationToken cancellationToken)
    {
        var directory = FindManifestDirectory();
        if (directory is null)
        {
            return Task.FromResult(PlatformScanResult.NotFound(Platform, DisplayName, SupportLevel));
        }

        var catalog = EpicCatalog.Load(Path.Combine(Path.GetDirectoryName(directory) ?? directory, "Catalog", "catcache.bin"));
        var games = new List<DetectedGame>();
        var errors = new List<string>();
        foreach (var file in Directory.EnumerateFiles(directory, "*.item"))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                if (ParseManifest(File.ReadAllText(file), catalog) is { } game &&
                    !games.Any(g => g.PlatformIdentifier.Equals(game.PlatformIdentifier, StringComparison.OrdinalIgnoreCase)))
                {
                    games.Add(game);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
            {
                errors.Add($"Skipped {Path.GetFileName(file)}: {ex.Message}");
            }
        }

        return Task.FromResult(new PlatformScanResult(Platform, DisplayName, SupportLevel, PlatformScanStatus.Ok, games, errors));
    }

    public string? FindManifestDirectory()
    {
        if (manifestDirectoryOverride is not null)
        {
            return Directory.Exists(manifestDirectoryOverride) ? manifestDirectoryOverride : null;
        }

        var dataPath = registry.GetString(RegistryHive.LocalMachine, @"SOFTWARE\WOW6432Node\Epic Games\EpicGamesLauncher", "AppDataPath");
        string?[] candidates =
        [
            dataPath is null ? null : Path.Combine(dataPath, "Manifests"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "Epic", "EpicGamesLauncher", "Data", "Manifests"),
        ];

        return candidates.FirstOrDefault(c => c is not null && Directory.Exists(c));
    }

    /// <summary>Turns one manifest into a detected game, or null for DLC, engines and other non-game items.</summary>
    internal static DetectedGame? ParseManifest(string json, IReadOnlyDictionary<string, IReadOnlyList<ArtworkCandidate>>? catalog = null)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = true });
        var root = document.RootElement;

        var displayName = GetString(root, "DisplayName");
        var appName = GetString(root, "AppName");
        var catalogNamespace = GetString(root, "CatalogNamespace");
        var catalogItemId = GetString(root, "CatalogItemId");
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(appName) ||
            string.IsNullOrWhiteSpace(catalogNamespace) || string.IsNullOrWhiteSpace(catalogItemId))
        {
            return null;
        }

        // DLC manifests point at their parent game.
        var mainGame = GetString(root, "MainGameAppName");
        if (!string.IsNullOrWhiteSpace(mainGame) && !string.Equals(mainGame, appName, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (root.TryGetProperty("AppCategories", out var categories) && categories.ValueKind == JsonValueKind.Array)
        {
            var list = categories.EnumerateArray().Select(c => c.GetString() ?? string.Empty).ToList();
            if (list.Count > 0 && !list.Contains("games", StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }
        }

        var installLocation = GetString(root, "InstallLocation");
        var launchExecutable = GetString(root, "LaunchExecutable");
        var incomplete = root.TryGetProperty("bIsIncompleteInstall", out var flag) && flag.ValueKind == JsonValueKind.True;
        var installed = !incomplete && !string.IsNullOrWhiteSpace(installLocation) && Directory.Exists(installLocation);

        string? executable = null;
        if (!string.IsNullOrWhiteSpace(installLocation) && !string.IsNullOrWhiteSpace(launchExecutable))
        {
            executable = Path.GetFullPath(Path.Combine(installLocation, launchExecutable));
        }

        return new DetectedGame
        {
            Platform = GamePlatform.Epic,
            PlatformIdentifier = $"{catalogNamespace}:{catalogItemId}:{appName}",
            Name = displayName.Trim(),
            InstallDirectory = string.IsNullOrWhiteSpace(installLocation) ? null : installLocation,
            ExecutablePath = executable,
            LaunchMethod = LaunchMethod.LauncherUri,
            LaunchTarget = LauncherUris.EpicRun(catalogNamespace, catalogItemId, appName),
            IsInstalled = installed,
            Artwork =
            [
                .. catalog is not null && catalog.TryGetValue(catalogItemId, out var art) ? art : [],
                .. executable is not null && installed ? [new ArtworkCandidate(ArtworkKind.Icon, executable)] : Array.Empty<ArtworkCandidate>(),
            ],
        };
    }

    private static string? GetString(JsonElement element, string property) =>
        element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;
}
