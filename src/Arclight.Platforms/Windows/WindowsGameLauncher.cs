using System.ComponentModel;
using System.Diagnostics;
using Arclight.Core.Interfaces;
using Arclight.Core.Launching;
using Arclight.Core.Models;
using Microsoft.Win32;

namespace Arclight.Platforms.Windows;

/// <summary>
/// Starts games on Windows. Executables are started directly (no shell, no command interpreter),
/// so a game that needs administrator rights fails with a clear error instead of Arclight elevating
/// silently; the user can then explicitly choose "Run as administrator". Launcher URIs are only
/// opened for allow-listed schemes whose handler is actually registered.
/// </summary>
public sealed class WindowsGameLauncher(IRegistryReader registry) : IGameLauncher
{
    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorElevationRequired = 740;
    private const int ErrorCancelled = 1223;

    public Task<LaunchResult> LaunchAsync(Game game, LaunchOptions options, CancellationToken cancellationToken = default)
    {
        var validation = LaunchValidator.ValidateGame(game);
        if (!validation.IsValid)
        {
            return Task.FromResult(LaunchResult.Fail(LaunchError.InvalidConfiguration, "Launch settings need attention", validation.Error!));
        }

        var result = game.LaunchMethod switch
        {
            LaunchMethod.Executable => StartExecutable(game, game.ExecutablePath!, options),
            LaunchMethod.CustomCommand => StartExecutable(game, game.LaunchTarget!, options),
            LaunchMethod.LauncherUri => OpenUri(game, game.LaunchTarget!),
            _ => LaunchResult.Fail(LaunchError.NotInstalled, $"{game.Name} can't be started",
                "Arclight doesn't know how to start this game. Edit it and choose an executable or launcher link."),
        };

        return Task.FromResult(result);
    }

    public Task<LaunchResult> OpenInLauncherAsync(Game game, LauncherAction action, CancellationToken cancellationToken = default)
    {
        string? uri = null;
        if (action == LauncherAction.Install)
        {
            if (game.Platform == GamePlatform.Steam && game.PlatformIdentifier is { } appId)
            {
                uri = LauncherUris.SteamInstall(appId);
            }
            else if (game.Platform == GamePlatform.Epic &&
                     LauncherUris.TryParseEpicIdentifier(game.PlatformIdentifier, out var ns, out var item, out var app))
            {
                uri = LauncherUris.EpicInstall(ns, item, app);
            }
        }

        uri ??= LauncherUris.LibraryUri(game.Platform);
        if (uri is null)
        {
            return Task.FromResult(LaunchResult.Fail(LaunchError.LauncherNotInstalled, "No launcher link",
                $"Arclight can't open a launcher for {PlatformInfo.DisplayName(game.Platform)} games."));
        }

        return Task.FromResult(OpenUri(game, uri, checkInstalled: false));
    }

    public bool IsLauncherAvailable(GamePlatform platform) => platform switch
    {
        GamePlatform.Steam => IsSchemeRegistered("steam"),
        GamePlatform.Epic => IsSchemeRegistered("com.epicgames.launcher"),
        GamePlatform.Ubisoft => IsSchemeRegistered("uplay"),
        GamePlatform.Gog => IsSchemeRegistered("goggalaxy"),
        _ => true,
    };

    internal static LaunchResult DescribeMissingExecutable(Game game, string path)
    {
        if (!string.IsNullOrWhiteSpace(game.InstallDirectory) && !Directory.Exists(game.InstallDirectory))
        {
            return LaunchResult.Fail(LaunchError.InstallDirectoryMissing, $"{game.Name} seems to be uninstalled",
                $"The game folder \"{game.InstallDirectory}\" no longer exists. If you moved the game, use Locate Game to pick its new executable.");
        }

        return LaunchResult.Fail(LaunchError.ExecutableNotFound, $"{game.Name}'s executable is missing",
            $"\"{path}\" could not be found. The game may have been updated, moved or uninstalled. Use Locate Game to choose the right file.");
    }

    private LaunchResult StartExecutable(Game game, string path, LaunchOptions options)
    {
        if (!File.Exists(path))
        {
            return DescribeMissingExecutable(game, path);
        }

        var extension = Path.GetExtension(path);
        var needsShell = extension.Equals(".lnk", StringComparison.OrdinalIgnoreCase) || extension.Equals(".url", StringComparison.OrdinalIgnoreCase);
        var workingDirectory = !string.IsNullOrWhiteSpace(game.WorkingDirectory) && Directory.Exists(game.WorkingDirectory)
            ? game.WorkingDirectory
            : Path.GetDirectoryName(path)!;

        var startInfo = new ProcessStartInfo(path)
        {
            Arguments = game.LaunchArguments ?? string.Empty,
            WorkingDirectory = workingDirectory,
            UseShellExecute = needsShell || options.RunAsAdministrator,
        };

        if (options.RunAsAdministrator)
        {
            startInfo.Verb = "runas";
        }

        return Start(game, startInfo);
    }

    private LaunchResult OpenUri(Game game, string uri, bool checkInstalled = true)
    {
        if (checkInstalled && !game.IsInstalled)
        {
            return LaunchResult.Fail(LaunchError.NotInstalled, $"{game.Name} isn't installed",
                $"Install it from {PlatformInfo.DisplayName(game.Platform)} first, then scan again.");
        }

        var scheme = LaunchValidator.GetUriScheme(uri);
        if (scheme == "shell")
        {
            // Packaged (Xbox / Microsoft Store) games are activated through the Apps folder.
            var explorer = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "explorer.exe");
            return Start(game, new ProcessStartInfo(explorer, uri) { UseShellExecute = false });
        }

        if (scheme is null || !IsSchemeRegistered(scheme))
        {
            return LaunchResult.Fail(LaunchError.LauncherNotInstalled, $"{PlatformInfo.DisplayName(game.Platform)} isn't available",
                $"No app on this PC handles \"{scheme}://\" links. Install or repair {PlatformInfo.DisplayName(game.Platform)} and try again.");
        }

        return Start(game, new ProcessStartInfo(uri) { UseShellExecute = true });
    }

    private bool IsSchemeRegistered(string scheme) =>
        registry.ValueExists(RegistryHive.ClassesRoot, scheme, "URL Protocol");

    private static LaunchResult Start(Game game, ProcessStartInfo startInfo)
    {
        try
        {
            using var process = Process.Start(startInfo);
            return LaunchResult.Started(TryGetId(process));
        }
        catch (Win32Exception ex)
        {
            return ex.NativeErrorCode switch
            {
                ErrorElevationRequired => LaunchResult.Fail(LaunchError.ElevationRequired, $"{game.Name} needs administrator rights",
                    "This game asks to run as administrator. Arclight never elevates on its own — choose Run as administrator if you trust it."),
                ErrorCancelled => LaunchResult.Fail(LaunchError.ElevationCancelled, "Launch cancelled",
                    "The administrator prompt was declined, so the game was not started."),
                ErrorAccessDenied => LaunchResult.Fail(LaunchError.AccessDenied, $"Access to {game.Name} was denied",
                    "Windows blocked access to the game's files. Check the folder permissions or your antivirus quarantine."),
                ErrorFileNotFound or ErrorPathNotFound => DescribeMissingExecutable(game, startInfo.FileName),
                _ => LaunchResult.Fail(LaunchError.LauncherFailed, $"{game.Name} could not be started", ex.Message),
            };
        }
        catch (InvalidOperationException ex)
        {
            return LaunchResult.Fail(LaunchError.Unknown, $"{game.Name} could not be started", ex.Message);
        }
    }

    private static int? TryGetId(Process? process)
    {
        try
        {
            return process?.Id;
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }
}
