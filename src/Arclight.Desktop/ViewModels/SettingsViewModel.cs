using System.Collections.ObjectModel;
using Arclight.Core.Formatting;
using Arclight.Core.Library;
using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Core.Tracking;
using Arclight.Desktop.Controls;
using Arclight.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arclight.Desktop.ViewModels;

public enum SettingsSection { General, Library, Tracking, Appearance, Privacy, About }

public sealed partial class ScannerToggle(string id, string name, string support, bool enabled, Action<string, bool> changed) : ObservableObject
{
    public string Id { get; } = id;

    public string Name { get; } = name;

    public string Support { get; } = support;

    public bool HasSupport => Support.Length > 0;

    [ObservableProperty]
    private bool _isEnabled = enabled;

    partial void OnIsEnabledChanged(bool value) => changed(Id, value);
}

public sealed partial class TrackedGameRow(GameItemViewModel item, bool canTrack, Func<long, bool, Task> changed) : ObservableObject
{
    public GameItemViewModel Item { get; } = item;

    public bool CanTrack { get; } = canTrack;

    public string Detail => CanTrack ? Item.PlatformName : Loc.Get("Settings.Tracked.Unknown");

    [ObservableProperty]
    private bool _isTracked = item.Game.IsTrackingEnabled;

    partial void OnIsTrackedChanged(bool value) => _ = changed(Item.Id, value);
}

public sealed record HiddenGameRow(string IdentityKey, string Name);

public sealed partial class SettingsViewModel : PageViewModel
{
    private readonly LibraryService _library;
    private readonly GameItemStore _items;
    private readonly ScanService _scans;
    private readonly ArtworkService _artwork;
    private readonly DialogService _dialogs;
    private readonly NotificationService _notifications;
    private readonly UpdateChecker _updates;
    private readonly DataExportService _export;
    private readonly AppPaths _paths;
    private readonly GameActions _actions;
    private bool _loading;

    public SettingsViewModel(
        LibraryService library,
        GameItemStore items,
        ScanService scans,
        ArtworkService artwork,
        DialogService dialogs,
        NotificationService notifications,
        UpdateChecker updates,
        DataExportService export,
        AppPaths paths,
        GameActions actions)
    {
        _library = library;
        _items = items;
        _scans = scans;
        _artwork = artwork;
        _dialogs = dialogs;
        _notifications = notifications;
        _updates = updates;
        _export = export;
        _paths = paths;
        _actions = actions;
        Loc.LanguageChanged += (_, _) =>
        {
            Load();
            OnPropertyChanged(nameof(StorageMode));
        };
    }

    public override PageKey Key => PageKey.Settings;

    public static IReadOnlyList<Choice<StartPage>> StartPages { get; } =
    [
        new(StartPage.Home, "Shell.Home"), new(StartPage.Library, "Shell.MyLibrary"), new(StartPage.Collections, "Common.Collections"),
        new(StartPage.RecentlyPlayed, "Common.RecentlyPlayed"), new(StartPage.Favorites, "Common.Favorites"), new(StartPage.Statistics, "Common.Statistics"),
    ];

    public static IReadOnlyList<Choice<AppTheme>> Themes { get; } = [new(AppTheme.Dark, "Settings.Theme.Dark"), new(AppTheme.OledBlack, "Settings.Theme.Oled")];

    public static IReadOnlyList<Choice<string>> Languages { get; } = [new(Loc.Automatic, "Language.Auto"), new("en", "Language.English"), new("tr", "Language.Turkish")];

    public static IReadOnlyList<Choice<LibraryViewMode>> ViewModes { get; } =
        [new(LibraryViewMode.Grid, "Library.Grid"), new(LibraryViewMode.CompactGrid, "Library.CompactGrid"), new(LibraryViewMode.List, "Library.List")];

    public static IReadOnlyList<Choice<CardSize>> CardSizes { get; } = [new(CardSize.Small, "Settings.Size.Small"), new(CardSize.Medium, "Settings.Size.Medium"), new(CardSize.Large, "Settings.Size.Large")];

    public static IReadOnlyList<Choice<GridDensity>> Densities { get; } = [new(GridDensity.Comfortable, "Settings.Density.Comfortable"), new(GridDensity.Compact, "Settings.Density.Compact")];

    public IReadOnlyList<SortOption> Sorts => LibraryViewModel.SortOptions;

    public IReadOnlyList<AccentOption> Accents => ThemeService.Accents;

    public ObservableCollection<ScannerToggle> Scanners { get; } = [];

    public ObservableCollection<string> ExtraSteamFolders { get; } = [];

    public ObservableCollection<string> GameFolders { get; } = [];

    public ObservableCollection<string> ExcludedProcesses { get; } = [];

    public ObservableCollection<TrackedGameRow> TrackedGames { get; } = [];

    public ObservableCollection<HiddenGameRow> HiddenGames { get; } = [];

    public IReadOnlyList<string> BuiltInExclusions => GameProcessMatcher.DefaultExcludedPatterns;

    public string Version => AppInfo.DisplayVersion;

    public string RepositoryUrl => AppInfo.RepositoryUrl;

    public string DataFolder => _paths.Root;

    public string StorageMode => Loc.Get(_paths.IsPortable ? "Settings.Storage.Portable" : "Settings.Storage.Profile");

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsGeneral), nameof(IsLibrary), nameof(IsTracking), nameof(IsAppearance), nameof(IsPrivacy), nameof(IsAbout))]
    private SettingsSection _section = SettingsSection.General;

    public bool IsGeneral { get => Section == SettingsSection.General; set { if (value) Section = SettingsSection.General; } }
    public bool IsLibrary { get => Section == SettingsSection.Library; set { if (value) Section = SettingsSection.Library; } }
    public bool IsTracking { get => Section == SettingsSection.Tracking; set { if (value) Section = SettingsSection.Tracking; } }
    public bool IsAppearance { get => Section == SettingsSection.Appearance; set { if (value) Section = SettingsSection.Appearance; } }
    public bool IsPrivacy { get => Section == SettingsSection.Privacy; set { if (value) Section = SettingsSection.Privacy; } }
    public bool IsAbout { get => Section == SettingsSection.About; set { if (value) Section = SettingsSection.About; } }

    // General
    [ObservableProperty] private string _displayName = "Player";
    [ObservableProperty] private Choice<StartPage>? _selectedStartPage;
    [ObservableProperty] private Choice<AppTheme>? _theme;
    [ObservableProperty] private Choice<string>? _language;
    [ObservableProperty] private bool _launchAtStartup;
    [ObservableProperty] private bool _startMinimized;
    [ObservableProperty] private bool _closeToTray;
    [ObservableProperty] private bool _checkUpdatesOnStartup;

    // Library
    [ObservableProperty] private bool _scanOnStartup;
    [ObservableProperty] private bool _autoAddNewGames;
    [ObservableProperty] private bool _markMissingUninstalled;
    [ObservableProperty] private bool _downloadArtwork;
    [ObservableProperty] private Choice<LibraryViewMode>? _defaultView;
    [ObservableProperty] private SortOption? _defaultSort;

    // Tracking
    [ObservableProperty] private bool _trackingEnabled;
    [ObservableProperty] private bool _backgroundWatcher;
    [ObservableProperty] private string _newExclusion = string.Empty;
    [ObservableProperty] private string _trackedFilter = string.Empty;

    // Appearance
    [ObservableProperty] private AccentOption? _accent;
    [ObservableProperty] private Choice<CardSize>? _selectedCardSize;
    [ObservableProperty] private Choice<GridDensity>? _density;
    [ObservableProperty] private bool _reduceAnimations;
    [ObservableProperty] private string _cacheSize = "—";

    // About
    [ObservableProperty] private string _updateStatus = string.Empty;
    [ObservableProperty] private bool _isCheckingUpdates;
    [ObservableProperty] private string? _updateUrl;

    public override void OnNavigatedTo(object? parameter)
    {
        if (parameter is SettingsSection section)
        {
            Section = section;
        }

        Load();
    }

    private void Load()
    {
        _loading = true;
        var s = _library.Settings;
        DisplayName = s.DisplayName == "Player" ? string.Empty : s.DisplayName;
        SelectedStartPage = StartPages.First(p => p.Value == s.StartPage);
        Theme = Themes.First(t => t.Value == s.Theme);
        Language = Languages.FirstOrDefault(l => l.Value == s.Language) ?? Languages[0];
        LaunchAtStartup = StartupRegistration.IsEnabled();
        StartMinimized = s.StartMinimized;
        CloseToTray = s.CloseToTray;
        CheckUpdatesOnStartup = s.CheckForUpdatesOnStartup;
        ScanOnStartup = s.ScanOnStartup;
        AutoAddNewGames = s.AutoAddNewGames;
        MarkMissingUninstalled = s.MarkMissingGamesAsUninstalled;
        DownloadArtwork = s.DownloadMissingArtwork;
        DefaultView = ViewModes.First(v => v.Value == s.LibraryViewMode);
        DefaultSort = Sorts.First(o => o.Sort == s.LibrarySort);
        TrackingEnabled = s.PlaytimeTrackingEnabled;
        BackgroundWatcher = s.BackgroundWatcherEnabled;
        Accent = Accents.FirstOrDefault(a => a.Name == s.AccentColor) ?? Accents[0];
        SelectedCardSize = CardSizes.First(c => c.Value == s.CardSize);
        Density = Densities.First(d => d.Value == s.GridDensity);
        ReduceAnimations = s.ReduceAnimations;

        Scanners.Clear();
        foreach (var scanner in _scans.Scanners)
        {
            var support = scanner.SupportLevel switch
            {
                PlatformSupportLevel.Experimental => Loc.Get("Support.Experimental"),
                PlatformSupportLevel.ManualOnly => Loc.Get("Support.LauncherOnly"),
                _ => string.Empty,
            };
            Scanners.Add(new ScannerToggle(scanner.Id, Loc.ScannerName(scanner.Id, scanner.DisplayName), support,
                !s.DisabledScanners.Contains(scanner.Id, StringComparer.OrdinalIgnoreCase), OnScannerToggled));
        }

        Reset(ExtraSteamFolders, s.ExtraSteamLibraryFolders);
        Reset(GameFolders, s.CustomGameFolders);
        Reset(ExcludedProcesses, s.ExcludedProcesses);
        RefreshTrackedGames();
        _ = RefreshHiddenGamesAsync();
        _ = RefreshCacheSizeAsync();
        _loading = false;
    }

    private Task Save(Func<AppSettings, AppSettings> change) => _loading ? Task.CompletedTask : _library.UpdateSettingsAsync(change);

    partial void OnDisplayNameChanged(string value) => _ = Save(s => s with { DisplayName = string.IsNullOrWhiteSpace(value) ? "Player" : value.Trim() });
    partial void OnSelectedStartPageChanged(Choice<StartPage>? value) { if (value is not null) _ = Save(s => s with { StartPage = value.Value }); }
    partial void OnThemeChanged(Choice<AppTheme>? value) { if (value is not null) _ = Save(s => s with { Theme = value.Value }); }
    partial void OnStartMinimizedChanged(bool value) => _ = Save(s => s with { StartMinimized = value });
    partial void OnCloseToTrayChanged(bool value) => _ = Save(s => s with { CloseToTray = value });
    partial void OnCheckUpdatesOnStartupChanged(bool value) => _ = Save(s => s with { CheckForUpdatesOnStartup = value });
    partial void OnScanOnStartupChanged(bool value) => _ = Save(s => s with { ScanOnStartup = value });
    partial void OnAutoAddNewGamesChanged(bool value) => _ = Save(s => s with { AutoAddNewGames = value });
    partial void OnMarkMissingUninstalledChanged(bool value) => _ = Save(s => s with { MarkMissingGamesAsUninstalled = value });
    partial void OnDownloadArtworkChanged(bool value) => _ = Save(s => s with { DownloadMissingArtwork = value });
    partial void OnDefaultViewChanged(Choice<LibraryViewMode>? value) { if (value is not null) _ = Save(s => s with { LibraryViewMode = value.Value }); }
    partial void OnDefaultSortChanged(SortOption? value) { if (value is not null) _ = Save(s => s with { LibrarySort = value.Sort }); }
    partial void OnTrackingEnabledChanged(bool value) => _ = Save(s => s with { PlaytimeTrackingEnabled = value });
    partial void OnBackgroundWatcherChanged(bool value) => _ = Save(s => s with { BackgroundWatcherEnabled = value });
    partial void OnAccentChanged(AccentOption? value) { if (value is not null) _ = Save(s => s with { AccentColor = value.Name }); }
    partial void OnSelectedCardSizeChanged(Choice<CardSize>? value) { if (value is not null) _ = Save(s => s with { CardSize = value.Value }); }
    partial void OnDensityChanged(Choice<GridDensity>? value) { if (value is not null) _ = Save(s => s with { GridDensity = value.Value }); }
    partial void OnReduceAnimationsChanged(bool value) => _ = Save(s => s with { ReduceAnimations = value });
    partial void OnTrackedFilterChanged(string value) => RefreshTrackedGames();
    partial void OnLanguageChanged(Choice<string>? value) { if (value is not null) _ = Save(s => s with { Language = value.Value }); }

    partial void OnLaunchAtStartupChanged(bool value)
    {
        if (_loading)
        {
            return;
        }

        try
        {
            StartupRegistration.Set(value);
            _ = Save(s => s with { LaunchAtWindowsStartup = value });
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            _notifications.Error(Loc.Get("Settings.Toast.StartupFailed"), ex.Message);
        }
    }

    private void OnScannerToggled(string id, bool enabled)
    {
        if (_loading)
        {
            return;
        }

        _ = Save(s => s with
        {
            DisabledScanners = enabled
                ? s.DisabledScanners.Where(d => !d.Equals(id, StringComparison.OrdinalIgnoreCase)).ToList()
                : s.DisabledScanners.Append(id).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
        });
    }

    [RelayCommand]
    private Task AddSteamFolderAsync() => AddFolderAsync(ExtraSteamFolders, Loc.Get("Settings.Pick.SteamFolder"),
        list => s => s with { ExtraSteamLibraryFolders = list });

    [RelayCommand]
    private Task RemoveSteamFolderAsync(string folder) => RemoveFolderAsync(ExtraSteamFolders, folder, list => s => s with { ExtraSteamLibraryFolders = list });

    [RelayCommand]
    private Task AddGameFolderAsync() => AddFolderAsync(GameFolders, Loc.Get("Settings.Pick.GameFolder"),
        list => s => s with { CustomGameFolders = list });

    [RelayCommand]
    private Task RemoveGameFolderAsync(string folder) => RemoveFolderAsync(GameFolders, folder, list => s => s with { CustomGameFolders = list });

    private async Task AddFolderAsync(ObservableCollection<string> target, string title, Func<List<string>, Func<AppSettings, AppSettings>> change)
    {
        if (_dialogs.PickFolder(title) is { } folder && !target.Contains(folder, StringComparer.OrdinalIgnoreCase))
        {
            if (!PathKeys.IsSpecificGameDirectory(folder) && folder.TrimEnd('\\').Length <= 3)
            {
                _notifications.Warning(Loc.Get("Settings.Toast.FolderTooBroad.Title"), Loc.Get("Settings.Toast.FolderTooBroad.Message"));
                return;
            }

            target.Add(folder);
            await Save(change([.. target]));
        }
    }

    private async Task RemoveFolderAsync(ObservableCollection<string> target, string folder, Func<List<string>, Func<AppSettings, AppSettings>> change)
    {
        target.Remove(folder);
        await Save(change([.. target]));
    }

    [RelayCommand]
    private async Task AddExclusionAsync()
    {
        var value = NewExclusion.Trim();
        if (value.Length == 0 || value.IndexOfAny(['\\', '/', ':', '"', '<', '>', '|']) >= 0)
        {
            _notifications.Warning(Loc.Get("Settings.Toast.ProcessName.Title"), Loc.Get("Settings.Toast.ProcessName.Message"));
            return;
        }

        if (!ExcludedProcesses.Contains(value, StringComparer.OrdinalIgnoreCase))
        {
            ExcludedProcesses.Add(value);
            await Save(s => s with { ExcludedProcesses = [.. ExcludedProcesses] });
        }

        NewExclusion = string.Empty;
    }

    [RelayCommand]
    private async Task RemoveExclusionAsync(string value)
    {
        ExcludedProcesses.Remove(value);
        await Save(s => s with { ExcludedProcesses = [.. ExcludedProcesses] });
    }

    [RelayCommand]
    private void SetAllTracked(string enabled)
    {
        var value = enabled == "true";
        foreach (var row in TrackedGames.Where(r => r.CanTrack))
        {
            row.IsTracked = value;
        }
    }

    private void RefreshTrackedGames()
    {
        var terms = TextSearch.Tokenize(TrackedFilter);
        TrackedGames.Clear();
        foreach (var game in _library.Games.OrderBy(g => g.Name, NaturalStringComparer.Instance))
        {
            if (terms.Count > 0 && !terms.All(t => TextSearch.Normalize(game.Name).Contains(t, StringComparison.Ordinal)))
            {
                continue;
            }

            TrackedGames.Add(new TrackedGameRow(_items.Get(game), TrackingService.IsTrackable(game), (id, value) => _library.SetTrackingEnabledAsync(id, value)));
        }
    }

    private async Task RefreshHiddenGamesAsync()
    {
        var hidden = await _library.GetIgnoredGamesAsync();
        HiddenGames.Clear();
        foreach (var (key, name) in hidden)
        {
            HiddenGames.Add(new HiddenGameRow(key, name));
        }
    }

    [RelayCommand]
    private async Task UnhideAsync(HiddenGameRow row)
    {
        await _library.UnignoreAsync(row.IdentityKey);
        HiddenGames.Remove(row);
        _notifications.Show(NoticeKind.Info, Loc.Format("Settings.Toast.Unhidden.Title", row.Name), Loc.Get("Settings.Toast.Unhidden.Message"), keepInHistory: false);
    }

    [RelayCommand]
    private Task ScanNowAsync() => _actions.ScanAsync();

    private async Task RefreshCacheSizeAsync()
    {
        var bytes = await Task.Run(_artwork.GetCacheSizeBytes);
        CacheSize = Format.Bytes(bytes);
    }

    [RelayCommand]
    private async Task ClearImageCacheAsync()
    {
        var answer = await _dialogs.ConfirmAsync(
            Loc.Get("Settings.ClearCache.Title"),
            Loc.Get("Settings.ClearCache.Message"),
            Loc.Get("Settings.ClearCache"));
        if (!answer.Confirmed)
        {
            return;
        }

        await Task.Run(_artwork.ClearCache);
        AsyncImage.ClearMemoryCache();
        foreach (var game in _library.Games.ToList())
        {
            if (_artwork.IsCachedPath(game.CoverImagePath) || _artwork.IsCachedPath(game.BackgroundImagePath) || _artwork.IsCachedPath(game.IconImagePath))
            {
                await _library.SetArtworkAsync(game.Id,
                    _artwork.IsCachedPath(game.CoverImagePath) ? null : game.CoverImagePath,
                    _artwork.IsCachedPath(game.BackgroundImagePath) ? null : game.BackgroundImagePath,
                    _artwork.IsCachedPath(game.IconImagePath) ? null : game.IconImagePath);
            }
        }

        await RefreshCacheSizeAsync();
        _notifications.Success(Loc.Get("Settings.Toast.CacheCleared.Title"), Loc.Get("Settings.Toast.CacheCleared.Message"), Loc.Get("Settings.ScanNow"), () => _actions.ScanAsync());
    }

    [RelayCommand]
    private void OpenDataFolder() => Shell.Open(_paths.Root);

    [RelayCommand]
    private async Task ResetStatisticsAsync()
    {
        var answer = await _dialogs.ConfirmAsync(
            Loc.Get("Settings.ResetStats.Title"),
            Loc.Get("Settings.ResetStats.Message"),
            Loc.Get("Settings.ResetStatistics"),
            danger: true);
        if (answer.Confirmed)
        {
            await _library.ResetStatisticsAsync();
            _notifications.Info(Loc.Get("Settings.Toast.StatsReset.Title"), Loc.Get("Settings.Toast.StatsReset.Message"));
        }
    }

    [RelayCommand]
    private async Task ClearPlayHistoryAsync()
    {
        var answer = await _dialogs.ConfirmAsync(
            Loc.Get("Settings.ClearHistory.Title"),
            Loc.Get("Settings.ClearHistory.Message"),
            Loc.Get("Settings.ClearHistory"),
            danger: true);
        if (answer.Confirmed)
        {
            await _library.ClearPlayHistoryAsync();
            _notifications.Info(Loc.Get("Settings.Toast.HistoryCleared"));
        }
    }

    [RelayCommand]
    private async Task ClearLogsAsync()
    {
        var removed = await Task.Run(() =>
        {
            var count = 0;
            foreach (var file in Directory.Exists(_paths.Logs) ? Directory.EnumerateFiles(_paths.Logs, "*.log") : [])
            {
                // Today's log is in use and only cleared on the next start.
                if (Path.GetFileName(file).Contains(DateTime.Now.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal))
                {
                    continue;
                }

                try
                {
                    File.Delete(file);
                    count++;
                }
                catch (IOException)
                {
                }
            }

            return count;
        });
        _notifications.Info(Loc.Get("Settings.Toast.LogsCleared.Title"), Loc.Format("Settings.Toast.LogsCleared.Message", Loc.Count("Count.LogFile", removed)));
    }

    [RelayCommand]
    private async Task ExportDataAsync()
    {
        var path = _dialogs.PickSaveFile(Loc.Get("Settings.Export.DialogTitle"), $"arclight-export-{DateTime.Now:yyyyMMdd}.json", Loc.Get("Settings.Export.Filter"));
        if (path is null)
        {
            return;
        }

        try
        {
            await _export.ExportAsync(path, AppInfo.DisplayVersion);
            _notifications.Success(Loc.Get("Settings.Toast.Exported"), path, Loc.Get("Settings.Toast.ShowFile"), () =>
            {
                Shell.RevealFile(path);
                return Task.CompletedTask;
            });
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _notifications.Error(Loc.Get("Settings.Toast.ExportFailed"), ex.Message);
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckingUpdates = true;
        UpdateStatus = Loc.Get("Settings.Update.Checking");
        var result = await _updates.CheckAsync();
        UpdateStatus = result.Message;
        UpdateUrl = result.ReleaseUrl;
        IsCheckingUpdates = false;
    }

    [RelayCommand]
    private void OpenUpdatePage()
    {
        if (UpdateUrl is not null)
        {
            Shell.Open(UpdateUrl);
        }
    }

    [RelayCommand]
    private void OpenRepository() => Shell.Open(AppInfo.RepositoryUrl);

    [RelayCommand]
    private Task HowItWorksAsync() => _actions.ShowAboutDetectionAsync();

    private static void Reset(ObservableCollection<string> target, IEnumerable<string> values)
    {
        target.Clear();
        foreach (var value in values)
        {
            target.Add(value);
        }
    }
}
