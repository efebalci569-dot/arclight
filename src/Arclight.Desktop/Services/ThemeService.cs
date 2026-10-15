using System.Windows;
using System.Windows.Media;
using Arclight.Core.Models;
using Arclight.Desktop.Controls;

namespace Arclight.Desktop.Services;

public sealed class AccentOption : System.ComponentModel.INotifyPropertyChanged
{
    public AccentOption(string name, Color color)
    {
        Name = name;
        Color = color;
        Brush = Frozen(color);
        Loc.LanguageChanged += (_, _) => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(DisplayName)));
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Stable id stored in settings.</summary>
    public string Name { get; }

    public string DisplayName => Loc.Get("Accent." + Name);

    public Color Color { get; }

    public SolidColorBrush Brush { get; }

    private static SolidColorBrush Frozen(Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }
}

/// <summary>Applies theme, accent color and motion preferences by swapping application brushes.</summary>
public sealed class ThemeService
{
    public static IReadOnlyList<AccentOption> Accents { get; } =
    [
        new("Violet", Color.FromRgb(0x8B, 0x5C, 0xF6)),
        new("Sky", Color.FromRgb(0x38, 0xBD, 0xF8)),
        new("Indigo", Color.FromRgb(0x63, 0x66, 0xF1)),
        new("Emerald", Color.FromRgb(0x10, 0xB9, 0x81)),
        new("Amber", Color.FromRgb(0xF5, 0x9E, 0x0B)),
        new("Rose", Color.FromRgb(0xF4, 0x3F, 0x5E)),
        new("Fuchsia", Color.FromRgb(0xD9, 0x46, 0xEF)),
    ];

    public static bool ReduceMotion { get; private set; }

    public void Apply(AppSettings settings)
    {
        var resources = Application.Current.Resources;
        var accent = Accents.FirstOrDefault(a => a.Name.Equals(settings.AccentColor, StringComparison.OrdinalIgnoreCase)) ?? Accents[0];
        var c = accent.Color;

        Set(resources, "Brush.Accent", c);
        Set(resources, "Brush.Accent.Hover", Mix(c, Colors.White, 0.14));
        Set(resources, "Brush.Accent.Pressed", Mix(c, Colors.Black, 0.12));
        Set(resources, "Brush.Accent.Soft", Color.FromArgb(0x26, c.R, c.G, c.B));
        Set(resources, "Brush.Accent.Glow", Color.FromArgb(0x59, c.R, c.G, c.B));
        Set(resources, "Brush.Accent.Text", Mix(c, Colors.White, 0.35));
        resources["Color.Accent"] = c;

        var oled = settings.Theme == AppTheme.OledBlack;
        Set(resources, "Brush.Bg.App", oled ? Color.FromRgb(0, 0, 0) : Color.FromRgb(0x09, 0x09, 0x0B));
        Set(resources, "Brush.Bg.Secondary", oled ? Color.FromRgb(0x07, 0x07, 0x09) : Color.FromRgb(0x10, 0x10, 0x14));
        Set(resources, "Brush.Bg.Card", oled ? Color.FromRgb(0x10, 0x10, 0x14) : Color.FromRgb(0x17, 0x17, 0x1D));
        Set(resources, "Brush.Bg.Input", oled ? Color.FromRgb(0x0C, 0x0C, 0x10) : Color.FromRgb(0x14, 0x14, 0x19));

        ReduceMotion = settings.ReduceAnimations;
        AsyncImage.ReduceMotion = settings.ReduceAnimations;
    }

    private static void Set(ResourceDictionary resources, string key, Color color)
    {
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        resources[key] = brush;
    }

    private static Color Mix(Color a, Color b, double amount) => Color.FromRgb(
        (byte)(a.R + ((b.R - a.R) * amount)),
        (byte)(a.G + ((b.G - a.G) * amount)),
        (byte)(a.B + ((b.B - a.B) * amount)));
}
