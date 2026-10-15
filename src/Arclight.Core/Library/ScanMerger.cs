using Arclight.Core.Models;

namespace Arclight.Core.Library;

/// <summary>Result of comparing scan results with the library.</summary>
public sealed record ScanMergeResult(IReadOnlyList<ScanCandidate> Candidates, IReadOnlyList<GameInstallUpdate> Updates);

/// <summary>
/// Classifies detected games against the library so a scan never creates duplicates.
/// A detected game matches an existing one by platform id first, then by executable path,
/// then by install folder.
/// </summary>
public static class ScanMerger
{
    public static string IdentityKey(GamePlatform platform, string platformIdentifier) =>
        $"{PlatformInfo.Code(platform)}:{platformIdentifier.Trim().ToUpperInvariant()}";

    public static ScanMergeResult Merge(
        IReadOnlyCollection<Game> library,
        IEnumerable<PlatformScanResult> results,
        IReadOnlySet<string> ignoredIdentityKeys,
        bool markMissingAsUninstalled)
    {
        var byIdentity = new Dictionary<string, Game>();
        var byExecutable = new Dictionary<string, Game>();
        var byDirectory = new Dictionary<string, Game>();
        foreach (var game in library)
        {
            if (!string.IsNullOrWhiteSpace(game.PlatformIdentifier))
            {
                byIdentity.TryAdd(IdentityKey(game.Platform, game.PlatformIdentifier), game);
            }

            if (PathKeys.Normalize(game.ExecutablePath) is { } exe)
            {
                byExecutable.TryAdd(exe, game);
            }

            if (PathKeys.IsSpecificGameDirectory(game.InstallDirectory) && PathKeys.Normalize(game.InstallDirectory) is { } dir)
            {
                byDirectory.TryAdd(dir, game);
            }
        }

        var resultList = results.ToList();
        var candidates = new List<ScanCandidate>();
        var updates = new List<GameInstallUpdate>();
        var seenIdentities = new HashSet<string>();
        var seenPaths = new HashSet<string>();
        var matchedGameIds = new HashSet<long>();

        // Real launcher scanners win over the generic folder scan when both find the same game.
        var ordered = resultList
            .SelectMany(r => r.Games)
            .OrderBy(d => d.Platform == GamePlatform.Manual ? 1 : 0);

        foreach (var detected in ordered)
        {
            var identity = IdentityKey(detected.Platform, detected.PlatformIdentifier);
            var exeKey = PathKeys.Normalize(detected.ExecutablePath);
            var dirKey = PathKeys.IsSpecificGameDirectory(detected.InstallDirectory) ? PathKeys.Normalize(detected.InstallDirectory) : null;

            // Duplicates inside the scan itself (same game found twice).
            if (!seenIdentities.Add(identity) ||
                (exeKey is not null && seenPaths.Contains("E:" + exeKey)) ||
                (dirKey is not null && detected.Platform == GamePlatform.Manual && seenPaths.Contains("D:" + dirKey)))
            {
                continue;
            }

            if (exeKey is not null) seenPaths.Add("E:" + exeKey);
            if (dirKey is not null) seenPaths.Add("D:" + dirKey);

            Game? existing = null;
            string? reason = null;
            if (byIdentity.TryGetValue(identity, out var byId))
            {
                (existing, reason) = (byId, "Same platform id");
            }
            else if (exeKey is not null && byExecutable.TryGetValue(exeKey, out var byExe))
            {
                (existing, reason) = (byExe, "Same executable");
            }
            else if (dirKey is not null && byDirectory.TryGetValue(dirKey, out var byDir))
            {
                (existing, reason) = (byDir, "Same install folder");
            }

            if (existing is not null)
            {
                matchedGameIds.Add(existing.Id);
                candidates.Add(new ScanCandidate(detected, ScanCandidateStatus.InLibrary, existing.Id, reason));
                if (IsSameEntry(existing, detected) && DescribeInstallChange(existing, detected) is { } change)
                {
                    updates.Add(new GameInstallUpdate(
                        existing.Id,
                        detected.IsInstalled,
                        detected.InstallDirectory ?? existing.InstallDirectory,
                        detected.ExecutablePath ?? existing.ExecutablePath,
                        change));
                }

                continue;
            }

            var status = ignoredIdentityKeys.Contains(identity) ? ScanCandidateStatus.Ignored : ScanCandidateStatus.New;
            candidates.Add(new ScanCandidate(detected, status, null, null));
        }

        if (markMissingAsUninstalled)
        {
            // Only trust "missing" for launchers that were actually read successfully.
            var scannedPlatforms = resultList
                .Where(r => r.Status == PlatformScanStatus.Ok && r.Platform != GamePlatform.Manual)
                .Select(r => r.Platform)
                .ToHashSet();

            foreach (var game in library)
            {
                if (game.Source == GameSource.Scan && game.IsInstalled && scannedPlatforms.Contains(game.Platform) &&
                    !matchedGameIds.Contains(game.Id))
                {
                    updates.Add(new GameInstallUpdate(game.Id, false, game.InstallDirectory, game.ExecutablePath, "No longer installed"));
                }
            }
        }

        return new ScanMergeResult(candidates, updates);
    }

    private static bool IsSameEntry(Game existing, DetectedGame detected) =>
        existing.Platform == detected.Platform &&
        string.Equals(existing.PlatformIdentifier, detected.PlatformIdentifier, StringComparison.OrdinalIgnoreCase);

    private static string? DescribeInstallChange(Game existing, DetectedGame detected)
    {
        if (existing.IsInstalled != detected.IsInstalled)
        {
            return detected.IsInstalled ? "Installed again" : "No longer installed";
        }

        if (detected.InstallDirectory is not null &&
            !string.Equals(PathKeys.Normalize(existing.InstallDirectory), PathKeys.Normalize(detected.InstallDirectory), StringComparison.Ordinal))
        {
            return "Install folder changed";
        }

        return null;
    }
}
