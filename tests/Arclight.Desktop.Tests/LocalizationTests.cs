using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Threading;
using System.Xml.Linq;
using Arclight.Core.Formatting;
using Arclight.Core.Launching;
using Arclight.Core.Models;
using Arclight.Desktop.Services;

namespace Arclight.Desktop.Tests;

/// <summary>The shipped string tables (copied next to the tests), read as plain XML.</summary>
internal static partial class StringTables
{
    public static readonly string[] Languages = ["en", "tr"];

    private static readonly XNamespace Xaml = "http://schemas.microsoft.com/winfx/2006/xaml";

    public static IReadOnlyDictionary<string, string> Load(string language)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Strings", $"Strings.{language}.xaml");
        var document = XDocument.Load(path, LoadOptions.PreserveWhitespace);
        var table = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in document.Root!.Elements())
        {
            var key = (string?)element.Attribute(Xaml + "Key");
            Assert.False(string.IsNullOrEmpty(key), $"An entry in Strings.{language}.xaml has no x:Key.");
            Assert.True(table.TryAdd(key!, element.Value), $"Strings.{language}.xaml defines {key} twice.");
        }

        return table;
    }

    public static ISet<string> Placeholders(string text) =>
        Placeholder().Matches(text).Select(m => m.Groups[1].Value).ToHashSet();

    [GeneratedRegex(@"(?<!\{)\{(\d+)(?:[,:][^}]*)?\}")]
    private static partial Regex Placeholder();
}

public class StringTableTests
{
    // Same text in both languages on purpose: brand slogan, language names, URIs and file names,
    // date patterns, and "Platform", which is also the Turkish word.
    private static readonly HashSet<string> SameInBothLanguages =
    [
        "Editor.Platform", "Editor.SteamRungameid730", "Editor.WindowedSkipintro", "Format.LongDate",
        "Format.ShortDate", "Grid.PLATFORM", "Language.English", "Language.Turkish",
        "Library.Filter.Platform", "Platform.PC", "Settings.ModmanagerExe", "Settings.YourGamesOneUniverse",
    ];

    [Fact]
    public void Every_language_defines_the_same_keys()
    {
        var english = StringTables.Load("en");
        foreach (var language in StringTables.Languages.Skip(1))
        {
            var other = StringTables.Load(language);
            var missing = english.Keys.Except(other.Keys).Order().ToList();
            var extra = other.Keys.Except(english.Keys).Order().ToList();
            Assert.True(missing.Count == 0, $"Missing in Strings.{language}.xaml: {string.Join(", ", missing)}");
            Assert.True(extra.Count == 0, $"Only in Strings.{language}.xaml: {string.Join(", ", extra)}");
        }
    }

    [Fact]
    public void No_string_is_empty()
    {
        foreach (var language in StringTables.Languages)
        {
            var empty = StringTables.Load(language).Where(e => string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Key).ToList();
            Assert.True(empty.Count == 0, $"Empty in Strings.{language}.xaml: {string.Join(", ", empty)}");
        }
    }

    [Fact]
    public void Translations_keep_every_placeholder()
    {
        var english = StringTables.Load("en");
        var turkish = StringTables.Load("tr");
        var mismatched = english.Keys
            .Where(key => !StringTables.Placeholders(english[key]).SetEquals(StringTables.Placeholders(turkish[key])))
            .ToList();

        Assert.True(mismatched.Count == 0, "Placeholders differ for: " + string.Join(", ", mismatched));
    }

    [Fact]
    public void Text_that_is_joined_to_other_text_keeps_its_spacing()
    {
        // Some strings sit next to a number in the same line ("New " + 3), so edge spaces matter.
        var english = StringTables.Load("en");
        var turkish = StringTables.Load("tr");
        var different = english.Keys
            .Where(key => char.IsWhiteSpace(english[key][0]) != char.IsWhiteSpace(turkish[key][0]) ||
                          char.IsWhiteSpace(english[key][^1]) != char.IsWhiteSpace(turkish[key][^1]))
            .ToList();

        Assert.True(different.Count == 0, "Leading/trailing spaces differ for: " + string.Join(", ", different));
    }

    [Fact]
    public void Turkish_text_is_translated()
    {
        var english = StringTables.Load("en");
        var turkish = StringTables.Load("tr");
        var untranslated = english.Keys
            .Where(key => english[key] == turkish[key] && !SameInBothLanguages.Contains(key))
            .ToList();

        Assert.True(untranslated.Count == 0, "Same as English: " + string.Join(", ", untranslated));
    }

    [Fact]
    public void Every_launch_validation_error_has_a_translation()
    {
        var turkish = StringTables.Load("tr");
        var results = new[]
        {
            LaunchValidator.ValidateExecutablePath(null),
            LaunchValidator.ValidateExecutablePath("C:\\Games\\a\u0001.exe"),
            LaunchValidator.ValidateExecutablePath("game.exe"),
            LaunchValidator.ValidateExecutablePath(@"C:\Games\notes.txt"),
            LaunchValidator.ValidateArguments(new string('a', 3000)),
            LaunchValidator.ValidateArguments("-a\n-b"),
            LaunchValidator.ValidateLauncherUri(null),
            LaunchValidator.ValidateLauncherUri("not a link"),
            LaunchValidator.ValidateLauncherUri(@"shell:AppsFolder\bad"),
            LaunchValidator.ValidateLauncherUri("no-scheme"),
            LaunchValidator.ValidateLauncherUri("http://example.com"),
            LaunchValidator.ValidateGame(new Game { Name = " " }),
            LaunchValidator.ValidateGame(new Game { Name = "Game", LaunchMethod = (LaunchMethod)99 }),
        };

        foreach (var result in results)
        {
            Assert.False(result.IsValid);
            Assert.False(string.IsNullOrEmpty(result.Code));
            Assert.True(turkish.ContainsKey("Validation." + result.Code), $"No translation for Validation.{result.Code}");
        }

        Assert.Equal(results.Length, results.Select(r => r.Code).Distinct().Count());
    }

    [Fact]
    public void Keys_used_by_the_app_exist()
    {
        // Looks at the source next to the repository when the tests run from a checkout.
        var root = FindRepositoryRoot();
        if (root is null)
        {
            return;
        }

        var english = StringTables.Load("en");
        var prefixes = english.Keys.Select(k => k[..k.IndexOf('.')]).ToHashSet();
        var keyPattern = new Regex(@"(?:""|DynamicResource )([A-Z][A-Za-z]+\.[A-Za-z0-9.]*[A-Za-z0-9])(?:""|\})");
        var missing = new SortedSet<string>(StringComparer.Ordinal);
        var desktop = Path.Combine(root, "src", "Arclight.Desktop");
        foreach (var file in Directory.EnumerateFiles(desktop, "*.*", SearchOption.AllDirectories))
        {
            if (!(file.EndsWith(".cs", StringComparison.Ordinal) || file.EndsWith(".xaml", StringComparison.Ordinal)) ||
                file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal) ||
                Path.GetFileName(file).StartsWith("Strings.", StringComparison.Ordinal))
            {
                continue;
            }

            foreach (Match match in keyPattern.Matches(File.ReadAllText(file)))
            {
                var key = match.Groups[1].Value;
                if (prefixes.Contains(key[..key.IndexOf('.')]) && !english.ContainsKey(key) && !english.ContainsKey(key + ".One"))
                {
                    missing.Add($"{key} ({Path.GetFileName(file)})");
                }
            }
        }

        Assert.True(missing.Count == 0, "Used but not defined: " + string.Join(", ", missing));
    }

    private static string? FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Arclight.sln")))
            {
                return directory.FullName;
            }
        }

        return null;
    }
}

[CollectionDefinition(nameof(UiLanguageCollection), DisableParallelization = true)]
public sealed class UiLanguageCollection;

/// <summary>Switches the real compiled dictionaries inside a WPF application (Loc is global state).</summary>
[Collection(nameof(UiLanguageCollection))]
public sealed class UiLanguageTests
{
    [Fact]
    public void Switching_language_updates_text_numbers_and_formatting()
    {
        WpfApp.Run(() =>
        {
            try
            {
                Loc.Apply("tr");
                Assert.Equal("tr", Loc.Language);
                Assert.Equal("Ana Sayfa", Loc.Get("Shell.Home"));
                Assert.Equal("Ana Sayfa", Application.Current.TryFindResource("Shell.Home"));
                Assert.Equal("3 oyun", Loc.Count("Count.Game", 3));
                Assert.Equal("12,5 sa", Format.PlaytimeShort(45_000));
                Assert.Equal("0 saat takip edildi", Format.Playtime(0));
                Assert.Equal("Hiç oynanmadı", Format.Relative(null, DateTimeOffset.UtcNow));

                Loc.Apply("en");
                Assert.Equal("Home", Loc.Get("Shell.Home"));
                Assert.Equal("1 game", Loc.Count("Count.Game", 1));
                Assert.Equal("1,250 games", Loc.Count("Count.Game", 1250));
                Assert.Equal("12.5 h", Format.PlaytimeShort(45_000));
                Assert.Equal("0 hours tracked", Format.Playtime(0));
            }
            finally
            {
                Loc.Apply("en");
            }
        });
    }

    [Fact]
    public void Compiled_strings_keep_spaces_line_breaks_and_placeholders()
    {
        WpfApp.Run(() =>
        {
            try
            {
                Loc.Apply("tr");
                Assert.Equal("Yeni ", Loc.Get("Scan.New"));
                Assert.Equal("Seçilenleri ekle (", Loc.Get("Scan.AddSelected"));
                Assert.Contains("\n", Loc.Get("Startup.DatabaseError.Message"), StringComparison.Ordinal);
                Assert.Equal("Kütüphanene 2 oyun eklendi", Loc.Format("Scan.Toast.Added.Title", Loc.Count("Count.Game", 2)));
            }
            finally
            {
                Loc.Apply("en");
            }
        });
    }

    [Fact]
    public void Labels_and_validation_errors_follow_the_language()
    {
        WpfApp.Run(() =>
        {
            try
            {
                Loc.Apply("en");
                var choice = new Choice<string>("tr", "Language.Turkish");
                var dark = new Choice<AppThemeProbe>(AppThemeProbe.Dark, "Settings.Theme.Dark");
                var changed = new List<string?>();
                dark.PropertyChanged += (_, e) => changed.Add(e.PropertyName);
                var invalid = LaunchValidator.ValidateLauncherUri("http://example.com");
                Assert.Equal("Dark", dark.Label);
                Assert.StartsWith("'http://' links are not allowed.", Loc.Validation(invalid), StringComparison.Ordinal);

                Loc.Apply("tr");
                Assert.Equal("Koyu", dark.Label);
                Assert.Equal("Türkçe", choice.Label);
                Assert.Contains(nameof(Choice<AppThemeProbe>.Label), changed);
                Assert.StartsWith("'http://' bağlantılarına izin verilmiyor.", Loc.Validation(invalid), StringComparison.Ordinal);
            }
            finally
            {
                Loc.Apply("en");
            }
        });
    }

    [Fact]
    public void Automatic_language_follows_the_windows_display_language()
    {
        var original = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal("tr", Loc.Resolve(Loc.Automatic));
            Assert.Equal("en", Loc.Resolve("en"));

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            Assert.Equal("en", Loc.Resolve(Loc.Automatic));
            Assert.Equal("en", Loc.Resolve(null));
            Assert.Equal("tr", Loc.Resolve("tr"));
        }
        finally
        {
            CultureInfo.CurrentUICulture = original;
        }
    }

    public enum AppThemeProbe { Dark }
}

/// <summary>One WPF application on its own UI thread, shared by the tests that need resources.</summary>
internal static class WpfApp
{
    private static readonly Lazy<Dispatcher> UiThread = new(Start);

    public static void Run(Action action) => UiThread.Value.Invoke(action);

    private static Dispatcher Start()
    {
        Dispatcher? dispatcher = null;
        using var ready = new ManualResetEventSlim();
        var thread = new Thread(() =>
        {
            _ = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            dispatcher = Dispatcher.CurrentDispatcher;
            ready.Set();
            Dispatcher.Run();
        })
        {
            IsBackground = true,
            Name = "WPF test UI thread",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        ready.Wait();
        return dispatcher!;
    }
}
