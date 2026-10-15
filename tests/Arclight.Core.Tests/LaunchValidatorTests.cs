using Arclight.Core.Launching;
using Arclight.Core.Models;

namespace Arclight.Core.Tests;

public class LaunchValidatorTests
{
    [Theory]
    [InlineData("steam://rungameid/730")]
    [InlineData("com.epicgames.launcher://apps/ns%3Aid%3Aapp?action=launch&silent=true")]
    [InlineData("uplay://launch/635/0")]
    [InlineData(@"shell:AppsFolder\ConcernedApe.StardewValleyPC_0c8vynj4cqe4e!Game")]
    public void Accepts_known_launcher_links(string uri)
    {
        Assert.True(LaunchValidator.ValidateLauncherUri(uri).IsValid);
    }

    [Theory]
    [InlineData("http://example.com/game")]
    [InlineData("file:///C:/Windows/System32/cmd.exe")]
    [InlineData("ms-settings:privacy")]
    [InlineData("steam://run game")]
    [InlineData("steam://rungameid/730\ncalc")]
    [InlineData(@"shell:AppsFolder\..\..\cmd")]
    [InlineData("")]
    public void Rejects_other_schemes_and_malformed_links(string uri)
    {
        Assert.False(LaunchValidator.ValidateLauncherUri(uri).IsValid);
    }

    [Theory]
    [InlineData(@"C:\Games\Hades\Hades.exe", true)]
    [InlineData(@"C:\Games\Hades\Hades.lnk", true)]
    [InlineData(@"Hades.exe", false)]
    [InlineData(@"C:\Games\Hades\readme.txt", false)]
    [InlineData(@"C:\Games\Hades\script.ps1", false)]
    [InlineData("", false)]
    public void Validates_executable_paths(string path, bool valid)
    {
        Assert.Equal(valid, LaunchValidator.ValidateExecutablePath(path).IsValid);
    }

    [Fact]
    public void Rejects_control_characters_in_arguments()
    {
        Assert.True(LaunchValidator.ValidateArguments("-windowed -fps 144").IsValid);
        Assert.False(LaunchValidator.ValidateArguments("-a\r\nshutdown").IsValid);
    }

    [Fact]
    public void Validates_a_whole_game()
    {
        var game = new Game { Name = "Hades", LaunchMethod = LaunchMethod.LauncherUri, LaunchTarget = "steam://rungameid/1145360" };
        Assert.True(LaunchValidator.ValidateGame(game).IsValid);

        game.Name = " ";
        Assert.False(LaunchValidator.ValidateGame(game).IsValid);
    }

    [Fact]
    public void Epic_identifier_roundtrip()
    {
        Assert.True(LauncherUris.TryParseEpicIdentifier("ns:item:app", out var ns, out var item, out var app));
        Assert.Equal(("ns", "item", "app"), (ns, item, app));
        Assert.Equal("com.epicgames.launcher://apps/ns%3Aitem%3Aapp?action=launch&silent=true", LauncherUris.EpicRun(ns, item, app));
        Assert.False(LauncherUris.TryParseEpicIdentifier("broken", out _, out _, out _));
    }
}
