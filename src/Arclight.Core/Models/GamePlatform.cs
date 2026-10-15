namespace Arclight.Core.Models;

/// <summary>The store or launcher a game belongs to.</summary>
public enum GamePlatform
{
    Manual = 0,
    Steam = 1,
    Epic = 2,
    Xbox = 3,
    Gog = 4,
    Ubisoft = 5,
    EA = 6,
    Other = 7,
}

/// <summary>How far Arclight's automatic detection goes for a platform.</summary>
public enum PlatformSupportLevel
{
    /// <summary>Detection reads the launcher's own install records and is covered by tests.</summary>
    Supported,

    /// <summary>Detection works for common setups but depends on undocumented launcher data.</summary>
    Experimental,

    /// <summary>No automatic detection yet; games must be added manually.</summary>
    ManualOnly,
}

public static class PlatformInfo
{
    private static readonly Dictionary<GamePlatform, (string Code, string Name)> Map = new()
    {
        [GamePlatform.Manual] = ("manual", "PC"),
        [GamePlatform.Steam] = ("steam", "Steam"),
        [GamePlatform.Epic] = ("epic", "Epic Games"),
        [GamePlatform.Xbox] = ("xbox", "Xbox"),
        [GamePlatform.Gog] = ("gog", "GOG"),
        [GamePlatform.Ubisoft] = ("ubisoft", "Ubisoft Connect"),
        [GamePlatform.EA] = ("ea", "EA app"),
        [GamePlatform.Other] = ("other", "Other"),
    };

    public static IReadOnlyList<GamePlatform> All { get; } = Enum.GetValues<GamePlatform>();

    public static string Code(GamePlatform platform) => Map[platform].Code;

    public static string DisplayName(GamePlatform platform) => Map[platform].Name;

    public static GamePlatform Parse(string? code)
    {
        if (!string.IsNullOrWhiteSpace(code))
        {
            foreach (var (platform, info) in Map)
            {
                if (string.Equals(info.Code, code, StringComparison.OrdinalIgnoreCase))
                {
                    return platform;
                }
            }
        }

        return GamePlatform.Other;
    }

    /// <summary>Platforms whose games are usually started through their launcher rather than an exe.</summary>
    public static bool UsesLauncher(GamePlatform platform) =>
        platform is GamePlatform.Steam or GamePlatform.Epic or GamePlatform.Xbox or GamePlatform.Ubisoft or GamePlatform.EA;
}
