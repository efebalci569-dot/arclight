using System.Text;
using System.Xml.Linq;
using Arclight.Core.Interfaces;
using Arclight.Core.Launching;
using Arclight.Core.Models;

namespace Arclight.Platforms.Xbox;

/// <summary>
/// Finds PC Game Pass / Microsoft Store games installed in the user-accessible "XboxGames" folders
/// that Windows records in each drive's <c>.GamingRoot</c> file. Games installed into the protected
/// WindowsApps folder are not read (that would need elevated access) and must be added manually.
/// </summary>
public sealed class XboxScanner(Func<IEnumerable<string>>? driveRootsProvider = null) : IPlatformScanner
{
    public GamePlatform Platform => GamePlatform.Xbox;

    public string Id => "xbox";

    public string DisplayName => "Xbox (PC Game Pass)";

    public PlatformSupportLevel SupportLevel => PlatformSupportLevel.Experimental;

    public Task<PlatformScanResult> ScanAsync(ScanOptions options, CancellationToken cancellationToken)
    {
        var gameRoots = FindGameRoots().ToList();
        if (gameRoots.Count == 0)
        {
            return Task.FromResult(PlatformScanResult.NotFound(Platform, DisplayName, SupportLevel));
        }

        var games = new List<DetectedGame>();
        var errors = new List<string>();
        foreach (var root in gameRoots)
        {
            IEnumerable<string> folders;
            try
            {
                folders = Directory.EnumerateDirectories(root).ToList();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"Could not read {root}: {ex.Message}");
                continue;
            }

            foreach (var folder in folders)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    if (ReadGame(folder) is { } game && !games.Any(g => g.PlatformIdentifier == game.PlatformIdentifier))
                    {
                        games.Add(game);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Xml.XmlException)
                {
                    errors.Add($"Skipped {Path.GetFileName(folder)}: {ex.Message}");
                }
            }
        }

        return Task.FromResult(new PlatformScanResult(Platform, DisplayName, SupportLevel, PlatformScanStatus.Ok, games, errors));
    }

    private IEnumerable<string> FindGameRoots()
    {
        var drives = driveRootsProvider?.Invoke() ?? DriveInfo.GetDrives()
            .Where(d => d.DriveType == DriveType.Fixed && d.IsReady)
            .Select(d => d.RootDirectory.FullName);

        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var drive in drives)
        {
            foreach (var folder in ReadGamingRoot(Path.Combine(drive, ".GamingRoot")))
            {
                var path = Path.Combine(drive, folder);
                if (Directory.Exists(path))
                {
                    roots.Add(path);
                }
            }

            var fallback = Path.Combine(drive, "XboxGames");
            if (Directory.Exists(fallback))
            {
                roots.Add(fallback);
            }
        }

        return roots;
    }

    /// <summary>The .GamingRoot file is "RGBX", a 32-bit count, then null-terminated UTF-16 folder names.</summary>
    internal static IReadOnlyList<string> ReadGamingRoot(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return [];
            }

            var bytes = File.ReadAllBytes(path);
            if (bytes.Length < 8 || Encoding.ASCII.GetString(bytes, 0, 4) != "RGBX")
            {
                return [];
            }

            var text = Encoding.Unicode.GetString(bytes, 8, (bytes.Length - 8) & ~1);
            return text.Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Where(name => name.IndexOfAny(Path.GetInvalidPathChars()) < 0 && !Path.IsPathRooted(name))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return [];
        }
    }

    internal static DetectedGame? ReadGame(string folder)
    {
        var content = Path.Combine(folder, "Content");
        var manifestPath = Path.Combine(content, "appxmanifest.xml");
        var configPath = Path.Combine(content, "MicrosoftGame.config");
        if (!File.Exists(manifestPath) && !File.Exists(configPath))
        {
            return null;
        }

        XElement? manifest = File.Exists(manifestPath) ? XDocument.Load(manifestPath).Root : null;
        XElement? config = File.Exists(configPath) ? XDocument.Load(configPath).Root : null;

        var identity = Child(manifest, "Identity") ?? Child(config, "Identity");
        var packageName = identity?.Attribute("Name")?.Value;
        var publisher = identity?.Attribute("Publisher")?.Value;
        if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(publisher))
        {
            return null;
        }

        var configExecutable = Descendant(config, "Executable");
        var manifestApplication = Descendant(manifest, "Application");
        var appId = configExecutable?.Attribute("Id")?.Value ?? manifestApplication?.Attribute("Id")?.Value ?? "Game";

        var shellVisuals = Child(config, "ShellVisuals");
        var name = FirstReadable(
            shellVisuals?.Attribute("DefaultDisplayName")?.Value,
            Descendant(manifest, "DisplayName")?.Value,
            Descendant(manifest, "VisualElements")?.Attribute("DisplayName")?.Value,
            new DirectoryInfo(folder).Name);

        string? executable = null;
        if (configExecutable?.Attribute("Name")?.Value is { Length: > 0 } exeName)
        {
            var candidate = Path.GetFullPath(Path.Combine(content, exeName));
            if (candidate.StartsWith(content, StringComparison.OrdinalIgnoreCase) && File.Exists(candidate))
            {
                executable = candidate;
            }
        }

        var artwork = new List<ArtworkCandidate>();
        foreach (var logo in new[]
                 {
                     shellVisuals?.Attribute("Square150x150Logo")?.Value,
                     shellVisuals?.Attribute("StoreLogo")?.Value,
                     Descendant(manifest, "Logo")?.Value,
                 })
        {
            if (ResolveScaledAsset(content, logo) is { } found)
            {
                artwork.Add(new ArtworkCandidate(ArtworkKind.Icon, found));
                break;
            }
        }

        var familyName = PackageIdentity.FamilyName(packageName, publisher);
        return new DetectedGame
        {
            Platform = GamePlatform.Xbox,
            PlatformIdentifier = familyName,
            Name = name.Trim(),
            InstallDirectory = content,
            ExecutablePath = executable,
            LaunchMethod = LaunchMethod.LauncherUri,
            LaunchTarget = LauncherUris.XboxAppsFolder(familyName, appId),
            IsInstalled = true,
            LikelyNotAGame = name.Contains("Launcher", StringComparison.OrdinalIgnoreCase),
            Artwork = artwork,
        };
    }

    /// <summary>Store assets are often only present with a scale qualifier, e.g. Logo.scale-200.png.</summary>
    private static string? ResolveScaledAsset(string content, string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        var exact = Path.Combine(content, relative);
        if (File.Exists(exact))
        {
            return exact;
        }

        var directory = Path.GetDirectoryName(exact)!;
        if (!Directory.Exists(directory))
        {
            return null;
        }

        var stem = Path.GetFileNameWithoutExtension(relative);
        var extension = Path.GetExtension(relative);
        return Directory.EnumerateFiles(directory, $"{stem}.*{extension}")
            .OrderByDescending(f => new FileInfo(f).Length)
            .FirstOrDefault();
    }

    private static string FirstReadable(params string?[] values) =>
        values.First(v => !string.IsNullOrWhiteSpace(v) && !v.StartsWith("ms-resource:", StringComparison.OrdinalIgnoreCase))!;

    private static XElement? Child(XElement? parent, string localName) =>
        parent?.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    private static XElement? Descendant(XElement? parent, string localName) =>
        parent?.Descendants().FirstOrDefault(e => e.Name.LocalName == localName);
}
