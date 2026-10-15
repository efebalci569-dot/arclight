using Arclight.Core.Models;

namespace Arclight.Core.Launching;

/// <summary>Builds the documented or de-facto standard launcher URIs for each platform.</summary>
public static class LauncherUris
{
    public static string SteamRun(string appId) => $"steam://rungameid/{appId}";

    public static string SteamInstall(string appId) => $"steam://install/{appId}";

    public const string SteamLibrary = "steam://open/games";

    /// <summary>Epic needs namespace, catalog item id and app name joined by URL-encoded colons.</summary>
    public static string EpicRun(string catalogNamespace, string catalogItemId, string appName) =>
        $"com.epicgames.launcher://apps/{catalogNamespace}%3A{catalogItemId}%3A{appName}?action=launch&silent=true";

    public static string EpicInstall(string catalogNamespace, string catalogItemId, string appName) =>
        $"com.epicgames.launcher://apps/{catalogNamespace}%3A{catalogItemId}%3A{appName}?action=install";

    public const string EpicLibrary = "com.epicgames.launcher://library";

    public static string UbisoftRun(string installId) => $"uplay://launch/{installId}/0";

    public const string UbisoftLibrary = "uplay://";

    public static string GogOpen(string gameId) => $"goggalaxy://openGameView/{gameId}";

    public static string XboxAppsFolder(string packageFamilyName, string appId) =>
        $"{LaunchValidator.AppsFolderPrefix}{packageFamilyName}!{appId}";

    /// <summary>A link that opens the launcher itself, used for "Open launcher" when a game is not installed.</summary>
    public static string? LibraryUri(GamePlatform platform) => platform switch
    {
        GamePlatform.Steam => SteamLibrary,
        GamePlatform.Epic => EpicLibrary,
        GamePlatform.Ubisoft => UbisoftLibrary,
        _ => null,
    };

    /// <summary>Parses the Epic triple back out of a stored platform identifier ("namespace:item:app").</summary>
    public static bool TryParseEpicIdentifier(string? identifier, out string catalogNamespace, out string catalogItemId, out string appName)
    {
        catalogNamespace = catalogItemId = appName = string.Empty;
        var parts = identifier?.Split(':');
        if (parts is not { Length: 3 } || parts.Any(string.IsNullOrWhiteSpace))
        {
            return false;
        }

        (catalogNamespace, catalogItemId, appName) = (parts[0], parts[1], parts[2]);
        return true;
    }
}
