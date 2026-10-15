using System.Collections.ObjectModel;
using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Core.Tracking;
using Arclight.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;

namespace Arclight.Desktop.ViewModels;

/// <summary>Owns navigation, the top bar (search, notifications, status) and the overlay layers.</summary>
public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly LibraryService _library;
    private readonly GameItemStore _items;
    private readonly GameActions _actions;
    private readonly ScanService _scans;
    private readonly TrackingService _tracking;
    private readonly Navigator _navigator;
    private readonly Stack<(PageViewModel Page, object? Parameter)> _history = new();
    private readonly Dictionary<PageKey, PageViewModel> _pages = [];
    private object? _currentParameter;
    private bool _navigating;

    public ShellViewModel(
        IServiceProvider services,
        LibraryService library,
        GameItemStore items,
        GameActions actions,
        ScanService scans,
        TrackingService tracking,
        Navigator navigator,
        SearchState search,
        NotificationService notifications,
        DialogService dialogs)
    {
        _services = services;
        _library = library;
        _items = items;
        _actions = actions;
        _scans = scans;
        _tracking = tracking;
        _navigator = navigator;
        Search = search;
        Notifications = notifications;
        Dialogs = dialogs;

        _navigator.Requested += (_, request) => Navigate(request.Page, request.Parameter);
        _navigator.BackRequested += (_, _) => GoBack();
        _items.RunningChanged += (_, _) => UpdateStatus();
        _library.Changed += (_, e) =>
        {
            if (e.Kind == LibraryChangeKind.SettingsChanged)
            {
                UpdateStatus();
            }
        };
        _scans.Progress += (_, _) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() => IsScanning = true);
        _scans.Completed += (_, _) => System.Windows.Application.Current.Dispatcher.BeginInvoke(() => IsScanning = false);
        Search.PropertyChanged += (_, _) => OnSearchChanged();
        Loc.LanguageChanged += (_, _) => UpdateStatus();
    }

    public SearchState Search { get; }

    public NotificationService Notifications { get; }

    public DialogService Dialogs { get; }

    public string Version => "v" + AppInfo.DisplayVersion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanGoBack))]
    private PageViewModel? _currentPage;

    [ObservableProperty]
    private PageKey _selectedNav = PageKey.Home;

    [ObservableProperty]
    private bool _isNotificationsOpen;

    [ObservableProperty]
    private bool _isScanning;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _statusDetail = string.Empty;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private bool _isTrackingOn;

    [ObservableProperty]
    private string _profileInitial = "P";

    public bool CanGoBack => _history.Count > 0 && CurrentPage?.Key is PageKey.GameDetail or PageKey.CollectionDetail;

    public ObservableCollection<GameItemViewModel> NowPlaying { get; } = [];

    public void Start(StartPage startPage)
    {
        UpdateStatus();
        Navigate(startPage switch
        {
            StartPage.Library => PageKey.Library,
            StartPage.Collections => PageKey.Collections,
            StartPage.RecentlyPlayed => PageKey.RecentlyPlayed,
            StartPage.Favorites => PageKey.Favorites,
            StartPage.Statistics => PageKey.Statistics,
            _ => PageKey.Home,
        });
    }

    partial void OnSelectedNavChanged(PageKey value)
    {
        if (!_navigating && CurrentPage?.NavKey != value)
        {
            Navigate(value);
        }
    }

    partial void OnIsNotificationsOpenChanged(bool value)
    {
        if (value)
        {
            Notifications.MarkAllRead();
        }
    }

    public void Navigate(PageKey key, object? parameter = null)
    {
        var page = GetPage(key);
        if (CurrentPage is not null)
        {
            if (ReferenceEquals(CurrentPage, page) && Equals(parameter, _currentParameter) && parameter is null)
            {
                page.OnNavigatedTo(parameter);
                return;
            }

            CurrentPage.OnNavigatedFrom();
            if (key is PageKey.GameDetail or PageKey.CollectionDetail)
            {
                _history.Push((CurrentPage, _currentParameter));
            }
            else
            {
                _history.Clear();
            }
        }

        // Typing in the top bar on a page without a game list jumps to the library; leaving clears it.
        if (!page.UsesSearch && key != PageKey.GameDetail && Search.Text.Length > 0)
        {
            Search.Text = string.Empty;
        }

        _currentParameter = parameter;
        page.OnNavigatedTo(parameter);
        CurrentPage = page;
        _navigating = true;
        SelectedNav = page.NavKey;
        _navigating = false;
        OnPropertyChanged(nameof(CanGoBack));
    }

    [RelayCommand]
    public void GoBack()
    {
        if (_history.Count == 0)
        {
            Navigate(PageKey.Home);
            return;
        }

        var (page, parameter) = _history.Pop();
        CurrentPage?.OnNavigatedFrom();
        _currentParameter = parameter;
        page.OnNavigatedTo(parameter);
        CurrentPage = page;
        _navigating = true;
        SelectedNav = page.NavKey;
        _navigating = false;
        OnPropertyChanged(nameof(CanGoBack));
    }

    [RelayCommand]
    private void NavigateTo(PageKey key) => Navigate(key);

    [RelayCommand]
    private Task AddGameAsync() => _actions.AddGameAsync();

    [RelayCommand]
    private Task ScanAsync() => _actions.ScanAsync();

    [RelayCommand]
    private void OpenSettings() => Navigate(PageKey.Settings);

    [RelayCommand]
    private void OpenProfile() => Navigate(PageKey.Settings, Arclight.Desktop.ViewModels.SettingsSection.General);

    [RelayCommand]
    private void ClearNotifications() => Notifications.ClearHistory();

    [RelayCommand]
    private void OpenNowPlaying(GameItemViewModel item) => _actions.OpenDetails(item.Game);

    private void OnSearchChanged()
    {
        if (Search.Text.Length > 0 && CurrentPage is { UsesSearch: false })
        {
            Navigate(PageKey.Library);
        }
    }

    private PageViewModel GetPage(PageKey key)
    {
        // Detail pages are created per visit; the main pages keep their state between visits.
        if (key is PageKey.GameDetail)
        {
            return _services.GetRequiredService<GameDetailViewModel>();
        }

        if (key is PageKey.CollectionDetail)
        {
            return ActivatorUtilities.CreateInstance<LibraryViewModel>(_services, LibraryPageMode.Collection);
        }

        if (!_pages.TryGetValue(key, out var page))
        {
            page = key switch
            {
                PageKey.Library => ActivatorUtilities.CreateInstance<LibraryViewModel>(_services, LibraryPageMode.All),
                PageKey.Favorites => ActivatorUtilities.CreateInstance<LibraryViewModel>(_services, LibraryPageMode.Favorites),
                PageKey.Collections => _services.GetRequiredService<CollectionsViewModel>(),
                PageKey.RecentlyPlayed => _services.GetRequiredService<RecentlyPlayedViewModel>(),
                PageKey.Statistics => _services.GetRequiredService<StatisticsViewModel>(),
                PageKey.Discover => _services.GetRequiredService<DiscoverViewModel>(),
                PageKey.Settings => _services.GetRequiredService<SettingsViewModel>(),
                _ => _services.GetRequiredService<HomeViewModel>(),
            };
            _pages[key] = page;
        }

        return page;
    }

    public void UpdateStatus()
    {
        var settings = _library.Settings;
        // The default name is stored as "Player"; show the initial of the name the greeting uses.
        var name = string.IsNullOrWhiteSpace(settings.DisplayName) || settings.DisplayName == "Player" ? Loc.Get("Home.DefaultName") : settings.DisplayName.Trim();
        ProfileInitial = name[..1].ToUpper(Loc.Culture);
        IsTrackingOn = settings.PlaytimeTrackingEnabled;
        HomeViewModel.Replace(NowPlaying, _items.Running);
        IsPlaying = NowPlaying.Count > 0;
        if (IsPlaying)
        {
            StatusText = NowPlaying.Count == 1 ? Loc.Format("Status.PlayingOne", NowPlaying[0].Name) : Loc.Format("Status.PlayingMany", NowPlaying.Count);
            var since = _tracking.RunningSince(NowPlaying[0].Id);
            StatusDetail = since is null ? Loc.Get("Status.TrackingPlaytime") : Loc.Format("Status.Since", since.Value.ToLocalTime().ToString("HH:mm", Loc.Culture));
        }
        else if (!settings.PlaytimeTrackingEnabled)
        {
            StatusText = Loc.Get("Status.TrackingOff");
            StatusDetail = Loc.Get("Status.TurnOnInSettings");
        }
        else
        {
            StatusText = Loc.Get(settings.BackgroundWatcherEnabled ? "Status.WatcherOn" : "Status.Ready");
            StatusDetail = Loc.Get(settings.BackgroundWatcherEnabled ? "Status.Watching" : "Status.LaunchFromArclight");
        }
    }
}
