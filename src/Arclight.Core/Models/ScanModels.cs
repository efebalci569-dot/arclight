namespace Arclight.Core.Models;

public enum PlatformScanStatus
{
    /// <summary>The launcher was found and its records were read.</summary>
    Ok,

    /// <summary>The launcher does not appear to be installed on this PC.</summary>
    LauncherNotFound,

    /// <summary>The scanner ran into an error; see <see cref="PlatformScanResult.Errors"/>.</summary>
    Failed,

    /// <summary>The scanner is turned off in Settings.</summary>
    Disabled,

    /// <summary>The launcher is installed but Arclight cannot read its game list yet.</summary>
    Unsupported,
}

public sealed record PlatformScanResult(
    GamePlatform Platform,
    string ScannerName,
    PlatformSupportLevel SupportLevel,
    PlatformScanStatus Status,
    IReadOnlyList<DetectedGame> Games,
    IReadOnlyList<string> Errors)
{
    public static PlatformScanResult NotFound(GamePlatform platform, string name, PlatformSupportLevel level) =>
        new(platform, name, level, PlatformScanStatus.LauncherNotFound, [], []);

    public static PlatformScanResult Failure(GamePlatform platform, string name, PlatformSupportLevel level, string error) =>
        new(platform, name, level, PlatformScanStatus.Failed, [], [error]);
}

/// <summary>Options passed to every scanner.</summary>
public sealed record ScanOptions
{
    public IReadOnlyList<string> ExtraSteamLibraryFolders { get; init; } = [];

    public IReadOnlyList<string> CustomGameFolders { get; init; } = [];
}

public enum ScanCandidateStatus
{
    /// <summary>Not in the library yet.</summary>
    New,

    /// <summary>Already in the library; nothing to do.</summary>
    InLibrary,

    /// <summary>The user removed it earlier and asked not to see it again.</summary>
    Ignored,
}

/// <summary>A detected game classified against the current library.</summary>
public sealed record ScanCandidate(DetectedGame Detected, ScanCandidateStatus Status, long? ExistingGameId, string? MatchReason);

/// <summary>A change to an existing library entry discovered by a scan (reinstalled, moved, uninstalled).</summary>
public sealed record GameInstallUpdate(long GameId, bool IsInstalled, string? InstallDirectory, string? ExecutablePath, string Reason);
