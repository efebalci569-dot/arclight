namespace Arclight.Core.Interfaces;

public interface IClock
{
    DateTimeOffset UtcNow { get; }

    TimeZoneInfo LocalZone { get; }
}

public sealed class SystemClock : IClock
{
    public static SystemClock Instance { get; } = new();

    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;

    public TimeZoneInfo LocalZone => TimeZoneInfo.Local;
}
