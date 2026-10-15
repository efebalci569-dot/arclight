using System.Text.Json;
using System.Text.Json.Serialization;
using Arclight.Core.Data;
using Arclight.Core.Models;

namespace Arclight.Core.Services;

/// <summary>Exports everything Arclight stores about the user to a single readable JSON file.</summary>
public sealed class DataExportService(GameRepository games, CollectionRepository collections, SessionRepository sessions, SettingsRepository settings)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task ExportAsync(string filePath, string appVersion, CancellationToken cancellationToken = default)
    {
        var export = await Task.Run(() => new
        {
            Format = "arclight-export",
            FormatVersion = 1,
            AppVersion = appVersion,
            ExportedAt = DateTimeOffset.UtcNow,
            Settings = settings.LoadAppSettings(),
            Games = games.GetAll(),
            Collections = collections.GetAll(),
            Sessions = sessions.GetSessions(),
        }, cancellationToken);

        var temp = filePath + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, export, Options, cancellationToken);
        }

        File.Move(temp, filePath, overwrite: true);
    }
}
