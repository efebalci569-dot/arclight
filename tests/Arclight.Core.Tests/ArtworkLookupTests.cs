using System.Net;
using System.Text;
using System.Text.Json;
using Arclight.Core.Artwork;
using Arclight.Core.Models;
using Arclight.Core.Services;

namespace Arclight.Core.Tests;

public class SteamStoreTests
{
    private static JsonElement Search(params (string Type, long Id, string Name)[] items)
    {
        var json = JsonSerializer.Serialize(new { total = items.Length, items = items.Select(i => new { type = i.Type, id = i.Id, name = i.Name }) });
        return JsonDocument.Parse(json).RootElement;
    }

    [Theory]
    [InlineData("Assassin’s Creed® II", "assassins creed ii")]
    [InlineData("STAR WARS™ Jedi: Survivor", "star wars jedi survivor")]
    [InlineData("Pokémon  Legends", "pokemon legends")]
    [InlineData("Rock & Roll", "rock and roll")]
    [InlineData("  ", "")]
    public void Normalizes_titles(string title, string expected) => Assert.Equal(expected, SteamStore.NormalizeTitle(title));

    [Fact]
    public void Picks_the_exact_title_and_never_soundtracks_or_dlc()
    {
        var result = Search(("app", 440820, "Stardew Valley Soundtrack"), ("app", 413150, "Stardew Valley"), ("bundle", 1, "Stardew Valley"));
        Assert.Equal("413150", SteamStore.FindMatch("Stardew Valley", result));
    }

    [Fact]
    public void Ignores_symbols_and_case()
    {
        var result = Search(("app", 1172380, "STAR WARS Jedi: Fallen Order™"));
        Assert.Equal("1172380", SteamStore.FindMatch("Star Wars Jedi Fallen Order", result));
    }

    [Fact]
    public void Falls_back_to_the_same_title_without_an_edition_suffix()
    {
        Assert.Equal("1551360", SteamStore.FindMatch("Forza Horizon 5 Standard Edition", Search(("app", 1551360, "Forza Horizon 5"))));
        Assert.Equal("2", SteamStore.FindMatch("Forza Horizon 5", Search(("app", 1, "Forza Horizon 5 Deluxe Edition"), ("app", 2, "Forza Horizon 5"))));
    }

    [Fact]
    public void Different_games_do_not_match()
    {
        var result = Search(("app", 271590, "Grand Theft Auto V Legacy"), ("app", 3240220, "Grand Theft Auto V Enhanced"), ("app", 12210, "Grand Theft Auto IV"));
        Assert.Null(SteamStore.FindMatch("Grand Theft Auto V", result));
        Assert.Null(SteamStore.FindMatch("Roblox", Search()));
        Assert.Null(SteamStore.FindMatch("Roblox", JsonDocument.Parse("{}").RootElement));
    }

    [Fact]
    public void Cdn_artwork_uses_the_app_id()
    {
        var artwork = SteamStore.CdnArtwork("413150");
        Assert.Equal($"{SteamStore.Cdn}/413150/library_600x900.jpg", artwork.Single(a => a.Kind == ArtworkKind.Cover).Source);
        Assert.All(artwork, a => Assert.True(a.IsRemote));
    }
}

public class MicrosoftStoreTests
{
    private const string Lookup = """
        {
          "Products": [ {
            "ProductId": "9MWR1NC6VQ6L",
            "LocalizedProperties": [ {
              "ProductTitle": "Stardew Valley",
              "Images": [
                { "ImagePurpose": "Poster", "Uri": "//store-images.s-microsoft.com/image/apps.1.small", "Width": 360, "Height": 540 },
                { "ImagePurpose": "Poster", "Uri": "//store-images.s-microsoft.com/image/apps.2.large", "Width": 720, "Height": 1080 },
                { "ImagePurpose": "SuperHeroArt", "Uri": "https://store-images.s-microsoft.com/image/apps.3.hero", "Width": 1920, "Height": 1080 },
                { "ImagePurpose": "TitledHeroArt", "Uri": "//evil.example.com/image.jpg", "Width": 1920, "Height": 1080 },
                { "ImagePurpose": "Screenshot", "Uri": "//store-images.s-microsoft.com/image/apps.4.shot", "Width": 1920, "Height": 1080 }
              ]
            } ]
          } ]
        }
        """;

    [Fact]
    public void Reads_the_largest_poster_and_the_hero_art_from_the_store_image_server_only()
    {
        var artwork = MicrosoftStore.ReadArtwork(JsonDocument.Parse(Lookup).RootElement);

        Assert.Equal(
            [
                new ArtworkCandidate(ArtworkKind.Cover, "https://store-images.s-microsoft.com/image/apps.2.large?w=720"),
                new ArtworkCandidate(ArtworkKind.Background, "https://store-images.s-microsoft.com/image/apps.3.hero?w=1920"),
            ],
            artwork);
    }

    [Fact]
    public void Unknown_packages_have_no_artwork()
    {
        Assert.Empty(MicrosoftStore.ReadArtwork(JsonDocument.Parse("""{ "Products": [] }""").RootElement));
        Assert.Empty(MicrosoftStore.ReadArtwork(JsonDocument.Parse("""{ "Message": "Not found" }""").RootElement));
    }

    [Fact]
    public void Lookup_url_escapes_the_package_name() =>
        Assert.Contains("value=Some.Game_8wekyb3d8bbwe&", MicrosoftStore.LookupUrl("Some.Game_8wekyb3d8bbwe"), StringComparison.Ordinal);
}

/// <summary>Answers HTTP requests from a test instead of the network.</summary>
internal sealed class FakeHttp : HttpMessageHandler
{
    public List<string> Requests { get; } = [];

    public Func<Uri, HttpResponseMessage> Respond { get; set; } = _ => new HttpResponseMessage(HttpStatusCode.NotFound);

    public static HttpResponseMessage Json(string json) =>
        new(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Jpeg() =>
        new(HttpStatusCode.OK) { Content = new ByteArrayContent([0xFF, 0xD8, 0xFF, 0xE0, 0, 0x10]) { Headers = { ContentType = new("image/jpeg") } } };

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request.RequestUri!.AbsoluteUri);
        return Task.FromResult(Respond(request.RequestUri));
    }
}

public sealed class OnlineArtworkTests : IDisposable
{
    private readonly TempDatabase _db = new();
    private readonly FakeHttp _http = new();
    private readonly FakeClock _clock = new(Games.BaseTime);
    private readonly ArtworkService _artwork;

    public OnlineArtworkTests()
    {
        _artwork = new ArtworkService(new AppPaths(_db.Directory), _db.Settings, new HttpClient(_http), _clock);
    }

    public void Dispose() => _db.Dispose();

    private const string StardewSearch = """{ "total": 1, "items": [ { "type": "app", "id": 413150, "name": "Stardew Valley" } ] }""";

    [Fact]
    public async Task Xbox_games_use_the_microsoft_store_catalog()
    {
        _http.Respond = uri => uri.Host == "displaycatalog.mp.microsoft.com"
            ? FakeHttp.Json("""{ "Products": [ { "LocalizedProperties": [ { "Images": [ { "ImagePurpose": "Poster", "Uri": "//store-images.s-microsoft.com/image/p", "Width": 720 } ] } ] } ] }""")
            : throw new InvalidOperationException("Unexpected request " + uri);

        var artwork = await _artwork.FindOnlineAsync(GamePlatform.Xbox, "Concerned.Stardew_0c8vynj4cqe4e", "Stardew Valley");

        Assert.Equal("https://store-images.s-microsoft.com/image/p?w=720", Assert.Single(artwork).Source);
        Assert.Single(_http.Requests);
    }

    [Fact]
    public async Task Other_games_are_matched_by_name_in_the_steam_store()
    {
        _http.Respond = uri => uri.Host switch
        {
            "displaycatalog.mp.microsoft.com" => FakeHttp.Json("""{ "Products": [] }"""),
            "store.steampowered.com" => FakeHttp.Json(StardewSearch),
            _ => throw new InvalidOperationException("Unexpected request " + uri),
        };

        var artwork = await _artwork.FindOnlineAsync(GamePlatform.Xbox, "Unknown_123", "Stardew Valley");

        Assert.Equal(SteamStore.CdnArtwork("413150"), artwork);
        Assert.Contains(_http.Requests, r => r.Contains("term=Stardew%20Valley", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_lookup_that_finds_nothing_is_not_repeated_for_a_week()
    {
        _http.Respond = _ => FakeHttp.Json("""{ "total": 0, "items": [] }""");

        Assert.Empty(await _artwork.FindOnlineAsync(GamePlatform.Manual, null, "My Homebrew Game"));
        Assert.Empty(await _artwork.FindOnlineAsync(GamePlatform.Manual, null, "My Homebrew Game"));
        Assert.Single(_http.Requests);

        _clock.Advance(TimeSpan.FromDays(8));
        await _artwork.FindOnlineAsync(GamePlatform.Manual, null, "My Homebrew Game");
        Assert.Equal(2, _http.Requests.Count);
    }

    [Fact]
    public async Task Network_errors_are_retried_next_time()
    {
        _http.Respond = _ => throw new HttpRequestException("offline");
        Assert.Empty(await _artwork.FindOnlineAsync(GamePlatform.Gog, "1207658924", "Stardew Valley"));

        _http.Respond = _ => FakeHttp.Json(StardewSearch);
        Assert.NotEmpty(await _artwork.FindOnlineAsync(GamePlatform.Gog, "1207658924", "Stardew Valley"));
    }

    [Fact]
    public async Task Steam_games_are_never_searched_by_name()
    {
        Assert.Empty(await _artwork.FindOnlineAsync(GamePlatform.Steam, "413150", "Stardew Valley"));
        Assert.Empty(_http.Requests);
    }

    [Fact]
    public async Task Manual_games_get_a_cover_and_keep_images_the_user_picked()
    {
        var library = new LibraryService(_db.Games, _db.Collections, _db.Sessions, _db.Settings, _clock);
        await library.LoadAsync();
        var picked = Path.Combine(_db.Directory, "my-background.png");
        await File.WriteAllBytesAsync(picked, [1, 2, 3]);
        var game = await library.AddGameAsync(new Game { Name = "Stardew Valley", Platform = GamePlatform.Manual, BackgroundImagePath = picked });
        _http.Respond = uri => uri.Host == "store.steampowered.com" ? FakeHttp.Json(StardewSearch) : FakeHttp.Jpeg();
        var scans = new ScanService([], library, _artwork, _clock);

        await scans.FillMissingArtworkOnlineAsync();

        var updated = library.FindGame(game.Id)!;
        Assert.True(File.Exists(updated.CoverImagePath));
        Assert.Equal(picked, updated.BackgroundImagePath);
        Assert.Contains(_http.Requests, r => r.EndsWith("/413150/library_600x900.jpg", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Nothing_is_looked_up_when_downloads_are_off()
    {
        var library = new LibraryService(_db.Games, _db.Collections, _db.Sessions, _db.Settings, _clock);
        await library.LoadAsync();
        await library.UpdateSettingsAsync(s => s with { DownloadMissingArtwork = false });
        await library.AddGameAsync(new Game { Name = "Stardew Valley", Platform = GamePlatform.Manual });

        await new ScanService([], library, _artwork, _clock).FillMissingArtworkOnlineAsync();

        Assert.Empty(_http.Requests);
    }
}
