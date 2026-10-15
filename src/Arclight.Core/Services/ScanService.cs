using Arclight.Core.Interfaces;
using Arclight.Core.Library;
using Arclight.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Arclight.Core.Services;

public sealed record ScanReport(
    IReadOnlyList<PlatformScanResult> Platforms,
    IReadOnlyList<ScanCandidate> Candidates,
    IReadOnlyList<GameInstallUpdate> Updates,
    IReadOnlyList<Game> AutoAdded,
    DateTimeOffset CompletedAt)
{
    public int FoundCount => Candidates.Count;
    public int NewCount => Candidates.Count(c => c.Status == ScanCandidateStatus.New);
    public int InLibraryCount => Candidates.Count(c => c.Status == ScanCandidateStatus.InLibrary);
    public int IgnoredCount => Candidates.Count(c => c.Status == ScanCandidateStatus.Ignored);
    public int ErrorCount => Platforms.Sum(p => p.Errors.Count);
}

public sealed class ScanProgressEventArgs(string scannerId, PlatformScanResult? result) : EventArgs
{
    public string ScannerId { get; } = scannerId;

    /// <summary>Null while the scanner is still running.</summary>
    public PlatformScanResult? Result { get; } = result;
}

/// <summary>
/// Runs the platform scanners, reconciles results with the library and fills in artwork.
/// Scans only read launcher records and the folders the user listed; the disk is never crawled.
/// </summary>
public sealed class ScanService
{
    private static readonly TimeSpan ScannerTimeout = TimeSpan.FromSeconds(60);

    private readonly IReadOnlyList<IPlatformScanner> _scanners;
    private readonly LibraryService _library;
    private readonly ArtworkService _artwork;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public ScanService(IEnumerable<IPlatformScanner> scanners, LibraryService library, ArtworkService artwork, IClock clock, ILogger<ScanService>? logger = null)
    {
        _scanners = scanners.ToList();
        _library = library;
        _artwork = artwork;
        _clock = clock;
        _logger = logger ?? NullLogger<ScanService>.Instance;
    }

    public event EventHandler<ScanProgressEventArgs>? Progress;

    public event EventHandler<ScanReport>? Completed;

    public IReadOnlyList<IPlatformScanner> Scanners => _scanners;

    public ScanReport? LastReport { get; private set; }

    public bool IsScanning => _gate.CurrentCount == 0;

    public async Task<ScanReport> ScanAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var settings = _library.Settings;
            var options = new ScanOptions
            {
                ExtraSteamLibraryFolders = settings.ExtraSteamLibraryFolders,
                CustomGameFolders = settings.CustomGameFolders,
            };

            var disabled = settings.DisabledScanners.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var results = await Task.WhenAll(_scanners.Select(scanner => RunScannerAsync(scanner, options, disabled, cancellationToken)));

            var ignored = await Task.Run(_library.GetIgnoredIdentityKeys, cancellationToken);
            var merge = ScanMerger.Merge(_library.Games.ToList(), results, ignored, settings.MarkMissingGamesAsUninstalled);
            await _library.ApplyInstallUpdatesAsync(merge.Updates);

            IReadOnlyList<Game> autoAdded = [];
            if (settings.AutoAddNewGames)
            {
                var toAdd = merge.Candidates
                    .Where(c => c.Status == ScanCandidateStatus.New && !c.Detected.LikelyNotAGame && c.Detected.Platform != GamePlatform.Manual)
                    .Select(c => c.Detected)
                    .ToList();
                autoAdded = await AddAsync(toAdd);
            }

            var report = new ScanReport(results, merge.Candidates, merge.Updates, autoAdded, _clock.UtcNow);
            LastReport = report;
            _logger.LogInformation(
                "Scan finished: {Found} found, {New} new, {Updates} updated, {Errors} errors",
                report.FoundCount, report.NewCount, merge.Updates.Count, report.ErrorCount);

            _ = FillMissingArtworkAsync(merge.Candidates.Where(c => c.Status == ScanCandidateStatus.InLibrary).ToList());
            Completed?.Invoke(this, report);
            return report;
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>Adds detected games to the library and fetches their artwork in the background.</summary>
    public async Task<IReadOnlyList<Game>> AddAsync(IReadOnlyCollection<DetectedGame> detected)
    {
        var now = _clock.UtcNow;
        var pairs = detected.Select(d => (Detected: d, Game: d.ToGame(now))).ToList();
        var added = await _library.AddGamesAsync(pairs.Select(p => p.Game).ToList());
        _ = ResolveArtworkAsync(pairs.Where(p => p.Game.Id != 0).ToList());
        return added;
    }

    public Task IgnoreAsync(DetectedGame detected) =>
        _library.IgnoreAsync(ScanMerger.IdentityKey(detected.Platform, detected.PlatformIdentifier), detected.Name);

    public Task UnignoreAsync(DetectedGame detected) =>
        _library.UnignoreAsync(ScanMerger.IdentityKey(detected.Platform, detected.PlatformIdentifier));

    private async Task<PlatformScanResult> RunScannerAsync(IPlatformScanner scanner, ScanOptions options, HashSet<string> disabled, CancellationToken cancellationToken)
    {
        if (disabled.Contains(scanner.Id))
        {
            var skipped = new PlatformScanResult(scanner.Platform, scanner.DisplayName, scanner.SupportLevel, PlatformScanStatus.Disabled, [], []);
            Progress?.Invoke(this, new ScanProgressEventArgs(scanner.Id, skipped));
            return skipped;
        }

        Progress?.Invoke(this, new ScanProgressEventArgs(scanner.Id, null));
        PlatformScanResult result;
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(ScannerTimeout);
            result = await Task.Run(() => scanner.ScanAsync(options, timeout.Token), timeout.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            result = PlatformScanResult.Failure(scanner.Platform, scanner.DisplayName, scanner.SupportLevel, "The scan took too long and was stopped.");
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Scanner {Scanner} failed", scanner.Id);
            result = PlatformScanResult.Failure(scanner.Platform, scanner.DisplayName, scanner.SupportLevel, ex.Message);
        }

        Progress?.Invoke(this, new ScanProgressEventArgs(scanner.Id, result));
        return result;
    }

    private async Task ResolveArtworkAsync(IReadOnlyList<(DetectedGame Detected, Game Game)> pairs)
    {
        foreach (var (detected, game) in pairs)
        {
            await ApplyArtworkAsync(game, detected);
        }
    }

    private async Task FillMissingArtworkAsync(IReadOnlyList<ScanCandidate> inLibrary)
    {
        foreach (var candidate in inLibrary)
        {
            if (candidate.ExistingGameId is not { } id || _library.FindGame(id) is not { } game)
            {
                continue;
            }

            var missing = (game.CoverImagePath is null || !File.Exists(game.CoverImagePath)) ||
                          (game.BackgroundImagePath is null || !File.Exists(game.BackgroundImagePath));
            if (missing && game.Platform == candidate.Detected.Platform)
            {
                await ApplyArtworkAsync(game, candidate.Detected);
            }
        }
    }

    private async Task ApplyArtworkAsync(Game game, DetectedGame detected)
    {
        if (detected.Artwork.Count == 0)
        {
            return;
        }

        try
        {
            var resolved = await _artwork.ResolveAsync(detected.Platform, detected.PlatformIdentifier, detected.Artwork, _library.Settings.DownloadMissingArtwork);
            var cover = Keep(game.CoverImagePath) ?? resolved.Cover;
            var background = Keep(game.BackgroundImagePath) ?? resolved.Background;
            var icon = Keep(game.IconImagePath) ?? resolved.Icon;
            if (cover != game.CoverImagePath || background != game.BackgroundImagePath || icon != game.IconImagePath)
            {
                await _library.SetArtworkAsync(game.Id, cover, background, icon);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not resolve artwork for {Name}", game.Name);
        }

        // Existing images (especially ones the user picked) always win over detected artwork.
        static string? Keep(string? path) => path is not null && File.Exists(path) ? path : null;
    }
}
