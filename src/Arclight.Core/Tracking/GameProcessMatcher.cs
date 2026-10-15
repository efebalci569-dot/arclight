using System.Text.RegularExpressions;
using Arclight.Core.Interfaces;
using Arclight.Core.Library;

namespace Arclight.Core.Tracking;

/// <summary>What the tracker knows about a game in order to recognise its process.</summary>
public sealed record TrackTarget(long GameId, string? ExecutablePath, string? InstallDirectory);

/// <summary>
/// Decides whether a game is running from a process snapshot. A process counts when it is the
/// game's executable, or when it runs from the game's install folder and is not a helper
/// (launcher, crash reporter, anti-cheat, installer). Launchers such as Steam itself never
/// count because they do not live inside the game folder.
/// </summary>
public sealed class GameProcessMatcher
{
    /// <summary>File name patterns for helper processes that must not add playtime.</summary>
    public static IReadOnlyList<string> DefaultExcludedPatterns { get; } =
    [
        "*crashhandler*", "*crashreport*", "*crashpad*", "*crash_reporter*", "*bugreport*", "*bugsplat*",
        "unins*", "*setup*", "*install*", "*redist*", "vc_redist*", "dxsetup*", "dotnetfx*", "*prereq*",
        "*launcher*", "*updater*", "*patcher*", "*bootstrap*", "gamelaunchhelper*",
        "*anticheat*", "easyanticheat*", "*battleye*", "beservice*", "*eac_launcher*",
        "*webhelper*", "cefprocess*", "*cefsharp*", "qtwebengineprocess*", "*overlay*",
        "*uninstall*", "*benchmark*", "*config*", "*settings*",
    ];

    private readonly Regex[] _excluded;

    public GameProcessMatcher(IEnumerable<string>? userExcludedPatterns = null)
    {
        _excluded = DefaultExcludedPatterns
            .Concat(userExcludedPatterns ?? [])
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(WildcardToRegex)
            .ToArray();
        UserPatterns = userExcludedPatterns?.Where(p => !string.IsNullOrWhiteSpace(p)).Select(WildcardToRegex).ToArray() ?? [];
    }

    private Regex[] UserPatterns { get; }

    /// <summary>Returns the ids of targets that have at least one qualifying process running.</summary>
    public IReadOnlySet<long> FindRunning(IReadOnlyList<RunningProcess> processes, IEnumerable<TrackTarget> targets)
    {
        var running = new HashSet<long>();
        var prepared = processes
            .Where(p => !string.IsNullOrEmpty(p.ImagePath))
            .Select(p => (Process: p, PathKey: PathKeys.Normalize(p.ImagePath), FileName: Path.GetFileName(p.ImagePath!)))
            .Where(p => p.PathKey is not null)
            .ToList();

        foreach (var target in targets)
        {
            var exeKey = PathKeys.Normalize(target.ExecutablePath);
            var dirKey = PathKeys.IsSpecificGameDirectory(target.InstallDirectory) ? PathKeys.Normalize(target.InstallDirectory) : null;
            if (exeKey is null && dirKey is null)
            {
                continue;
            }

            foreach (var (_, pathKey, fileName) in prepared)
            {
                if (IsMatch(pathKey!, fileName, exeKey, dirKey))
                {
                    running.Add(target.GameId);
                    break;
                }
            }
        }

        return running;
    }

    private bool IsMatch(string pathKey, string fileName, string? exeKey, string? dirKey)
    {
        // The configured executable always counts unless the user excluded it explicitly.
        if (exeKey is not null && string.Equals(pathKey, exeKey, StringComparison.Ordinal))
        {
            return !UserPatterns.Any(r => r.IsMatch(fileName));
        }

        if (dirKey is null || !pathKey.StartsWith(dirKey + "\\", StringComparison.Ordinal))
        {
            return false;
        }

        if (!fileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !_excluded.Any(r => r.IsMatch(fileName));
    }

    private static Regex WildcardToRegex(string pattern)
    {
        var trimmed = pattern.Trim();
        var escaped = Regex.Escape(trimmed).Replace(@"\*", ".*", StringComparison.Ordinal).Replace(@"\?", ".", StringComparison.Ordinal);

        // A bare name like "helper.exe" or "helper" should match exactly that file.
        if (!trimmed.Contains('*') && !trimmed.Contains('?') && !trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            escaped += @"(\.exe)?";
        }

        return new Regex("^" + escaped + "$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    }
}
