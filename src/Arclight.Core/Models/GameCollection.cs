namespace Arclight.Core.Models;

/// <summary>A user defined group of games. Deleting a collection never deletes its games.</summary>
public sealed class GameCollection
{
    public long Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? CoverImagePath { get; set; }

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public List<long> GameIds { get; set; } = [];

    public GameCollection Clone()
    {
        var copy = (GameCollection)MemberwiseClone();
        copy.GameIds = [.. GameIds];
        return copy;
    }
}
