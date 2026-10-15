using System.Text;
using System.Text.Json;
using Arclight.Core.Models;

namespace Arclight.Platforms.Epic;

/// <summary>
/// The Epic Games Launcher's local catalog cache (<c>Catalog\catcache.bin</c> next to the manifests):
/// base64-encoded JSON with the official cover and background links for every owned item. Read-only;
/// an unreadable or missing file simply means no artwork.
/// </summary>
public static class EpicCatalog
{
    private static readonly string[] CoverTypes = ["DieselGameBoxTall", "OfferImageTall"];
    private static readonly string[] BackgroundTypes = ["DieselGameBox", "OfferImageWide"];

    /// <summary>Artwork per catalog item id.</summary>
    public static IReadOnlyDictionary<string, IReadOnlyList<ArtworkCandidate>> Load(string path)
    {
        try
        {
            if (!File.Exists(path) || new FileInfo(path).Length > 64 * 1024 * 1024)
            {
                return Empty;
            }

            var bytes = Convert.FromBase64String(File.ReadAllText(path).Trim());
            return Parse(Encoding.UTF8.GetString(bytes));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or JsonException)
        {
            return Empty;
        }
    }

    internal static IReadOnlyDictionary<string, IReadOnlyList<ArtworkCandidate>> Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            return Empty;
        }

        var result = new Dictionary<string, IReadOnlyList<ArtworkCandidate>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("id", out var id) || id.GetString() is not { Length: > 0 } itemId ||
                !item.TryGetProperty("keyImages", out var images) || images.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var byType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var image in images.EnumerateArray())
            {
                if (image.TryGetProperty("type", out var type) && type.GetString() is { } kind &&
                    image.TryGetProperty("url", out var url) && ToEpicHttps(url.GetString()) is { } link)
                {
                    byType.TryAdd(kind, link);
                }
            }

            var artwork = new List<ArtworkCandidate>();
            artwork.AddRange(CoverTypes.Where(byType.ContainsKey).Select(t => new ArtworkCandidate(ArtworkKind.Cover, byType[t])));
            artwork.AddRange(BackgroundTypes.Where(byType.ContainsKey).Select(t => new ArtworkCandidate(ArtworkKind.Background, byType[t])));
            if (artwork.Count > 0)
            {
                result.TryAdd(itemId, artwork);
            }
        }

        return result;
    }

    /// <summary>Only Epic's own image servers; links may contain spaces, so they are escaped here.</summary>
    private static string? ToEpicHttps(string? url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var parsed) &&
        parsed.Scheme == Uri.UriSchemeHttps &&
        (parsed.Host.Equals("epicgames.com", StringComparison.OrdinalIgnoreCase) ||
         parsed.Host.EndsWith(".epicgames.com", StringComparison.OrdinalIgnoreCase))
            ? parsed.AbsoluteUri
            : null;

    private static IReadOnlyDictionary<string, IReadOnlyList<ArtworkCandidate>> Empty { get; } =
        new Dictionary<string, IReadOnlyList<ArtworkCandidate>>();
}
