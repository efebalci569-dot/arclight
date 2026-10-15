using System.IO;
using System.Net.Http;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Arclight.Core.Data;
using Arclight.Core.Interfaces;
using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Core.Tracking;
using Arclight.Desktop.Services;
using Arclight.Desktop.ViewModels;
using Arclight.Desktop.Views;
using Arclight.Platforms.Ea;
using Arclight.Platforms.Epic;
using Arclight.Platforms.Gog;
using Arclight.Platforms.Manual;
using Arclight.Platforms.Steam;
using Arclight.Platforms.Ubisoft;
using Arclight.Platforms.Windows;
using Arclight.Platforms.Xbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Arclight.Desktop;

public partial class App : Application
{
    private ServiceProvider? _services;
    private SingleInstance? _instance;
    private FileLoggerProvider? _logProvider;
    private ILogger? _logger;
    private ShellWindow? _window;
    private TrayService? _tray;
    private string? _languageSetting;
    private bool _exiting;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var options = StartupOptions.Parse(e.Args);

        // The Windows display language until the library (and the saved choice) has loaded.
        Loc.Apply(options.Language);
        var paths = AppPaths.Resolve(options.DataDirectory, AppContext.BaseDirectory);

        _instance = SingleInstance.TryAcquire(paths.Root, () => Dispatcher.BeginInvoke(() => _window?.BringToFront()));
        if (_instance is null && options.CaptureFile is null)
        {
            // Another Arclight is running for this data folder; it was asked to show itself.
            Shutdown();
            return;
        }

        try
        {
            paths.EnsureCreated();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show(Loc.Format("Startup.DataFolderError", paths.Root, ex.Message), "Arclight", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
            return;
        }

        _logProvider = new FileLoggerProvider(paths.Logs);
        _services = ConfigureServices(paths, _logProvider).BuildServiceProvider();
        _logger = _services.GetRequiredService<ILogger<App>>();
        _logger.LogInformation("Arclight {Version} starting (data: {Root})", AppInfo.DisplayVersion, paths.Root);
        HookUnhandledExceptions();

        if (!TryInitializeDatabase(_services.GetRequiredService<ArclightDatabase>(), paths))
        {
            Shutdown(1);
            return;
        }

        var library = _services.GetRequiredService<LibraryService>();
        await library.LoadAsync();
        if (options.SeedDemo && options.DataDirectory is not null && library.Games.Count == 0)
        {
            await DemoData.SeedAsync(library, _services.GetRequiredService<SessionRepository>(), paths);
            await library.LoadAsync();
        }

        _languageSetting = library.Settings.Language;
        Loc.Apply(options.Language ?? _languageSetting);
        await CompleteFirstRunAsync(library);

        var theme = _services.GetRequiredService<ThemeService>();
        theme.Apply(library.Settings);
        var tracking = _services.GetRequiredService<TrackingService>();
        tracking.ApplySettings(library.Settings);
        await tracking.StartAsync();
        library.Changed += OnLibraryChanged;

        var shell = _services.GetRequiredService<ShellViewModel>();
        _window = new ShellWindow(shell) { ShouldHideOnClose = () => library.Settings.CloseToTray, Language = Loc.XmlLanguage };
        _window.SetReduceMotion(library.Settings.ReduceAnimations);
        _window.Closed += (_, _) => ExitApplication();
        MainWindow = _window;
        shell.Start(library.Settings.StartPage);

        if (options.CaptureFile is not null)
        {
            await CaptureAsync(_window, shell, library, options);
            return;
        }

        RestoreWindowBounds(library.Settings);
        var startHidden = options.Minimized || library.Settings.StartMinimized;
        UpdateTray(library.Settings);
        if (startHidden && library.Settings.CloseToTray)
        {
            _tray?.Show();
        }
        else
        {
            _window.Show();
            if (startHidden)
            {
                _window.WindowState = WindowState.Minimized;
            }
        }

        _ = RunStartupTasksAsync(library);
    }

    private static ServiceCollection ConfigureServices(AppPaths paths, FileLoggerProvider logProvider)
    {
        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddProvider(logProvider);
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // Core
        services.AddSingleton(paths);
        services.AddSingleton(sp => new ArclightDatabase(paths.DatabaseFile, sp.GetRequiredService<ILogger<ArclightDatabase>>()));
        services.AddSingleton<GameRepository>();
        services.AddSingleton<CollectionRepository>();
        services.AddSingleton<SessionRepository>();
        services.AddSingleton<SettingsRepository>();
        services.AddSingleton<IClock>(SystemClock.Instance);
        services.AddSingleton(_ =>
        {
            var http = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"Arclight/{AppInfo.DisplayVersion} (+{AppInfo.RepositoryUrl})");
            return http;
        });
        services.AddSingleton<IIconExtractor, ShellIconExtractor>();
        services.AddSingleton<ArtworkService>();
        services.AddSingleton(sp => new LibraryService(
            sp.GetRequiredService<GameRepository>(),
            sp.GetRequiredService<CollectionRepository>(),
            sp.GetRequiredService<SessionRepository>(),
            sp.GetRequiredService<SettingsRepository>(),
            sp.GetRequiredService<IClock>(),
            DispatchToUi,
            sp.GetRequiredService<ILogger<LibraryService>>()));
        services.AddSingleton<DataExportService>();

        // Platforms (read-only detection, launching, process inspection)
        services.AddSingleton<IRegistryReader>(WindowsRegistryReader.Instance);
        services.AddSingleton<IPlatformScanner>(sp => new SteamScanner(sp.GetRequiredService<IRegistryReader>()));
        services.AddSingleton<IPlatformScanner>(sp => new EpicScanner(sp.GetRequiredService<IRegistryReader>()));
        services.AddSingleton<IPlatformScanner>(sp => new GogScanner(sp.GetRequiredService<IRegistryReader>()));
        services.AddSingleton<IPlatformScanner>(_ => new XboxScanner());
        services.AddSingleton<IPlatformScanner>(sp => new UbisoftScanner(sp.GetRequiredService<IRegistryReader>()));
        services.AddSingleton<IPlatformScanner>(sp => new EaAppScanner(sp.GetRequiredService<IRegistryReader>()));
        services.AddSingleton<IPlatformScanner, FolderScanner>();
        services.AddSingleton<ScanService>();
        services.AddSingleton<IProcessSnapshotProvider, WindowsProcessSnapshotProvider>();
        services.AddSingleton(sp => new TrackingService(
            sp.GetRequiredService<SessionRepository>(),
            sp.GetRequiredService<LibraryService>(),
            sp.GetRequiredService<IProcessSnapshotProvider>(),
            sp.GetRequiredService<IClock>(),
            logger: sp.GetRequiredService<ILogger<TrackingService>>()));
        services.AddSingleton<IGameLauncher>(sp => new WindowsGameLauncher(sp.GetRequiredService<IRegistryReader>()));
        services.AddSingleton<PlayService>();

        // Desktop
        services.AddSingleton<ThemeService>();
        services.AddSingleton<NotificationService>();
        services.AddSingleton<DialogService>();
        services.AddSingleton<Navigator>();
        services.AddSingleton<SearchState>();
        services.AddSingleton<UpdateChecker>();
        services.AddSingleton<GameActions>();
        services.AddSingleton<GameItemStore>();
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<HomeViewModel>();
        services.AddSingleton<CollectionsViewModel>();
        services.AddSingleton<RecentlyPlayedViewModel>();
        services.AddSingleton<StatisticsViewModel>();
        services.AddSingleton<DiscoverViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<GameDetailViewModel>();
        return services;
    }

    private static Task DispatchToUi(Action action)
    {
        var dispatcher = Current?.Dispatcher;
        if (dispatcher is null || dispatcher.CheckAccess())
        {
            action();
            return Task.CompletedTask;
        }

        return dispatcher.InvokeAsync(action).Task;
    }

    private bool TryInitializeDatabase(ArclightDatabase database, AppPaths paths)
    {
        try
        {
            database.Initialize();
            return true;
        }
        catch (DatabaseException ex)
        {
            _logger?.LogError(ex, "Database could not be opened");
            var answer = MessageBox.Show(
                Loc.Format("Startup.DatabaseError.Message", ex.Message),
                Loc.Get("Startup.DatabaseError.Title"),
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Error);

            if (answer == MessageBoxResult.Yes)
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                Directory.CreateDirectory(paths.Backups);
                var stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
                foreach (var suffix in new[] { string.Empty, "-wal", "-shm" })
                {
                    var file = database.FilePath + suffix;
                    if (File.Exists(file))
                    {
                        File.Move(file, Path.Combine(paths.Backups, $"arclight-unreadable-{stamp}.db{suffix}"));
                    }
                }

                database.Initialize();
                return true;
            }

            if (answer == MessageBoxResult.No)
            {
                Services.Shell.Open(paths.Root);
            }

            return false;
        }
    }

    /// <summary>Creates a few empty starter collections the first time Arclight runs.</summary>
    private static async Task CompleteFirstRunAsync(LibraryService library)
    {
        if (library.Settings.FirstRunCompleted)
        {
            return;
        }

        if (library.Collections.Count == 0)
        {
            // Named in the language in use on first run; they are ordinary collections after that.
            await library.CreateCollectionAsync(Loc.Get("FirstRun.Playing.Name"), Loc.Get("FirstRun.Playing.Description"));
            await library.CreateCollectionAsync(Loc.Get("FirstRun.Backlog.Name"), Loc.Get("FirstRun.Backlog.Description"));
            await library.CreateCollectionAsync(Loc.Get("FirstRun.Completed.Name"), Loc.Get("FirstRun.Completed.Description"));
        }

        await library.UpdateSettingsAsync(s => s with { FirstRunCompleted = true });
    }

    private async Task RunStartupTasksAsync(LibraryService library)
    {
        var services = _services!;
        var notifications = services.GetRequiredService<NotificationService>();
        var actions = services.GetRequiredService<GameActions>();
        try
        {
            if (library.Settings.ScanOnStartup)
            {
                var report = await services.GetRequiredService<ScanService>().ScanAsync();
                var newGames = report.Candidates.Count(c => c.Status == ScanCandidateStatus.New && !c.Detected.LikelyNotAGame);
                if (report.AutoAdded.Count > 0)
                {
                    notifications.Success(Loc.Format("Scan.Toast.AutoAdded.Title", Loc.Count("Count.NewGame", report.AutoAdded.Count)), Loc.Get("Startup.AutoAdded.Message"));
                }
                else if (newGames > 0)
                {
                    notifications.Info(Loc.Format("Startup.NewGames.Title", Loc.Count("Count.NewGame", newGames)), Loc.Get("Startup.NewGames.Message"), Loc.Get("Startup.NewGames.Action"),
                        () => actions.ScanAsync(showLastReport: true));
                }
            }

            // Covers for games whose launcher keeps none (only when "Download missing artwork" is on).
            await services.GetRequiredService<ScanService>().FillMissingArtworkOnlineAsync();

            if (library.Settings.CheckForUpdatesOnStartup)
            {
                var update = await services.GetRequiredService<UpdateChecker>().CheckAsync();
                if (update.UpdateAvailable && update.ReleaseUrl is { } url)
                {
                    notifications.Info(Loc.Get("Update.Toast.Title"), update.Message, Loc.Get("Update.Toast.Action"), () =>
                    {
                        Services.Shell.Open(url);
                        return Task.CompletedTask;
                    });
                }
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Startup tasks failed");
        }
    }

    private void OnLibraryChanged(object? sender, LibraryChangedEventArgs e)
    {
        var library = (LibraryService)sender!;
        var tracking = _services!.GetRequiredService<TrackingService>();
        switch (e.Kind)
        {
            case LibraryChangeKind.SettingsChanged:
                if (library.Settings.Language != _languageSetting)
                {
                    _languageSetting = library.Settings.Language;
                    Loc.Apply(_languageSetting);
                }

                _services!.GetRequiredService<ThemeService>().Apply(library.Settings);
                _window?.SetReduceMotion(library.Settings.ReduceAnimations);
                tracking.ApplySettings(library.Settings);
                UpdateTray(library.Settings);
                break;
            case LibraryChangeKind.GamesAdded or LibraryChangeKind.GamesRemoved or LibraryChangeKind.GamesUpdated or LibraryChangeKind.Reloaded:
                tracking.RefreshWatchedGames();
                break;
        }
    }

    private void UpdateTray(AppSettings settings)
    {
        if (settings.CloseToTray)
        {
            if (_tray is null)
            {
                _tray = new TrayService();
                _tray.OpenRequested += (_, _) => Dispatcher.BeginInvoke(() => _window?.BringToFront());
                _tray.ExitRequested += (_, _) => Dispatcher.BeginInvoke(() =>
                {
                    if (_window is not null)
                    {
                        _window.IsExiting = true;
                        _window.Close();
                    }
                });
            }

            _tray.Show();
            _tray.SetStatus(Loc.Get(settings.BackgroundWatcherEnabled ? "Tray.Status.Watcher" : "Tray.Status.Running"));
        }
        else
        {
            _tray?.Hide();
        }
    }

    private void RestoreWindowBounds(AppSettings settings)
    {
        if (_window is null)
        {
            return;
        }

        var area = SystemParameters.WorkArea;
        if (settings.WindowWidth is { } width && settings.WindowHeight is { } height && width >= _window.MinWidth && height >= _window.MinHeight)
        {
            _window.Width = Math.Min(width, area.Width);
            _window.Height = Math.Min(height, area.Height);
        }
        else
        {
            _window.Width = Math.Min(_window.Width, area.Width - 40);
            _window.Height = Math.Min(_window.Height, area.Height - 40);
        }

        if (settings.WindowMaximized)
        {
            _window.WindowState = WindowState.Maximized;
        }
    }

    private async void ExitApplication()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        try
        {
            if (_services is not null && _window is not null)
            {
                var library = _services.GetRequiredService<LibraryService>();
                var maximized = _window.WindowState == WindowState.Maximized;
                var bounds = _window.RestoreBounds;
                await library.UpdateSettingsAsync(s => s with
                {
                    WindowMaximized = maximized,
                    WindowWidth = bounds.IsEmpty ? s.WindowWidth : bounds.Width,
                    WindowHeight = bounds.IsEmpty ? s.WindowHeight : bounds.Height,
                });

                // Close running sessions at the moment the game was last seen.
                var tracking = _services.GetRequiredService<TrackingService>();
                await tracking.DisposeAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error while shutting down");
        }
        finally
        {
            _logger?.LogInformation("Arclight closed");
            _tray?.Dispose();
            if (_services is not null)
            {
                try
                {
                    await _services.DisposeAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Service shutdown problem");
                }
            }

            _logProvider?.Dispose();
            _instance?.Dispose();
            Shutdown();
        }
    }

    protected override void OnSessionEnding(SessionEndingCancelEventArgs e)
    {
        base.OnSessionEnding(e);
        if (_window is not null)
        {
            _window.IsExiting = true;
        }

        ExitApplication();
    }

    private void HookUnhandledExceptions()
    {
        DispatcherUnhandledException += (_, e) =>
        {
            _logger?.LogError(e.Exception, "Unhandled UI exception");
            if (!_exiting)
            {
                _services?.GetService<NotificationService>()?.Error(Loc.Get("Error.Unexpected"), e.Exception.Message);
            }

            e.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            _logger?.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            _logger?.LogCritical(e.ExceptionObject as Exception, "Fatal exception");
    }

    /// <summary>Renders the window into a PNG (documentation screenshots) and exits.</summary>
    private async Task CaptureAsync(ShellWindow window, ShellViewModel shell, LibraryService library, StartupOptions options)
    {
        try
        {
            window.Width = options.CaptureWidth;
            window.Height = options.CaptureHeight;
            window.WindowStartupLocation = WindowStartupLocation.Manual;
            window.Left = SystemParameters.VirtualScreenLeft - options.CaptureWidth - 200;
            window.Top = 0;
            window.ShowActivated = false;
            window.ShowInTaskbar = false;
            window.Show();

            if (options.CapturePage is { } pageName && Enum.TryParse<PageKey>(pageName, ignoreCase: true, out var page))
            {
                object? parameter = null;
                if (page == PageKey.GameDetail)
                {
                    parameter = library.Games.FirstOrDefault(g => options.CaptureGame is null || g.Name.Contains(options.CaptureGame, StringComparison.OrdinalIgnoreCase))?.Id;
                }
                else if (page == PageKey.CollectionDetail)
                {
                    parameter = library.Collections.FirstOrDefault(c => c.GameIds.Count > 0)?.Id;
                }

                shell.Navigate(page, parameter);
            }

            var actions = _services!.GetRequiredService<GameActions>();
            switch (options.CaptureDialog)
            {
                case "scan":
                    _ = actions.ScanAsync();
                    break;
                case "add":
                    _ = actions.AddGameAsync();
                    break;
            }

            if (options.CaptureSwitchLanguage is { } switchTo)
            {
                // Render once in the starting language, then switch while everything is open.
                await Task.Delay(1500);
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                Loc.Apply(switchTo);
            }

            await Task.Delay(options.CaptureDelayMs);
            window.UpdateLayout();
            await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);

            var dpi = VisualTreeHelper.GetDpi(window);
            var bitmap = new RenderTargetBitmap(
                (int)Math.Round(window.ActualWidth * dpi.DpiScaleX),
                (int)Math.Round(window.ActualHeight * dpi.DpiScaleY),
                dpi.PixelsPerInchX,
                dpi.PixelsPerInchY,
                PixelFormats.Pbgra32);
            bitmap.Render(window);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(options.CaptureFile!))!);
            await using (var stream = File.Create(options.CaptureFile!))
            {
                encoder.Save(stream);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Capture failed");
        }
        finally
        {
            window.IsExiting = true;
            window.Close();
        }
    }
}
