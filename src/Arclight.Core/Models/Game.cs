namespace Arclight.Core.Models;

public enum LaunchMethod
{
    /// <summary>The game cannot be started from Arclight (for example it is not installed).</summary>
    None = 0,

    /// <summary>Start <see cref="Game.ExecutablePath"/> directly.</summary>
    Executable = 1,

    /// <summary>Open a launcher URI such as <c>steam://rungameid/730</c>.</summary>
    LauncherUri = 2,

    /// <summary>Run a user supplied program (<see cref="Game.LaunchTarget"/>) with arguments.</summary>
    CustomCommand = 3,
}

public enum GameSource
{
    Manual = 0,
    Scan = 1,
}

/// <summary>A game in the user's library. Every field except the name is optional.</summary>
public sealed class Game
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public GamePlatform Platform { get; set; } = GamePlatform.Manual;

    /// <summary>Platform specific id (Steam app id, Epic app name, Xbox package name, ...).</summary>
    public string? PlatformIdentifier { get; set; }

    public LaunchMethod LaunchMethod { get; set; } = LaunchMethod.Executable;

    /// <summary>Launcher URI for <see cref="LaunchMethod.LauncherUri"/>, program path for <see cref="LaunchMethod.CustomCommand"/>.</summary>
    public string? LaunchTarget { get; set; }

    public string? LaunchArguments { get; set; }

    public string? WorkingDirectory { get; set; }

    public string? ExecutablePath { get; set; }

    public string? InstallDirectory { get; set; }

    public string? CoverImagePath { get; set; }

    public string? BackgroundImagePath { get; set; }

    /// <summary>Small square artwork (exe icon, store logo). Used to decorate placeholders.</summary>
    public string? IconImagePath { get; set; }

    public string? Genre { get; set; }

    public int? ReleaseYear { get; set; }

    public string? Developer { get; set; }

    public string? Publisher { get; set; }

    public string? Description { get; set; }

    public bool IsInstalled { get; set; } = true;

    public bool IsFavorite { get; set; }

    /// <summary>Whether the optional background watcher may track this game.</summary>
    public bool IsTrackingEnabled { get; set; } = true;

    public DateTimeOffset DateAdded { get; set; }

    public DateTimeOffset? LastPlayed { get; set; }

    /// <summary>Sum of verified play sessions. Never estimated or imported.</summary>
    public long TotalPlaytimeSeconds { get; set; }

    public int SessionCount { get; set; }

    /// <summary>Personal rating from 1 to 5 stars.</summary>
    public int? PersonalRating { get; set; }

    public string? Notes { get; set; }

    public DateTimeOffset? NotesUpdatedAt { get; set; }

    public GameSource Source { get; set; } = GameSource.Manual;

    public List<string> Tags { get; set; } = [];

    public bool HasBeenPlayed => TotalPlaytimeSeconds > 0 || LastPlayed is not null;

    public Game Clone()
    {
        var copy = (Game)MemberwiseClone();
        copy.Tags = [.. Tags];
        return copy;
    }
}
