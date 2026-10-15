using System.Text.RegularExpressions;
using Arclight.Core.Interfaces;
using Arclight.Core.Library;
using Arclight.Core.Models;
using Arclight.Core.Tracking;

namespace Arclight.Platforms.Manual;

/// <summary>
/// Looks for DRM-free and standalone games in folders the user chose in Settings. Each direct
/// subfolder is treated as one game and the most likely executable is suggested. Results are only
/// suggestions: the scan window shows them unchecked-by-default style notes so the user confirms.
/// </summary>
public sealed class FolderScanner : IPlatformScanner
{
    private const int MaxGamesPerFolder = 500;
    private const int MaxDepth = 3;

    private static readonly Regex[] HelperPatterns = GameProcessMatcher.DefaultExcludedPatterns
        .Select(p => new Regex("^" + Regex.Escape(p).Replace(@"\*", ".*", StringComparison.Ordinal) + "$", RegexOptions.IgnoreCase))
        .ToArray();

    public GamePlatform Platform => GamePlatform.Manual;

    public string Id => "folders";

    public string DisplayName => "Game folders";

    public PlatformSupportLevel SupportLevel => PlatformSupportLevel.Experimental;

    public Task<PlatformScanResult> ScanAsync(ScanOptions options, CancellationToken cancellationToken)
    {
        if (options.CustomGameFolders.Count == 0)
        {
            return Task.FromResult(new PlatformScanResult(Platform, DisplayName, SupportLevel, PlatformScanStatus.Disabled, [], []));
        }

        var games = new List<DetectedGame>();
        var errors = new List<string>();
        foreach (var root in options.CustomGameFolders)
        {
            if (!Directory.Exists(root))
            {
                errors.Add($"The folder {root} does not exist.");
                continue;
            }

            try
            {
                foreach (var gameFolder in Directory.EnumerateDirectories(root).Take(MaxGamesPerFolder))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (FindMainExecutable(gameFolder) is { } exe)
                    {
                        var name = new DirectoryInfo(gameFolder).Name;
                        games.Add(new DetectedGame
                        {
                            Platform = GamePlatform.Manual,
                            PlatformIdentifier = "folder:" + (PathKeys.Normalize(exe) ?? exe),
                            Name = name,
                            InstallDirectory = gameFolder,
                            ExecutablePath = exe,
                            LaunchMethod = LaunchMethod.Executable,
                            Note = $"Suggested executable: {Path.GetRelativePath(gameFolder, exe)}",
                            Artwork = [new ArtworkCandidate(ArtworkKind.Icon, exe)],
                        });
                    }
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"Could not read {root}: {ex.Message}");
            }
        }

        return Task.FromResult(new PlatformScanResult(Platform, DisplayName, SupportLevel, PlatformScanStatus.Ok, games, errors));
    }

    /// <summary>Prefers an exe named like its folder, then the largest non-helper exe near the top.</summary>
    internal static string? FindMainExecutable(string gameFolder)
    {
        var folderKey = Simplify(new DirectoryInfo(gameFolder).Name);
        var candidates = new List<(string Path, long Size, int Depth)>();
        Collect(gameFolder, 0, candidates);
        if (candidates.Count == 0)
        {
            return null;
        }

        return candidates
            .OrderByDescending(c => folderKey.Length > 2 && Simplify(Path.GetFileNameWithoutExtension(c.Path)).Contains(folderKey, StringComparison.Ordinal))
            .ThenBy(c => c.Depth)
            .ThenByDescending(c => c.Size)
            .First().Path;
    }

    private static void Collect(string directory, int depth, List<(string, long, int)> results)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(directory, "*.exe"))
            {
                var name = Path.GetFileName(file);
                if (!HelperPatterns.Any(p => p.IsMatch(name)))
                {
                    results.Add((file, new FileInfo(file).Length, depth));
                }
            }

            if (depth >= MaxDepth)
            {
                return;
            }

            foreach (var child in Directory.EnumerateDirectories(directory))
            {
                var childName = Path.GetFileName(child);
                if (childName.StartsWith('_') || childName.Equals("redist", StringComparison.OrdinalIgnoreCase) ||
                    childName.Contains("support", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Collect(child, depth + 1, results);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static string Simplify(string value) =>
        new(TextSearch.Normalize(value).Where(char.IsLetterOrDigit).ToArray());
}
