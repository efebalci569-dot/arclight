using Arclight.Core.Interfaces;
using Arclight.Core.Launching;
using Arclight.Core.Models;
using Arclight.Core.Tracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arclight.Core.Services;

/// <summary>What the UI should offer instead of "Play" for a game that cannot start right now.</summary>
public enum PrimaryAction
{
    Play,
    Install,
    OpenLauncher,
    LocateGame,
}

/// <summary>Coordinates launching a game and telling the tracker about it.</summary>
public sealed class PlayService(IGameLauncher launcher, TrackingService tracking, ILogger<PlayService>? logger = null)
{
    private readonly ILogger _logger = logger ?? NullLogger<PlayService>.Instance;

    public static PrimaryAction GetPrimaryAction(Game game)
    {
        if (game.IsInstalled && game.LaunchMethod != LaunchMethod.None)
        {
            return PrimaryAction.Play;
        }

        if (!game.IsInstalled && game.Platform is GamePlatform.Steam or GamePlatform.Epic)
        {
            return PrimaryAction.Install;
        }

        return LauncherUris.LibraryUri(game.Platform) is not null ? PrimaryAction.OpenLauncher : PrimaryAction.LocateGame;
    }

    public async Task<LaunchResult> PlayAsync(Game game, LaunchOptions? options = null)
    {
        var result = await launcher.LaunchAsync(game, options ?? new LaunchOptions());
        if (result.Success)
        {
            var tracked = tracking.NotifyLaunched(game);
            _logger.LogInformation("Launched {Name} via {Method} (tracked: {Tracked})", game.Name, game.LaunchMethod, tracked);
        }
        else
        {
            _logger.LogWarning("Launch of {Name} failed: {Error} {Message}", game.Name, result.Error, result.Message);
        }

        return result;
    }

    public Task<LaunchResult> OpenLauncherAsync(Game game, LauncherAction action) => launcher.OpenInLauncherAsync(game, action);
}
