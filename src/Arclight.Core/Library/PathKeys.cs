namespace Arclight.Core.Library;

/// <summary>Helpers for comparing Windows paths reliably.</summary>
public static class PathKeys
{
    /// <summary>Returns a canonical, case-folded key for a path, or null when the path is empty or invalid.</summary>
    public static string? Normalize(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        try
        {
            var full = Path.GetFullPath(path.Trim().Trim('"').Replace('/', '\\'));
            return full.TrimEnd('\\').ToUpperInvariant();
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }
    }

    /// <summary>True when <paramref name="path"/> is <paramref name="directory"/> or somewhere below it.</summary>
    public static bool IsUnder(string? path, string? directory)
    {
        var p = Normalize(path);
        var d = Normalize(directory);
        if (p is null || d is null)
        {
            return false;
        }

        return p.Length == d.Length
            ? string.Equals(p, d, StringComparison.Ordinal)
            : p.StartsWith(d + "\\", StringComparison.Ordinal);
    }

    /// <summary>
    /// A directory is specific enough to identify a game when it is at least two levels deep
    /// and not a well-known shared folder. This keeps the tracker from treating every program
    /// under "C:\Program Files" as the same game.
    /// </summary>
    public static bool IsSpecificGameDirectory(string? directory)
    {
        var key = Normalize(directory);
        if (key is null)
        {
            return false;
        }

        var segments = key.Split('\\', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length < 2)
        {
            return false;
        }

        // Nothing inside the Windows folder is a game folder: System32 alone runs dozens of processes.
        if (segments[1] == "WINDOWS" || (WindowsDirectory is { } windows && (key == windows || key.StartsWith(windows + "\\", StringComparison.Ordinal))))
        {
            return false;
        }

        var last = segments[^1];
        string[] shared =
        [
            "PROGRAM FILES", "PROGRAM FILES (X86)", "WINDOWS", "USERS", "PROGRAMDATA", "APPDATA", "LOCAL", "ROAMING",
            "COMMON", "STEAMAPPS", "STEAM", "STEAMLIBRARY", "EPIC GAMES", "GAMES", "XBOXGAMES", "GOG GAMES", "GOG GALAXY",
            "UBISOFT", "UBISOFT GAME LAUNCHER", "EA GAMES", "DESKTOP", "DOWNLOADS", "DOCUMENTS", "WINDOWSAPPS",
        ];
        return !shared.Contains(last, StringComparer.Ordinal);
    }

    private static readonly string? WindowsDirectory = Normalize(Environment.GetFolderPath(Environment.SpecialFolder.Windows));
}
