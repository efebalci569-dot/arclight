using System.Globalization;

namespace Arclight.Desktop;

/// <summary>
/// Command-line switches. Normal users never need them; they exist for the startup entry
/// (<c>--minimized</c>) and for development (<c>--data-dir</c>, <c>--lang</c>, <c>--capture</c>, <c>--seed-demo</c>).
/// </summary>
public sealed record StartupOptions
{
    public string? DataDirectory { get; init; }

    public bool Minimized { get; init; }

    /// <summary>UI language for this run only (<c>en</c> or <c>tr</c>); the saved setting is not changed.</summary>
    public string? Language { get; init; }

    /// <summary>Render the window to this PNG file and exit (used for documentation screenshots).</summary>
    public string? CaptureFile { get; init; }

    public string? CapturePage { get; init; }

    public string? CaptureDialog { get; init; }

    public string? CaptureGame { get; init; }

    /// <summary>Capture: switch to this language after the page is shown (checks that open views follow).</summary>
    public string? CaptureSwitchLanguage { get; init; }

    public int CaptureWidth { get; init; } = 1440;

    public int CaptureHeight { get; init; } = 900;

    public int CaptureDelayMs { get; init; } = 2500;

    /// <summary>Fill an empty, separate data folder with a fictional sample library for screenshots.</summary>
    public bool SeedDemo { get; init; }

    public static StartupOptions Parse(IReadOnlyList<string> args)
    {
        var options = new StartupOptions();
        for (var i = 0; i < args.Count; i++)
        {
            string? Next() => i + 1 < args.Count ? args[++i] : null;
            switch (args[i].ToLowerInvariant())
            {
                case "--data-dir": options = options with { DataDirectory = Next() }; break;
                case "--minimized": options = options with { Minimized = true }; break;
                case "--lang": options = options with { Language = Next() }; break;
                case "--capture": options = options with { CaptureFile = Next() }; break;
                case "--page": options = options with { CapturePage = Next() }; break;
                case "--dialog": options = options with { CaptureDialog = Next() }; break;
                case "--game": options = options with { CaptureGame = Next() }; break;
                case "--switch-lang": options = options with { CaptureSwitchLanguage = Next() }; break;
                case "--seed-demo": options = options with { SeedDemo = true }; break;
                case "--wait":
                    options = options with { CaptureDelayMs = int.TryParse(Next(), NumberStyles.None, CultureInfo.InvariantCulture, out var ms) ? ms : 2500 };
                    break;
                case "--size":
                    var parts = Next()?.Split('x', 'X');
                    if (parts is [var w, var h] && int.TryParse(w, out var width) && int.TryParse(h, out var height))
                    {
                        options = options with { CaptureWidth = width, CaptureHeight = height };
                    }

                    break;
            }
        }

        return options;
    }
}
