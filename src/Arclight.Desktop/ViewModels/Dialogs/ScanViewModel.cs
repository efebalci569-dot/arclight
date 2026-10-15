using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Data;
using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Arclight.Desktop.ViewModels.Dialogs;

public enum ScanTab { New, InLibrary, Hidden }

public sealed partial class ScanPlatformItem : ObservableObject
{
    public required string Id { get; init; }

    /// <summary>The scanner's own name (matches <see cref="PlatformScanResult.ScannerName"/>).</summary>
    public required string ScannerName { get; init; }

    public string Name => Loc.ScannerName(Id, ScannerName);

    public required PlatformSupportLevel SupportLevel { get; init; }

    public string SupportText => SupportLevel switch
    {
        PlatformSupportLevel.Experimental => Loc.Get("Support.Experimental"),
        PlatformSupportLevel.ManualOnly => Loc.Get("Support.ManualOnly"),
        _ => string.Empty,
    };

    public bool HasSupportText => SupportText.Length > 0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(Glyph), nameof(StateBrushKey))]
    private PlatformScanResult? _result;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText), nameof(Glyph), nameof(StateBrushKey))]
    private bool _isRunning;

    public string StatusText => IsRunning ? Loc.Get("Shell.Scanning") : Result is null ? Loc.Get("Scan.Status.Waiting") : Result.Status switch
    {
        PlatformScanStatus.Ok => Loc.Count("Count.Game", Result.Games.Count) + (Result.Errors.Count > 0 ? " · " + Loc.Format("Scan.Status.Skipped", Result.Errors.Count) : string.Empty),
        PlatformScanStatus.LauncherNotFound => Loc.Get("Game.NotInstalled"),
        PlatformScanStatus.Disabled => Loc.Get(Id == "folders" ? "Scan.Status.NoFolders" : "Scan.Status.TurnedOff"),
        PlatformScanStatus.Unsupported => Loc.Get("Scan.Status.AddManually"),
        _ => Loc.Get("Scan.Status.Failed"),
    };

    public string Glyph => IsRunning ? string.Empty : Result?.Status switch
    {
        PlatformScanStatus.Ok => "",
        PlatformScanStatus.Failed => "",
        PlatformScanStatus.Unsupported => "",
        _ => "",
    };

    public string StateBrushKey => Result?.Status switch
    {
        PlatformScanStatus.Ok => "Brush.Success",
        PlatformScanStatus.Failed => "Brush.Danger",
        PlatformScanStatus.Unsupported => "Brush.Warning",
        _ => "Brush.Text.Muted",
    };
}

public sealed partial class ScanCandidateItem : ObservableObject
{
    public ScanCandidateItem(ScanCandidate candidate)
    {
        Detected = candidate.Detected;
        _status = candidate.Status;
        MatchReason = candidate.MatchReason;
        _isSelected = candidate.Status == ScanCandidateStatus.New && !candidate.Detected.LikelyNotAGame;
    }

    public DetectedGame Detected { get; }

    public string Name => Detected.Name;

    public string PlatformName => Loc.PlatformName(Detected.Platform);

    public string? Location => Detected.InstallDirectory ?? Detected.ExecutablePath;

    public string? Note => Detected.LikelyNotAGame
        ? Loc.Get("Scan.Note.NotAGame")
        : !Detected.IsInstalled ? Loc.Get("Scan.Note.NotFullyInstalled") : SuggestedExecutableNote(Detected);

    public bool HasNote => Note is not null;

    public bool IsWarning => Detected.LikelyNotAGame || !Detected.IsInstalled;

    public string? MatchReason { get; }

    public string? MatchText => MatchReason switch
    {
        null => null,
        "Same platform id" => Loc.Get("Scan.Match.PlatformId"),
        "Same executable" => Loc.Get("Scan.Match.Executable"),
        "Same install folder" => Loc.Get("Scan.Match.Folder"),
        _ => Loc.Format("Scan.Match.Other", MatchReason),
    };

    private static string? SuggestedExecutableNote(DetectedGame game) =>
        game is { Platform: GamePlatform.Manual, InstallDirectory: { } folder, ExecutablePath: { } exe }
            ? Loc.Format("Scan.Note.SuggestedExe", Path.GetRelativePath(folder, exe))
            : game.Note;

    [ObservableProperty]
    private ScanCandidateStatus _status;

    [ObservableProperty]
    private bool _isSelected;
}

/// <summary>The scan window: per-platform progress, what was found, and what to add.</summary>
public sealed partial class ScanViewModel : DialogViewModel
{
    private readonly ScanService _scans;
    private readonly LibraryService _library;
    private readonly NotificationService _notifications;
    private readonly ICollectionView _view;

    public ScanViewModel(ScanService scans, LibraryService library, NotificationService notifications)
    {
        _scans = scans;
        _library = library;
        _notifications = notifications;
        foreach (var scanner in scans.Scanners)
        {
            PlatformItems.Add(new ScanPlatformItem { Id = scanner.Id, ScannerName = scanner.DisplayName, SupportLevel = scanner.SupportLevel });
        }

        _view = CollectionViewSource.GetDefaultView(Candidates);
        _view.Filter = item => item is ScanCandidateItem c && c.Status == ToStatus(SelectedTab);
    }

    public override double DialogWidth => 940;

    public override bool CanDismiss => !IsScanning;

    public ObservableCollection<ScanPlatformItem> PlatformItems { get; } = [];

    public ObservableCollection<ScanCandidateItem> Candidates { get; } = [];

    public ICollectionView CandidatesView => _view;

    public ObservableCollection<string> Errors { get; } = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanDismiss), nameof(HeaderText))]
    [NotifyCanExecuteChangedFor(nameof(RescanCommand), nameof(AddSelectedCommand))]
    private bool _isScanning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNewTab), nameof(IsInLibraryTab), nameof(IsHiddenTab), nameof(EmptyText), nameof(IsCurrentTabEmpty))]
    private ScanTab _selectedTab = ScanTab.New;

    [ObservableProperty]
    private int _newCount;

    [ObservableProperty]
    private int _inLibraryCount;

    [ObservableProperty]
    private int _hiddenCount;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AddSelectedCommand), nameof(HideSelectedCommand))]
    private int _selectedCount;

    [ObservableProperty]
    private bool _hasRun;

    public int FoundCount => NewCount + InLibraryCount + HiddenCount;

    public bool HasErrors => Errors.Count > 0;

    public bool IsCurrentTabEmpty => SelectedTab switch
    {
        ScanTab.InLibrary => InLibraryCount == 0,
        ScanTab.Hidden => HiddenCount == 0,
        _ => NewCount == 0,
    };

    public bool IsNewTab
    {
        get => SelectedTab == ScanTab.New;
        set { if (value) SelectedTab = ScanTab.New; }
    }

    public bool IsInLibraryTab
    {
        get => SelectedTab == ScanTab.InLibrary;
        set { if (value) SelectedTab = ScanTab.InLibrary; }
    }

    public bool IsHiddenTab
    {
        get => SelectedTab == ScanTab.Hidden;
        set { if (value) SelectedTab = ScanTab.Hidden; }
    }

    public string HeaderText => IsScanning
        ? Loc.Get("Scan.Header.Reading")
        : HasRun ? Loc.Format("Scan.Header.Summary", Loc.Count("Count.Game", FoundCount), NewCount, InLibraryCount) : Loc.Get("Scan.Header.Ready");

    public string EmptyText => SelectedTab switch
    {
        ScanTab.New => HasRun ? Loc.Get("Scan.Empty.New") : Loc.Get("Shell.Scanning"),
        ScanTab.InLibrary => Loc.Get("Scan.Empty.InLibrary"),
        _ => Loc.Get("Scan.Empty.Hidden"),
    };

    partial void OnSelectedTabChanged(ScanTab value) => _view.Refresh();

    public async Task StartScanAsync()
    {
        if (IsScanning)
        {
            return;
        }

        IsScanning = true;
        Candidates.Clear();
        Errors.Clear();
        foreach (var item in PlatformItems)
        {
            item.Result = null;
            item.IsRunning = false;
        }

        _scans.Progress += OnProgress;
        try
        {
            var report = await _scans.ScanAsync();
            ShowReport(report);
            if (report.AutoAdded.Count > 0)
            {
                _notifications.Success(Loc.Format("Scan.Toast.AutoAdded.Title", Loc.Count("Count.NewGame", report.AutoAdded.Count)), Loc.Get("Scan.Toast.AutoAdded.Message"));
            }
        }
        catch (Exception ex)
        {
            Errors.Add(ex.Message);
            OnPropertyChanged(nameof(HasErrors));
        }
        finally
        {
            _scans.Progress -= OnProgress;
            IsScanning = false;
            OnPropertyChanged(nameof(HeaderText));
        }
    }

    public void ShowReport(ScanReport report)
    {
        foreach (var result in report.Platforms)
        {
            if (PlatformItems.FirstOrDefault(p => p.ScannerName == result.ScannerName) is { } item)
            {
                item.IsRunning = false;
                item.Result = result;
            }
        }

        Candidates.Clear();
        foreach (var candidate in report.Candidates.OrderBy(c => c.Detected.LikelyNotAGame).ThenBy(c => c.Detected.Name, StringComparer.CurrentCultureIgnoreCase))
        {
            var item = new ScanCandidateItem(candidate);
            item.PropertyChanged += OnCandidateChanged;
            Candidates.Add(item);
        }

        Errors.Clear();
        foreach (var error in report.Platforms.SelectMany(p => p.Status == PlatformScanStatus.Unsupported
                     ? [Loc.Format("Scan.Error.Unsupported", p.ScannerName)]
                     : p.Errors.Select(e => $"{LocalizedScannerName(p.ScannerName)}: {e}")))
        {
            Errors.Add(error);
        }

        HasRun = true;
        OnPropertyChanged(nameof(HasErrors));
        UpdateCounts();
        SelectedTab = NewCount > 0 ? ScanTab.New : SelectedTab;
        _view.Refresh();
    }

    [RelayCommand(CanExecute = nameof(CanRescan))]
    private Task RescanAsync() => StartScanAsync();

    private bool CanRescan() => !IsScanning;

    [RelayCommand]
    private void SelectAll()
    {
        foreach (var item in VisibleItems())
        {
            item.IsSelected = true;
        }
    }

    [RelayCommand]
    private void SelectNone()
    {
        foreach (var item in VisibleItems())
        {
            item.IsSelected = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanAddSelected))]
    private async Task AddSelectedAsync()
    {
        var selected = Candidates.Where(c => c.IsSelected && c.Status is ScanCandidateStatus.New or ScanCandidateStatus.Ignored).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        try
        {
            foreach (var hidden in selected.Where(c => c.Status == ScanCandidateStatus.Ignored))
            {
                await _scans.UnignoreAsync(hidden.Detected);
            }

            var added = await _scans.AddAsync(selected.Select(c => c.Detected).ToList());
            foreach (var item in selected)
            {
                item.IsSelected = false;
                item.Status = ScanCandidateStatus.InLibrary;
            }

            UpdateCounts();
            _view.Refresh();
            _notifications.Success(Loc.Format("Scan.Toast.Added.Title", Loc.Count("Count.Game", added.Count)), Loc.Get("Scan.Toast.Added.Message"));
        }
        catch (Exception ex)
        {
            _notifications.Error(Loc.Get("Scan.Toast.AddFailed"), ex.Message);
        }
    }

    private bool CanAddSelected() => !IsScanning && SelectedCount > 0 && SelectedTab != ScanTab.InLibrary;

    [RelayCommand(CanExecute = nameof(CanHideSelected))]
    private async Task HideSelectedAsync()
    {
        var selected = Candidates.Where(c => c.IsSelected && c.Status == ScanCandidateStatus.New).ToList();
        foreach (var item in selected)
        {
            await _scans.IgnoreAsync(item.Detected);
            item.IsSelected = false;
            item.Status = ScanCandidateStatus.Ignored;
        }

        UpdateCounts();
        _view.Refresh();
    }

    private bool CanHideSelected() => SelectedCount > 0 && SelectedTab == ScanTab.New;

    [RelayCommand]
    private void Done() => Close(null);

    private void OnProgress(object? sender, ScanProgressEventArgs e) =>
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            if (PlatformItems.FirstOrDefault(p => p.Id == e.ScannerId) is { } item)
            {
                item.IsRunning = e.Result is null;
                item.Result = e.Result;
            }
        });

    private void OnCandidateChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ScanCandidateItem.IsSelected))
        {
            SelectedCount = VisibleItems().Count(c => c.IsSelected);
        }
    }

    private IEnumerable<ScanCandidateItem> VisibleItems() => Candidates.Where(c => c.Status == ToStatus(SelectedTab));

    private void UpdateCounts()
    {
        NewCount = Candidates.Count(c => c.Status == ScanCandidateStatus.New);
        InLibraryCount = Candidates.Count(c => c.Status == ScanCandidateStatus.InLibrary);
        HiddenCount = Candidates.Count(c => c.Status == ScanCandidateStatus.Ignored);
        SelectedCount = VisibleItems().Count(c => c.IsSelected);
        OnPropertyChanged(nameof(FoundCount));
        OnPropertyChanged(nameof(HeaderText));
        OnPropertyChanged(nameof(EmptyText));
        OnPropertyChanged(nameof(IsCurrentTabEmpty));
    }

    private string LocalizedScannerName(string scannerName) =>
        PlatformItems.FirstOrDefault(p => p.ScannerName == scannerName)?.Name ?? scannerName;

    private static ScanCandidateStatus ToStatus(ScanTab tab) => tab switch
    {
        ScanTab.InLibrary => ScanCandidateStatus.InLibrary,
        ScanTab.Hidden => ScanCandidateStatus.Ignored,
        _ => ScanCandidateStatus.New,
    };
}
