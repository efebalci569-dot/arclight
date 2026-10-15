using System.Globalization;
using System.Text;
using System.Text.Json;
using Arclight.Core.Models;

namespace Arclight.Core.Artwork;

/// <summary>
/// Steam's public artwork: the CDN paths for an app id, and a strict name search in the Steam
/// store used for games whose own launcher keeps no artwork.
/// </summary>
public static class SteamStore
{
    public const string Cdn = "https://shared.steamstatic.com/store_item_assets/steam/apps";

    // Store names often differ from launcher names only by an edition or platform suffix.
    private static readonly string[] EditionSuffixes =
    [
        "game of the year edition", "goty edition", "standard edition", "deluxe edition", "ultimate edition",
        "complete edition", "gold edition", "windows 10 edition", "windows edition", "pc edition",
        "for windows 10", "for windows", "windows 10", "windows", "goty", "pc",
    ];

    public static IReadOnlyList<ArtworkCandidate> CdnArtwork(string appId) =>
    [
        new(ArtworkKind.Cover, $"{Cdn}/{appId}/library_600x900.jpg"),
        new(ArtworkKind.Background, $"{Cdn}/{appId}/library_hero.jpg"),
        new(ArtworkKind.Background, $"{Cdn}/{appId}/header.jpg"),
    ];

    public static string SearchUrl(string name) =>
        "https://store.steampowered.com/api/storesearch/?term=" + Uri.EscapeDataString(name) + "&l=english&cc=US";

    /// <summary>
    /// The app id from a store search whose name is the same game: equal after ignoring case, accents,
    /// symbols and punctuation, or — only when nothing is equal — equal once an edition suffix is removed.
    /// Soundtracks, DLC and sequels never match.
    /// </summary>
    public static string? FindMatch(string name, JsonElement searchResult)
    {
        if (!searchResult.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        var wanted = NormalizeTitle(name);
        if (wanted.Length == 0)
        {
            return null;
        }

        var wantedBase = StripEdition(wanted);
        string? closeMatch = null;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("type", out var type) || type.GetString() != "app" ||
                !item.TryGetProperty("id", out var idElement) || !idElement.TryGetInt64(out var id) ||
                !item.TryGetProperty("name", out var nameElement) || nameElement.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var title = NormalizeTitle(nameElement.GetString()!);
            if (title == wanted)
            {
                return id.ToString(CultureInfo.InvariantCulture);
            }

            if (closeMatch is null && wantedBase.Length > 0 && StripEdition(title) == wantedBase)
            {
                closeMatch = id.ToString(CultureInfo.InvariantCulture);
            }
        }

        return closeMatch;
    }

    /// <summary>Lower-case letters and digits separated by single spaces ("Assassin’s Creed® II" → "assassins creed ii").</summary>
    public static string NormalizeTitle(string title)
    {
        var decomposed = title.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(char.ToLowerInvariant(c));
            }
            else if (c == '&')
            {
                builder.Append(" and ");
            }
            else if (c is '\'' or '’' or '´' || CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark)
            {
                // Apostrophes and accents disappear: "Assassin's" == "Assassins", "Pokémon" == "Pokemon".
            }
            else
            {
                builder.Append(' ');
            }
        }

        return string.Join(' ', builder.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static string StripEdition(string normalized)
    {
        foreach (var suffix in EditionSuffixes)
        {
            if (normalized.EndsWith(" " + suffix, StringComparison.Ordinal))
            {
                return normalized[..^(suffix.Length + 1)];
            }
        }

        return normalized;
    }
}
