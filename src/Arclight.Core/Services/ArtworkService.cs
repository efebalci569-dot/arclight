using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Arclight.Core.Artwork;
using Arclight.Core.Data;
using Arclight.Core.Interfaces;
using Arclight.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arclight.Core.Services;

public sealed record ResolvedArtwork(string? Cover, string? Background, string? Icon);

/// <summary>
/// Finds and caches artwork. Order of preference: the user's own image (never overwritten),
/// artwork the launcher already stores on disk, then the platform's public image servers when downloads
/// are allowed. Games whose launcher keeps no artwork are looked up online (<see cref="FindOnlineAsync"/>);
/// anything that cannot be matched exactly falls back to a placeholder.
/// </summary>
public sealed class ArtworkService
{
    public static IReadOnlySet<string> SupportedImageExtensions { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".png", ".jpg", ".jpeg", ".bmp", ".gif", ".ico" };

    private const long MaxImageBytes = 25 * 1024 * 1024;
    private static readonly TimeSpan FailureCooldown = TimeSpan.FromDays(7);

    private readonly AppPaths _paths;
    private readonly SettingsRepository _store;
    private readonly HttpClient _http;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _downloadSlots = new(4);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _keyLocks = new(StringComparer.Ordinal);
    private readonly IIconExtractor? _iconExtractor;

    public ArtworkService(
        AppPaths paths,
        SettingsRepository store,
        HttpClient http,
        IClock clock,
        IIconExtractor? iconExtractor = null,
        ILogger<ArtworkService>? logger = null)
    {
        _paths = paths;
        _store = store;
        _http = http;
        _clock = clock;
        _iconExtractor = iconExtractor;
        _logger = logger ?? NullLogger<ArtworkService>.Instance;
    }

    /// <summary>Extracts a large icon from an executable (or .ico) into a PNG file in the cache.</summary>
    public string? ExtractIcon(string executablePath, string cacheName)
    {
        if (_iconExtractor is null || !File.Exists(executablePath))
        {
            return null;
        }

        Directory.CreateDirectory(_paths.ArtworkCache);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cacheName.ToUpperInvariant())))[..12].ToLowerInvariant();
        var target = Path.Combine(_paths.ArtworkCache, $"exe-{hash}-icon.png");
        if (File.Exists(target))
        {
            return target;
        }

        try
        {
            return _iconExtractor.TryExtract(executablePath, target) ? target : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Icon extraction failed for {Path}", executablePath);
            return null;
        }
    }

    public bool IsCachedPath(string? path) =>
        !string.IsNullOrEmpty(path) &&
        Path.GetFullPath(path).StartsWith(Path.GetFullPath(_paths.ArtworkCache) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

    public async Task<ResolvedArtwork> ResolveAsync(
        GamePlatform platform,
        string platformIdentifier,
        IReadOnlyList<ArtworkCandidate> candidates,
        bool allowDownload,
        CancellationToken cancellationToken = default)
    {
        var cover = await ResolveKindAsync(platform, platformIdentifier, ArtworkKind.Cover, candidates, allowDownload, cancellationToken);
        var background = await ResolveKindAsync(platform, platformIdentifier, ArtworkKind.Background, candidates, allowDownload, cancellationToken);
        var icon = await ResolveKindAsync(platform, platformIdentifier, ArtworkKind.Icon, candidates, allowDownload, cancellationToken);
        return new ResolvedArtwork(cover, background, icon);
    }

    /// <summary>
    /// Artwork for a game whose launcher stores none: the Microsoft Store catalog for Xbox games (by
    /// package family name), otherwise a strict name match in the Steam store. A lookup that finds
    /// nothing is not repeated for a week; network errors are simply retried next time.
    /// </summary>
    public async Task<IReadOnlyList<ArtworkCandidate>> FindOnlineAsync(
        GamePlatform platform,
        string? platformIdentifier,
        string name,
        CancellationToken cancellationToken = default)
    {
        if (platform == GamePlatform.Xbox && !string.IsNullOrWhiteSpace(platformIdentifier))
        {
            var store = await LookupAsync(
                "msstore|" + platformIdentifier.ToUpperInvariant(),
                MicrosoftStore.LookupUrl(platformIdentifier),
                MicrosoftStore.ReadArtwork,
                cancellationToken);
            if (store.Count > 0)
            {
                return store;
            }
        }

        // Steam games already use their own app id; searching by name could only find the same game.
        var title = SteamStore.NormalizeTitle(name);
        if (platform == GamePlatform.Steam || title.Length == 0)
        {
            return [];
        }

        return await LookupAsync(
            "steam-search|" + title,
            SteamStore.SearchUrl(name),
            result => SteamStore.FindMatch(name, result) is { } appId ? SteamStore.CdnArtwork(appId) : [],
            cancellationToken);
    }

    /// <summary>Copies an image the user picked into Arclight's own folder so it survives the original moving.</summary>
    public string ImportUserImage(string sourcePath)
    {
        var info = new FileInfo(sourcePath);
        if (!info.Exists)
        {
            throw new FileNotFoundException("The selected image no longer exists.", sourcePath);
        }

        if (!SupportedImageExtensions.Contains(info.Extension))
        {
            throw new InvalidOperationException("Choose a PNG, JPG, BMP or GIF image.");
        }

        if (info.Length > MaxImageBytes)
        {
            throw new InvalidOperationException("The image is larger than 25 MB.");
        }

        Directory.CreateDirectory(_paths.UserImages);
        var target = Path.Combine(_paths.UserImages, $"{Guid.NewGuid():N}{info.Extension.ToLowerInvariant()}");
        File.Copy(info.FullName, target);
        return target;
    }

    public long GetCacheSizeBytes()
    {
        try
        {
            return Directory.Exists(_paths.ArtworkCache)
                ? new DirectoryInfo(_paths.ArtworkCache).EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)
                : 0;
        }
        catch (IOException)
        {
            return 0;
        }
    }

    /// <summary>Deletes cached artwork files. User-picked images are kept.</summary>
    public void ClearCache()
    {
        if (Directory.Exists(_paths.ArtworkCache))
        {
            foreach (var file in Directory.EnumerateFiles(_paths.ArtworkCache))
            {
                try
                {
                    File.Delete(file);
                }
                catch (IOException ex)
                {
                    _logger.LogWarning(ex, "Could not delete cached image {File}", file);
                }
            }
        }

        _store.ClearImageCacheEntries();
    }

    private async Task<string?> ResolveKindAsync(
        GamePlatform platform,
        string platformIdentifier,
        ArtworkKind kind,
        IReadOnlyList<ArtworkCandidate> candidates,
        bool allowDownload,
        CancellationToken cancellationToken)
    {
        var options = candidates.Where(c => c.Kind == kind).ToList();
        if (options.Count == 0)
        {
            return null;
        }

        Directory.CreateDirectory(_paths.ArtworkCache);
        var key = CacheKey(platform, platformIdentifier, kind);

        // A scan and the startup pass may ask for the same image at once; the second one waits and reuses the file.
        var gate = _keyLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            var existing = Directory.EnumerateFiles(_paths.ArtworkCache, key + ".*")
                .FirstOrDefault(f => !f.EndsWith(".download", StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                return existing;
            }

            foreach (var candidate in options)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!candidate.IsRemote)
                {
                    if (CopyLocal(candidate.Source, key) is { } copied)
                    {
                        return copied;
                    }

                    continue;
                }

                if (!allowDownload)
                {
                    continue;
                }

                if (await DownloadAsync(candidate.Source, key, cancellationToken) is { } downloaded)
                {
                    return downloaded;
                }
            }

            return null;
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<IReadOnlyList<ArtworkCandidate>> LookupAsync(
        string entryKey,
        string url,
        Func<JsonElement, IReadOnlyList<ArtworkCandidate>> read,
        CancellationToken cancellationToken)
    {
        var previous = _store.GetImageCacheEntry(entryKey);
        if (previous is { Status: "failed" } && _clock.UtcNow - previous.UpdatedAt < FailureCooldown)
        {
            return [];
        }

        try
        {
            using var response = await _http.GetAsync(url, cancellationToken);
            IReadOnlyList<ArtworkCandidate> found = [];
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                found = read(json.RootElement);
            }
            else if (response.StatusCode != System.Net.HttpStatusCode.NotFound)
            {
                return [];
            }

            if (found.Count == 0)
            {
                _store.SetImageCacheEntry(new ImageCacheEntry(entryKey, url, null, "failed", _clock.UtcNow));
            }

            return found;
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Artwork lookup failed for {Url}", url);
            return [];
        }
    }

    private string? CopyLocal(string source, string key)
    {
        try
        {
            var info = new FileInfo(source);
            if (!info.Exists || info.Length == 0)
            {
                return null;
            }

            // Executables and .ico files carry several icon sizes; let the platform extractor pick the largest.
            if (info.Extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) || info.Extension.Equals(".ico", StringComparison.OrdinalIgnoreCase))
            {
                if (_iconExtractor is null)
                {
                    return null;
                }

                var png = Path.Combine(_paths.ArtworkCache, key + ".png");
                return _iconExtractor.TryExtract(info.FullName, png) ? png : null;
            }

            if (info.Length > MaxImageBytes || !SupportedImageExtensions.Contains(info.Extension))
            {
                return null;
            }

            var target = Path.Combine(_paths.ArtworkCache, key + info.Extension.ToLowerInvariant());
            File.Copy(info.FullName, target, overwrite: true);
            return target;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogDebug(ex, "Could not copy artwork from {Source}", source);
            return null;
        }
    }

    private async Task<string?> DownloadAsync(string url, string key, CancellationToken cancellationToken)
    {
        var entryKey = key + "|" + url;
        var previous = _store.GetImageCacheEntry(entryKey);
        if (previous is { Status: "failed" } && _clock.UtcNow - previous.UpdatedAt < FailureCooldown)
        {
            return null;
        }

        await _downloadSlots.WaitAsync(cancellationToken);
        var temp = Path.Combine(_paths.ArtworkCache, key + ".download");
        try
        {
            using var response = await _http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            var mediaType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
            if (!response.IsSuccessStatusCode || !mediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase) ||
                response.Content.Headers.ContentLength > MaxImageBytes)
            {
                _store.SetImageCacheEntry(new ImageCacheEntry(entryKey, url, null, "failed", _clock.UtcNow));
                return null;
            }

            var extension = mediaType.ToLowerInvariant() switch
            {
                "image/png" => ".png",
                "image/gif" => ".gif",
                "image/bmp" => ".bmp",
                _ => ".jpg",
            };

            await using (var output = File.Create(temp))
            await using (var input = await response.Content.ReadAsStreamAsync(cancellationToken))
            {
                var buffer = new byte[81920];
                long total = 0;
                int read;
                while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    total += read;
                    if (total > MaxImageBytes)
                    {
                        throw new InvalidDataException("Image exceeds the size limit.");
                    }

                    await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                }
            }

            var target = Path.Combine(_paths.ArtworkCache, key + extension);
            File.Move(temp, target, overwrite: true);
            _store.SetImageCacheEntry(new ImageCacheEntry(entryKey, url, target, "ok", _clock.UtcNow));
            return target;
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException or InvalidDataException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogDebug(ex, "Artwork download failed for {Url}", url);
            _store.SetImageCacheEntry(new ImageCacheEntry(entryKey, url, null, "failed", _clock.UtcNow));
            return null;
        }
        finally
        {
            _downloadSlots.Release();
            TryDelete(temp);
        }
    }

    private static string CacheKey(GamePlatform platform, string platformIdentifier, ArtworkKind kind)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(platformIdentifier.ToUpperInvariant())))[..12].ToLowerInvariant();
        return $"{PlatformInfo.Code(platform)}-{hash}-{kind.ToString().ToLowerInvariant()}";
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }
}
