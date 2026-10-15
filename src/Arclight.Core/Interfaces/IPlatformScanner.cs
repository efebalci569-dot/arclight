using Arclight.Core.Models;

namespace Arclight.Core.Interfaces;

/// <summary>
/// Reads one launcher's local install records. Scanners must be read-only: they never
/// modify launcher files, never ask for credentials and never touch protected folders.
/// </summary>
public interface IPlatformScanner
{
    GamePlatform Platform { get; }

    /// <summary>Stable id used in settings (for example "steam" or "folders").</summary>
    string Id { get; }

    string DisplayName { get; }

    PlatformSupportLevel SupportLevel { get; }

    Task<PlatformScanResult> ScanAsync(ScanOptions options, CancellationToken cancellationToken);
}
