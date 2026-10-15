namespace Arclight.Core.Launching;

public enum LaunchError
{
    None,
    NotInstalled,
    InstallDirectoryMissing,
    ExecutableNotFound,
    LauncherNotInstalled,
    LauncherFailed,
    AccessDenied,
    ElevationRequired,
    ElevationCancelled,
    InvalidConfiguration,
    Unknown,
}

public sealed record LaunchResult(bool Success, LaunchError Error, string Title, string Message, int? ProcessId = null)
{
    public static LaunchResult Started(int? processId = null) =>
        new(true, LaunchError.None, "Launching", "The game is starting.", processId);

    public static LaunchResult Fail(LaunchError error, string title, string message) =>
        new(false, error, title, message);
}
