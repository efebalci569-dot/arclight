using Arclight.Core.Interfaces;
using Arclight.Core.Models;
using Arclight.Core.Services;
using Arclight.Core.Tracking;

namespace Arclight.Core.Tests;

public sealed class TrackingServiceTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly FakeClock _clock = new(Games.BaseTime);
    private readonly FakeProcesses _processes = new();
    private readonly LibraryService _library;
    private readonly TrackingService _tracking;

    public TrackingServiceTests()
    {
        _library = new LibraryService(_db.Games, _db.Collections, _db.Sessions, _db.Settings, _clock);
        _tracking = new TrackingService(_db.Sessions, _library, _processes, _clock);
    }

    public void Dispose() => _db.Dispose();

    private async Task<Game> AddGameAsync(string name, string dir)
    {
        await _library.LoadAsync();
        return await _library.AddGameAsync(new Game
        {
            Name = name,
            LaunchMethod = LaunchMethod.LauncherUri,
            LaunchTarget = "steam://rungameid/1",
            Platform = GamePlatform.Steam,
            PlatformIdentifier = "1",
            InstallDirectory = dir,
        });
    }

    private async Task PollEvery5Seconds(int count)
    {
        for (var i = 0; i < count; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(5));
            await _tracking.PollAsync();
        }
    }

    [Fact]
    public async Task Game_launched_from_Arclight_gets_a_verified_session()
    {
        var game = await AddGameAsync("Hades", @"D:\Steam\steamapps\common\Hades");
        _tracking.ApplySettings(new AppSettings());
        Assert.True(_tracking.NotifyLaunched(game));

        await PollEvery5Seconds(2); // launcher is still starting up
        _processes.Running.Add(new RunningProcess(100, "Hades", @"D:\Steam\steamapps\common\Hades\x64\Hades.exe"));
        await PollEvery5Seconds(12 * 10); // 10 minutes of play
        _processes.Running.Clear();
        await PollEvery5Seconds(6);

        var loaded = _library.FindGame(game.Id)!;
        Assert.InRange(loaded.TotalPlaytimeSeconds, 595, 600);
        Assert.Equal(1, loaded.SessionCount);
        Assert.NotNull(loaded.LastPlayed);
        Assert.Single(_db.Sessions.GetSessions());
    }

    [Fact]
    public async Task Nothing_is_tracked_when_tracking_is_disabled()
    {
        var game = await AddGameAsync("Hades", @"D:\Steam\steamapps\common\Hades");
        _tracking.ApplySettings(new AppSettings { PlaytimeTrackingEnabled = false, BackgroundWatcherEnabled = true });

        Assert.False(_tracking.NotifyLaunched(game));
        _processes.Running.Add(new RunningProcess(100, "Hades", @"D:\Steam\steamapps\common\Hades\Hades.exe"));
        await PollEvery5Seconds(30);

        Assert.Equal(0, _library.FindGame(game.Id)!.TotalPlaytimeSeconds);
    }

    [Fact]
    public async Task Background_watcher_only_follows_games_with_tracking_enabled()
    {
        var tracked = await AddGameAsync("Hades", @"D:\Games\Hades");
        var untracked = await _library.AddGameAsync(new Game
        {
            Name = "Celeste",
            LaunchMethod = LaunchMethod.Executable,
            ExecutablePath = @"D:\Games\Celeste\Celeste.exe",
            IsTrackingEnabled = false,
        });
        _tracking.ApplySettings(new AppSettings { BackgroundWatcherEnabled = true });

        _processes.Running.Add(new RunningProcess(1, "Hades", @"D:\Games\Hades\Hades.exe"));
        _processes.Running.Add(new RunningProcess(2, "Celeste", @"D:\Games\Celeste\Celeste.exe"));
        await PollEvery5Seconds(24);
        _processes.Running.Clear();
        await PollEvery5Seconds(6);

        Assert.True(_library.FindGame(tracked.Id)!.TotalPlaytimeSeconds > 0);
        Assert.Equal(0, _library.FindGame(untracked.Id)!.TotalPlaytimeSeconds);
        Assert.Equal(SessionSource.BackgroundWatcher, Assert.Single(_db.Sessions.GetSessions()).Source);
    }

    [Fact]
    public async Task Games_without_a_known_location_cannot_be_tracked()
    {
        await _library.LoadAsync();
        var game = await _library.AddGameAsync(new Game { Name = "Unknown", LaunchMethod = LaunchMethod.LauncherUri, LaunchTarget = "steam://rungameid/5" });
        _tracking.ApplySettings(new AppSettings());
        Assert.False(TrackingService.IsTrackable(game));
        Assert.False(_tracking.NotifyLaunched(game));
    }
}
