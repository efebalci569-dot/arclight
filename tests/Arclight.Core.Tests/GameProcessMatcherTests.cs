using Arclight.Core.Interfaces;
using Arclight.Core.Tracking;

namespace Arclight.Core.Tests;

public class GameProcessMatcherTests
{
    private static RunningProcess P(string path) => new(1, Path.GetFileNameWithoutExtension(path), path);

    private const string Dir = @"D:\Steam\steamapps\common\Hades";

    [Fact]
    public void Process_inside_install_folder_counts()
    {
        var matcher = new GameProcessMatcher();
        var running = matcher.FindRunning([P(@"D:\Steam\steamapps\common\Hades\x64\Hades.exe")], [new TrackTarget(1, null, Dir)]);
        Assert.Contains(1, running);
    }

    [Theory]
    [InlineData(@"D:\Steam\steamapps\common\Hades\UnityCrashHandler64.exe")]
    [InlineData(@"D:\Steam\steamapps\common\Hades\GameLauncher.exe")]
    [InlineData(@"D:\Steam\steamapps\common\Hades\EasyAntiCheat\EasyAntiCheat_EOS.exe")]
    [InlineData(@"D:\Steam\steamapps\common\Hades\_CommonRedist\vc_redist.x64.exe")]
    [InlineData(@"C:\Program Files (x86)\Steam\steam.exe")]
    [InlineData(@"D:\Steam\steamapps\common\HadesII\Hades2.exe")]
    public void Helpers_launchers_and_other_folders_do_not_count(string path)
    {
        var matcher = new GameProcessMatcher();
        Assert.Empty(matcher.FindRunning([P(path)], [new TrackTarget(1, null, Dir)]));
    }

    [Fact]
    public void Configured_executable_counts_even_if_its_name_looks_like_a_launcher()
    {
        var matcher = new GameProcessMatcher();
        var exe = @"C:\Games\Old Game\launcher.exe";
        Assert.Contains(1, matcher.FindRunning([P(exe)], [new TrackTarget(1, exe, null)]));
    }

    [Fact]
    public void User_exclusions_apply()
    {
        var matcher = new GameProcessMatcher(["modmanager.exe"]);
        Assert.Empty(matcher.FindRunning([P(Dir + @"\ModManager.exe")], [new TrackTarget(1, null, Dir)]));
        Assert.Contains(1, matcher.FindRunning([P(Dir + @"\Hades.exe")], [new TrackTarget(1, null, Dir)]));
    }

    [Theory]
    [InlineData(@"C:\Program Files")]
    [InlineData(@"D:\Games")]
    [InlineData(@"C:\")]
    [InlineData(@"C:\Windows\System32")]
    [InlineData(@"C:\Windows")]
    public void Shared_folders_are_never_used_to_match(string directory)
    {
        var matcher = new GameProcessMatcher();
        Assert.Empty(matcher.FindRunning([P(directory.TrimEnd('\\') + @"\Something\app.exe")], [new TrackTarget(1, null, directory)]));
    }

    [Fact]
    public void Processes_without_a_readable_path_are_ignored()
    {
        var matcher = new GameProcessMatcher();
        Assert.Empty(matcher.FindRunning([new RunningProcess(1, "Hades", null)], [new TrackTarget(1, null, Dir)]));
    }
}
