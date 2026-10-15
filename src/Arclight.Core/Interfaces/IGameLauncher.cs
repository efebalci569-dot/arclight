using Arclight.Core.Launching;
using Arclight.Core.Models;

namespace Arclight.Core.Interfaces;

/// <summary>Starts games. Implementations never request administrator rights on their own.</summary>
public interface IGameLauncher
{
    Task<LaunchResult> LaunchAsync(Game game, LaunchOptions options, CancellationToken cancellationToken = default);

    /// <summary>Opens the game's launcher (store page, library) when the game itself cannot be started.</summary>
    Task<LaunchResult> OpenInLauncherAsync(Game game, LauncherAction action, CancellationToken cancellationToken = default);

    bool IsLauncherAvailable(GamePlatform platform);
}

public sealed record LaunchOptions
{
    /// <summary>Only set after the user explicitly confirmed an elevation prompt for this game.</summary>
    public bool RunAsAdministrator { get; init; }
}

public enum LauncherAction
{
    OpenLibrary,
    Install,
}
