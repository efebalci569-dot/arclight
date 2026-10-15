using Arclight.Core.Interfaces;
using Arclight.Core.Launching;
using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Core.Tracking;
using Arclight.Desktop.ViewModels;
using Arclight.Desktop.ViewModels.Dialogs;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace Arclight.Desktop.Services;

/// <summary>
/// Every user action on a game lives here so cards, lists, context menus and the detail page all
/// behave the same way and report problems the same way.
/// </summary>
public sealed class GameActions(
    LibraryService library,
    PlayService play,
    ScanService scans,
    ArtworkService artwork,
    DialogService dialogs,
    NotificationService notifications,
    Navigator navigator,
    ILogger<GameActions> logger)
{
    public LibraryService Library => library;

    public Task PrimaryActionAsync(Game game) => PlayService.GetPrimaryAction(game) switch
    {
        PrimaryAction.Install => OpenLauncherAsync(game, LauncherAction.Install),
        PrimaryAction.OpenLauncher => OpenLauncherAsync(game, LauncherAction.OpenLibrary),
        PrimaryAction.LocateGame => LocateAsync(game),
        _ => PlayAsync(game),
    };

    public async Task PlayAsync(Game game, bool asAdministrator = false)
    {
        LaunchResult result;
        try
        {
            result = await play.PlayAsync(game, new LaunchOptions { RunAsAdministrator = asAdministrator });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected launch failure for {Name}", game.Name);
            notifications.Error(Loc.Format("Launch.Failed.Title", game.Name), ex.Message);
            return;
        }

        if (result.Success)
        {
            var trackable = library.Settings.PlaytimeTrackingEnabled && TrackingService.IsTrackable(game);
            if (!trackable)
            {
                await library.MarkLaunchedAsync(game.Id);
            }

            var hint = Loc.Get(!library.Settings.PlaytimeTrackingEnabled
                ? "Launch.Hint.TrackingOff"
                : trackable ? "Launch.Hint.Tracked" : "Launch.Hint.Untrackable");
            notifications.Show(NoticeKind.Info, Loc.Format("Launch.Starting", game.Name), hint, keepInHistory: false);
            return;
        }

        var (title, message) = DescribeFailure(game, result);
        switch (result.Error)
        {
            case LaunchError.ElevationRequired:
                notifications.Warning(title, message, Loc.Get("Launch.Action.RunAsAdmin"), () => ConfirmElevatedLaunchAsync(game));
                break;
            case LaunchError.ExecutableNotFound or LaunchError.InstallDirectoryMissing:
                notifications.Error(title, message, Loc.Get("Action.LocateGame"), () => LocateAsync(game));
                break;
            case LaunchError.NotInstalled when LauncherUris.LibraryUri(game.Platform) is not null:
                notifications.Warning(title, message, Loc.Get("Action.OpenLauncher"), () => OpenLauncherAsync(game, LauncherAction.OpenLibrary));
                break;
            case LaunchError.InvalidConfiguration or LaunchError.LauncherNotInstalled:
                notifications.Error(title, message, Loc.Get("Launch.Action.EditGame"), () => EditAsync(game));
                break;
            case LaunchError.ElevationCancelled:
                notifications.Info(title, message);
                break;
            default:
                notifications.Error(title, message);
                break;
        }
    }

    /// <summary>Turns a launch result into a title and message in the user's language.</summary>
    internal static (string Title, string Message) DescribeFailure(Game game, LaunchResult result)
    {
        var platform = Loc.PlatformName(game.Platform);
        var path = game.LaunchMethod == LaunchMethod.CustomCommand ? game.LaunchTarget : game.ExecutablePath;
        return result.Error switch
        {
            LaunchError.NotInstalled => (Loc.Format("Launch.NotInstalled.Title", game.Name), Loc.Format("Launch.NotInstalled.Message", platform)),
            LaunchError.InstallDirectoryMissing => (Loc.Format("Launch.FolderMissing.Title", game.Name), Loc.Format("Launch.FolderMissing.Message", game.InstallDirectory)),
            LaunchError.ExecutableNotFound => (Loc.Format("Launch.ExeMissing.Title", game.Name), Loc.Format("Launch.ExeMissing.Message", path)),
            LaunchError.LauncherNotInstalled => (Loc.Format("Launch.LauncherMissing.Title", platform),
                Loc.Format("Launch.LauncherMissing.Message", LaunchValidator.GetUriScheme(game.LaunchTarget) ?? "?", platform)),
            LaunchError.AccessDenied => (Loc.Format("Launch.AccessDenied.Title", game.Name), Loc.Get("Launch.AccessDenied.Message")),
            LaunchError.ElevationRequired => (Loc.Format("Launch.Elevation.Title", game.Name), Loc.Get("Launch.Elevation.Message")),
            LaunchError.ElevationCancelled => (Loc.Get("Launch.Cancelled.Title"), Loc.Get("Launch.Cancelled.Message")),
            LaunchError.InvalidConfiguration => (Loc.Get("Launch.Invalid.Title"), Loc.Validation(LaunchValidator.ValidateGame(game), result.Message)),
            _ => (Loc.Format("Launch.Failed.Title", game.Name), result.Message),
        };
    }

    public async Task OpenLauncherAsync(Game game, LauncherAction action)
    {
        var result = await play.OpenLauncherAsync(game, action);
        if (!result.Success)
        {
            var (title, message) = result.Error == LaunchError.LauncherNotInstalled && LauncherUris.LibraryUri(game.Platform) is null
                ? (Loc.Get("Launch.NoLauncherLink.Title"), Loc.Format("Launch.NoLauncherLink.Message", Loc.PlatformName(game.Platform)))
                : DescribeFailure(game, result);
            notifications.Error(title, message);
        }
    }

    private async Task ConfirmElevatedLaunchAsync(Game game)
    {
        var answer = await dialogs.ConfirmAsync(
            Loc.Format("Launch.ConfirmAdmin.Title", game.Name),
            Loc.Get("Launch.ConfirmAdmin.Message"),
            Loc.Get("Launch.Action.RunAsAdmin"));
        if (answer.Confirmed)
        {
            await PlayAsync(game, asAdministrator: true);
        }
    }

    public void OpenDetails(Game game) => navigator.Go(PageKey.GameDetail, game.Id);

    public Task ToggleFavoriteAsync(Game game) => library.SetFavoriteAsync(game.Id, !game.IsFavorite);

    public async Task AddGameAsync(string? executablePath = null)
    {
        var editor = new GameEditorViewModel(library, artwork, dialogs, existing: null, executablePath);
        if (await dialogs.ShowAsync<Game>(editor) is { } added)
        {
            notifications.Success(Loc.Format("Toast.GameAdded.Title", added.Name), Loc.Get("Toast.GameAdded.Message"), Loc.Get("Toast.View"), () =>
            {
                OpenDetails(added);
                return Task.CompletedTask;
            });
        }
    }

    public async Task EditAsync(Game game)
    {
        var editor = new GameEditorViewModel(library, artwork, dialogs, existing: game);
        if (await dialogs.ShowAsync<Game>(editor) is { } saved)
        {
            notifications.Show(NoticeKind.Success, Loc.Get("Toast.ChangesSaved"), saved.Name, keepInHistory: false);
        }
    }

    public async Task RemoveAsync(Game game)
    {
        var fromScan = game.Source == GameSource.Scan;
        var answer = await dialogs.ConfirmAsync(
            Loc.Format("Remove.Title", game.Name),
            Loc.Get("Remove.Message"),
            Loc.Get("Game.RemoveFromLibrary"),
            danger: true,
            optionText: fromScan ? Loc.Get("Remove.DontSuggestAgain") : null,
            optionDefault: fromScan);
        if (!answer.Confirmed)
        {
            return;
        }

        await library.RemoveGameAsync(game.Id, answer.OptionChecked);
        notifications.Info(Loc.Format("Toast.GameRemoved.Title", game.Name), Loc.Get("Toast.GameRemoved.Message"));
    }

    public void OpenInstallFolder(Game game)
    {
        if (!string.IsNullOrWhiteSpace(game.InstallDirectory) && Directory.Exists(game.InstallDirectory))
        {
            Shell.Open(game.InstallDirectory!);
        }
        else if (!string.IsNullOrWhiteSpace(game.ExecutablePath) && File.Exists(game.ExecutablePath))
        {
            Shell.RevealFile(game.ExecutablePath!);
        }
        else
        {
            notifications.Warning(Loc.Get("Toast.FolderNotFound.Title"), Loc.Get("Toast.FolderNotFound.Message"));
        }
    }

    public async Task FeatureOnHomeAsync(Game game)
    {
        await library.UpdateSettingsAsync(s => s with { FeaturedGameId = game.Id });
        notifications.Show(NoticeKind.Success, Loc.Format("Toast.Featured", game.Name), null, keepInHistory: false);
    }

    /// <summary>Lets the user point Arclight at a moved or re-installed game's executable.</summary>
    public async Task LocateAsync(Game game)
    {
        var start = Directory.Exists(game.InstallDirectory) ? game.InstallDirectory : null;
        var path = dialogs.PickExecutable(start);
        if (path is null)
        {
            return;
        }

        var validation = LaunchValidator.ValidateExecutablePath(path);
        if (!validation.IsValid)
        {
            notifications.Error(Loc.Get("Toast.FileUnusable"), Loc.Validation(validation));
            return;
        }

        var updated = game.Clone();
        updated.ExecutablePath = path;
        updated.IsInstalled = true;
        if (updated.LaunchMethod is LaunchMethod.None or LaunchMethod.Executable)
        {
            updated.LaunchMethod = LaunchMethod.Executable;
        }

        if (string.IsNullOrWhiteSpace(updated.InstallDirectory) || !Directory.Exists(updated.InstallDirectory) ||
            !Core.Library.PathKeys.IsUnder(path, updated.InstallDirectory))
        {
            updated.InstallDirectory = Path.GetDirectoryName(path);
        }

        await library.UpdateGameAsync(updated);
        if (updated.IconImagePath is null && artwork.ExtractIcon(path, path) is { } icon)
        {
            await library.SetArtworkAsync(game.Id, game.CoverImagePath, game.BackgroundImagePath, icon);
        }

        notifications.Success(Loc.Format("Toast.Located", game.Name), path);
    }

    public IReadOnlyList<CollectionOption> GetCollectionOptions(Game game) =>
        library.Collections
            .Select(c => new CollectionOption(c.Name, c.GameIds.Contains(game.Id), new AsyncRelayCommand(() => ToggleCollectionAsync(c, game))))
            .ToList();

    public async Task ToggleCollectionAsync(GameCollection collection, Game game)
    {
        if (collection.GameIds.Contains(game.Id))
        {
            await library.RemoveFromCollectionAsync(collection.Id, game.Id);
            notifications.Show(NoticeKind.Info, Loc.Format("Toast.RemovedFromCollection", collection.Name), game.Name, keepInHistory: false);
        }
        else
        {
            await library.AddToCollectionAsync(collection.Id, [game.Id]);
            notifications.Show(NoticeKind.Success, Loc.Format("Toast.AddedToCollection", collection.Name), game.Name, keepInHistory: false);
        }
    }

    public async Task AddToNewCollectionAsync(Game game)
    {
        var editor = new CollectionEditorViewModel(library, artwork, dialogs, existing: null);
        if (await dialogs.ShowAsync<GameCollection>(editor) is { } created)
        {
            await library.AddToCollectionAsync(created.Id, [game.Id]);
            notifications.Show(NoticeKind.Success, Loc.Format("Toast.AddedToCollection", created.Name), game.Name, keepInHistory: false);
        }
    }

    /// <summary>Opens the scan window and starts a scan (or shows the last report when <paramref name="showLastReport"/> is set).</summary>
    public async Task ScanAsync(bool showLastReport = false)
    {
        var dialog = new ScanViewModel(scans, library, notifications);
        if (showLastReport && scans.LastReport is { } report)
        {
            dialog.ShowReport(report);
        }
        else
        {
            _ = dialog.StartScanAsync();
        }

        await dialogs.ShowAsync(dialog);
    }

    public Task ShowAboutDetectionAsync() => dialogs.ShowAsync(new InfoDialogViewModel
    {
        Title = Loc.Get("About.Title"),
        Sections =
        [
            (Loc.Get("About.Library.Heading"), Loc.Get("About.Library.Body")),
            (Loc.Get("About.Finding.Heading"), Loc.Get("About.Finding.Body")),
            (Loc.Get("About.Launching.Heading"), Loc.Get("About.Launching.Body")),
            (Loc.Get("About.Playtime.Heading"), Loc.Get("About.Playtime.Body")),
        ],
    });
}
