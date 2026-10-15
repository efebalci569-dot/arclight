namespace Arclight.Core.Models;

public enum StartPage { Home, Library, Collections, RecentlyPlayed, Favorites, Statistics }

public enum AppTheme { Dark, OledBlack }

public enum LibraryViewMode { Grid, CompactGrid, List }

public enum LibrarySort
{
    NameAscending,
    NameDescending,
    RecentlyAdded,
    RecentlyPlayed,
    MostPlayed,
    LeastPlayed,
    ReleaseYear,
    InstallationStatus,
}

public enum CardSize { Small, Medium, Large }

public enum GridDensity { Comfortable, Compact }

/// <summary>
/// All user preferences. Stored as one JSON document in the settings table so new fields
/// pick up their defaults automatically.
/// </summary>
public sealed record AppSettings
{
    // General
    public string DisplayName { get; init; } = "Player";
    public StartPage StartPage { get; init; } = StartPage.Home;
    public AppTheme Theme { get; init; } = AppTheme.Dark;
    public string Language { get; init; } = "auto";
    public bool LaunchAtWindowsStartup { get; init; }
    public bool StartMinimized { get; init; }
    public bool CloseToTray { get; init; }
    public bool CheckForUpdatesOnStartup { get; init; }

    // Library
    public bool ScanOnStartup { get; init; } = true;
    public bool AutoAddNewGames { get; init; }
    public bool MarkMissingGamesAsUninstalled { get; init; } = true;
    public bool DownloadMissingArtwork { get; init; } = true;
    public IReadOnlyList<string> DisabledScanners { get; init; } = [];
    public IReadOnlyList<string> ExtraSteamLibraryFolders { get; init; } = [];
    public IReadOnlyList<string> CustomGameFolders { get; init; } = [];
    public LibraryViewMode LibraryViewMode { get; init; } = LibraryViewMode.Grid;
    public LibrarySort LibrarySort { get; init; } = LibrarySort.NameAscending;

    // Tracking
    public bool PlaytimeTrackingEnabled { get; init; } = true;
    public bool BackgroundWatcherEnabled { get; init; }
    public IReadOnlyList<string> ExcludedProcesses { get; init; } = [];

    // Appearance
    public string AccentColor { get; init; } = "Violet";
    public CardSize CardSize { get; init; } = CardSize.Medium;
    public GridDensity GridDensity { get; init; } = GridDensity.Comfortable;
    public bool ReduceAnimations { get; init; }

    // Home
    public long? FeaturedGameId { get; init; }

    // Window
    public double? WindowWidth { get; init; }
    public double? WindowHeight { get; init; }
    public bool WindowMaximized { get; init; }

    // Internal
    public bool FirstRunCompleted { get; init; }
}
