using System.Net;
using System.Net.Http;
using System.Text.Json;

namespace Arclight.Desktop.Services;

public sealed record UpdateCheckResult(bool Success, bool UpdateAvailable, string Message, string? ReleaseUrl);

/// <summary>
/// Checks GitHub Releases for a newer version. Only runs when the user asks (or enabled the
/// startup check); it sends no library data, just a request for the public release list.
/// </summary>
public sealed class UpdateChecker(HttpClient http)
{
    public async Task<UpdateCheckResult> CheckAsync(CancellationToken cancellationToken = default)
    {
        var url = $"https://api.github.com/repos/{AppInfo.RepositoryOwner}/{AppInfo.RepositoryName}/releases/latest";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Accept.ParseAdd("application/vnd.github+json");
            using var response = await http.SendAsync(request, cancellationToken);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new UpdateCheckResult(true, false, Loc.Get("Update.NoRelease"), AppInfo.RepositoryUrl + "/releases");
            }

            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var tag = json.RootElement.GetProperty("tag_name").GetString() ?? string.Empty;
            var page = json.RootElement.TryGetProperty("html_url", out var html) ? html.GetString() : null;

            if (Version.TryParse(tag.TrimStart('v', 'V').Split('-')[0], out var latest) && latest > AppInfo.Version)
            {
                return new UpdateCheckResult(true, true, Loc.Format("Update.Available", tag, AppInfo.DisplayVersion), page);
            }

            return new UpdateCheckResult(true, false, Loc.Format("Update.UpToDate", AppInfo.DisplayVersion), page);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException)
        {
            return new UpdateCheckResult(false, false, Loc.Get("Update.Offline"), null);
        }
    }
}
