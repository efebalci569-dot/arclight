using System.ComponentModel;
using System.Globalization;
using System.Windows;
using System.Windows.Markup;
using Arclight.Core.Formatting;
using Arclight.Core.Models;

namespace Arclight.Desktop.Services;

/// <summary>
/// UI language. Strings live in Resources/Strings/Strings.{language}.xaml; XAML uses them through
/// DynamicResource so switching language updates every open view, and code reads them with
/// <see cref="Get"/> / <see cref="Format"/>. Views and view models refresh their computed text on
/// <see cref="LanguageChanged"/>.
/// </summary>
public static class Loc
{
    public const string Automatic = "auto";

    private static ResourceDictionary? _strings;

    public static event EventHandler? LanguageChanged;

    /// <summary>The language in use: "en" or "tr".</summary>
    public static string Language { get; private set; } = "en";

    public static CultureInfo Culture { get; private set; } = CultureInfo.GetCultureInfo("en-US");

    /// <summary>The language WPF uses for bindings with StringFormat and text shaping.</summary>
    public static XmlLanguage XmlLanguage => XmlLanguage.GetLanguage(Culture.IetfLanguageTag);

    /// <summary>Maps the setting ("auto", "en", "tr") to a supported language.</summary>
    public static string Resolve(string? setting) => setting switch
    {
        "en" => "en",
        "tr" => "tr",
        _ => CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr" ? "tr" : "en",
    };

    public static void Apply(string? setting)
    {
        var application = Application.Current;
        if (application is null)
        {
            return;
        }

        var language = Resolve(setting);
        if (_strings is not null && language == Language)
        {
            return;
        }

        var dictionary = new ResourceDictionary
        {
            Source = new Uri($"/Arclight;component/Resources/Strings/Strings.{language}.xaml", UriKind.Relative),
        };
        var merged = application.Resources.MergedDictionaries;
        if (_strings is not null)
        {
            merged.Remove(_strings);
        }

        merged.Add(dictionary);
        _strings = dictionary;
        Language = language;
        Culture = CultureInfo.GetCultureInfo(language == "tr" ? "tr-TR" : "en-US");
        Arclight.Core.Formatting.Format.Strings = BuildFormatStrings();

        foreach (Window window in application.Windows)
        {
            window.Language = XmlLanguage;
        }

        LanguageChanged?.Invoke(null, EventArgs.Empty);
    }

    /// <summary>The localized string for a key; the key itself if it is missing (never throws).</summary>
    public static string Get(string key) =>
        _strings?[key] as string ?? Application.Current?.TryFindResource(key) as string ?? key;

    public static string Format(string key, params object?[] args) => string.Format(Culture, Get(key), args);

    /// <summary>"1 game" / "3 games" — uses <c>key.One</c> and <c>key.Other</c>.</summary>
    public static string Count(string key, long count) =>
        Format(count == 1 ? key + ".One" : key + ".Other", count.ToString("#,0", Culture));

    /// <summary>A launch validation error in the UI language (falls back to the English text).</summary>
    public static string Validation(Arclight.Core.Launching.ValidationResult result, string? fallback = null)
    {
        if (result.IsValid)
        {
            return fallback ?? string.Empty;
        }

        var key = "Validation." + result.Code;
        var template = Get(key);
        return template == key ? result.Error ?? fallback ?? string.Empty : string.Format(Culture, template, result.Args ?? []);
    }

    /// <summary>Scanner names that need translating; launcher names stay as they are.</summary>
    public static string ScannerName(string id, string displayName) => id == "folders" ? Get("Scanner.Folders") : displayName;

    public static string PlatformName(GamePlatform platform) => platform switch
    {
        GamePlatform.Manual => Get("Platform.PC"),
        GamePlatform.Other => Get("Platform.Other"),
        _ => PlatformInfo.DisplayName(platform),
    };

    private static FormatStrings BuildFormatStrings() => new()
    {
        Culture = Culture,
        NoPlaytime = Get("Format.NoPlaytime"),
        LessThanMinute = Get("Format.LessThanMinute"),
        Minutes = Get("Format.Minutes"),
        HoursMinutes = Get("Format.HoursMinutes"),
        Hours = Get("Format.Hours"),
        NotPlayed = Get("Format.NotPlayed"),
        NeverPlayed = Get("Format.NeverPlayed"),
        JustNow = Get("Format.JustNow"),
        MinutesAgo = Get("Format.MinutesAgo"),
        HoursAgo = Get("Format.HoursAgo"),
        Yesterday = Get("Format.Yesterday"),
        DaysAgo = Get("Format.DaysAgo"),
        WeekAgo = Get("Format.WeekAgo"),
        WeeksAgo = Get("Format.WeeksAgo"),
        ShortDate = Get("Format.ShortDate"),
        LongDate = Get("Format.LongDate"),
        LongDateTime = Get("Format.LongDateTime"),
    };
}

/// <summary>A selectable option whose label follows the UI language.</summary>
public sealed class Choice<T> : INotifyPropertyChanged
{
    public Choice(T value, string labelKey)
    {
        Value = value;
        LabelKey = labelKey;
        Loc.LanguageChanged += (_, _) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Label)));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public T Value { get; }

    public string LabelKey { get; }

    public string Label => Loc.Get(LabelKey);

    public override string ToString() => Label;
}
