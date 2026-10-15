using System.Text.Json;
using Arclight.Core.Models;

namespace Arclight.Core.Artwork;

/// <summary>
/// The Microsoft Store's public product catalog, looked up by the exact package family name the
/// Xbox scanner reads from the installed package. Gives the game's own poster and hero art.
/// </summary>
public static class MicrosoftStore
{
    private const string ImageHost = "store-images.s-microsoft.com";

    public static string LookupUrl(string packageFamilyName) =>
        "https://displaycatalog.mp.microsoft.com/v7.0/products/lookup?alternateId=PackageFamilyName&value=" +
        Uri.EscapeDataString(packageFamilyName) + "&market=US&languages=en-US&fieldsTemplate=details";

    /// <summary>Cover and background candidates from a lookup response (empty when the store does not know the package).</summary>
    public static IReadOnlyList<ArtworkCandidate> ReadArtwork(JsonElement lookupResult)
    {
        if (!lookupResult.TryGetProperty("Products", out var products) || products.ValueKind != JsonValueKind.Array ||
            products.GetArrayLength() == 0 ||
            !products[0].TryGetProperty("LocalizedProperties", out var localized) || localized.ValueKind != JsonValueKind.Array ||
            localized.GetArrayLength() == 0 ||
            !localized[0].TryGetProperty("Images", out var images) || images.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var byPurpose = new Dictionary<string, (string Url, long Width)>(StringComparer.OrdinalIgnoreCase);
        foreach (var image in images.EnumerateArray())
        {
            var purpose = image.TryGetProperty("ImagePurpose", out var p) ? p.GetString() : null;
            var uri = image.TryGetProperty("Uri", out var u) ? u.GetString() : null;
            var width = image.TryGetProperty("Width", out var w) && w.TryGetInt64(out var value) ? value : 0;
            if (purpose is null || ToHttps(uri) is not { } url)
            {
                continue;
            }

            if (!byPurpose.TryGetValue(purpose, out var current) || width > current.Width)
            {
                byPurpose[purpose] = (url, width);
            }
        }

        var candidates = new List<ArtworkCandidate>();
        void Add(ArtworkKind kind, string purpose, string? resize = null)
        {
            if (byPurpose.TryGetValue(purpose, out var image))
            {
                candidates.Add(new ArtworkCandidate(kind, image.Url + resize));
            }
        }

        // Posters are 2:3 and up to 1440x2160; the image server scales them down on request.
        Add(ArtworkKind.Cover, "Poster", "?w=720");
        Add(ArtworkKind.Cover, "BoxArt", "?w=720");
        Add(ArtworkKind.Background, "SuperHeroArt", "?w=1920");
        Add(ArtworkKind.Background, "TitledHeroArt", "?w=1920");
        return candidates;
    }

    /// <summary>Accepts only the store's own image server; the catalog returns protocol-relative links.</summary>
    private static string? ToHttps(string? uri)
    {
        if (string.IsNullOrWhiteSpace(uri))
        {
            return null;
        }

        var absolute = uri.StartsWith("//", StringComparison.Ordinal) ? "https:" + uri : uri;
        return Uri.TryCreate(absolute, UriKind.Absolute, out var parsed) &&
               parsed.Scheme == Uri.UriSchemeHttps &&
               parsed.Host.Equals(ImageHost, StringComparison.OrdinalIgnoreCase) &&
               string.IsNullOrEmpty(parsed.Query)
            ? parsed.GetLeftPart(UriPartial.Path)
            : null;
    }
}
