using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Data;
using Arclight.Core.Models;
using Arclight.Desktop;
using Arclight.Desktop.Converters;
using Arclight.Desktop.Services;
using Arclight.Desktop.ViewModels.Dialogs;

namespace Arclight.Desktop.Tests;

public class StartupOptionsTests
{
    [Fact]
    public void Parses_known_switches()
    {
        var options = StartupOptions.Parse(["--data-dir", @"C:\Data", "--minimized", "--capture", "out.png", "--page", "Statistics", "--size", "1280x720", "--wait", "500"]);

        Assert.Equal(@"C:\Data", options.DataDirectory);
        Assert.True(options.Minimized);
        Assert.Equal("out.png", options.CaptureFile);
        Assert.Equal("Statistics", options.CapturePage);
        Assert.Equal(1280, options.CaptureWidth);
        Assert.Equal(720, options.CaptureHeight);
        Assert.Equal(500, options.CaptureDelayMs);
        Assert.False(options.SeedDemo);
    }

    [Fact]
    public void Language_switch_applies_to_this_run_only()
    {
        Assert.Equal("tr", StartupOptions.Parse(["--lang", "tr"]).Language);
        Assert.Null(StartupOptions.Parse(["--minimized"]).Language);
    }

    [Fact]
    public void Ignores_unknown_and_incomplete_switches()
    {
        var options = StartupOptions.Parse(["--whatever", "--size", "big", "--data-dir"]);
        Assert.Null(options.DataDirectory);
        Assert.Equal(1440, options.CaptureWidth);
    }
}

public class ConverterTests
{
    [Theory]
    [InlineData(true, null, Visibility.Visible)]
    [InlineData(false, null, Visibility.Collapsed)]
    [InlineData(true, "invert", Visibility.Collapsed)]
    [InlineData("text", null, Visibility.Visible)]
    [InlineData("", null, Visibility.Collapsed)]
    [InlineData(0, null, Visibility.Collapsed)]
    [InlineData(3, null, Visibility.Visible)]
    [InlineData(null, null, Visibility.Collapsed)]
    public void VisibleWhen_handles_common_values(object? value, string? parameter, Visibility expected)
    {
        Assert.Equal(expected, new VisibleWhenConverter().Convert(value, typeof(Visibility), parameter, CultureInfo.InvariantCulture));
    }

    [Fact]
    public void EnumEquals_round_trips_radio_selection()
    {
        var converter = new EnumEqualsConverter();
        Assert.Equal(true, converter.Convert(PageKey.Library, typeof(bool), "Library", CultureInfo.InvariantCulture));
        Assert.Equal(false, converter.Convert(PageKey.Home, typeof(bool), "Library", CultureInfo.InvariantCulture));
        Assert.Equal(PageKey.Statistics, converter.ConvertBack(true, typeof(PageKey), "Statistics", CultureInfo.InvariantCulture));
        Assert.Equal(Binding.DoNothing, converter.ConvertBack(false, typeof(PageKey), "Statistics", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void Badge_caps_large_counts()
    {
        var converter = new BadgeConverter();
        Assert.Equal("3", converter.Convert(3, typeof(string), null, CultureInfo.InvariantCulture));
        Assert.Equal("9+", converter.Convert(42, typeof(string), null, CultureInfo.InvariantCulture));
    }
}

public sealed class GameEditorTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "arclight-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        try
        {
            Directory.Delete(_folder, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    [Theory]
    [InlineData(@"Hollow Knight\hollow_knight.exe", "Hollow Knight")]
    [InlineData(@"Celeste\bin\Celeste.exe", "Celeste")]
    [InlineData(@"Witcher\bin\x64\witcher3.exe", "witcher3")]
    public void Suggests_a_name_from_the_folder_when_the_exe_has_no_product_name(string relative, string expected)
    {
        var path = Path.Combine(_folder, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, [0x4D, 0x5A]);

        Assert.Equal(expected, GameEditorViewModel.SuggestName(path));
    }

    [Fact]
    public void Platform_list_covers_every_platform()
    {
        Assert.Equal(Enum.GetValues<GamePlatform>().Order(), GameEditorViewModel.Platforms.Select(p => p.Platform).Order());
    }
}
