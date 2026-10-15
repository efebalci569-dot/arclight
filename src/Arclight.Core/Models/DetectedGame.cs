namespace Arclight.Core.Models;

public enum ArtworkKind
{
    Cover = 0,
    Background = 1,
    Logo = 2,
    Icon = 3,
}

/// <summary>A place artwork for a detected game may be taken from: a local file or an https URL.</summary>
public sealed record ArtworkCandidate(ArtworkKind Kind, string Source)
{
    public bool IsRemote => Source.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
}

/// <summary>A game found by a platform scanner, before the user decides to add it.</summary>
public sealed record DetectedGame
{
    public required GamePlatform Platform { get; init; }

    public required string PlatformIdentifier { get; init; }

    public required string Name { get; init; }

    public string? InstallDirectory { get; init; }

    public string? ExecutablePath { get; init; }

    public LaunchMethod LaunchMethod { get; init; } = LaunchMethod.Executable;

    public string? LaunchTarget { get; init; }

    public string? LaunchArguments { get; init; }

    public bool IsInstalled { get; init; } = true;

    /// <summary>Set for tools, redistributables and launchers that scanners cannot rule out with certainty.</summary>
    public bool LikelyNotAGame { get; init; }

    /// <summary>Extra context shown in the scan window (for example "found by folder scan").</summary>
    public string? Note { get; init; }

    public IReadOnlyList<ArtworkCandidate> Artwork { get; init; } = [];

    public Game ToGame(DateTimeOffset now) => new()
    {
        Name = Name,
        Platform = Platform,
        PlatformIdentifier = PlatformIdentifier,
        LaunchMethod = LaunchMethod,
        LaunchTarget = LaunchTarget,
        LaunchArguments = LaunchArguments,
        ExecutablePath = ExecutablePath,
        InstallDirectory = InstallDirectory,
        IsInstalled = IsInstalled,
        DateAdded = now,
        Source = GameSource.Scan,
    };
}
