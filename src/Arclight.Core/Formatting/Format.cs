using System.Globalization;

namespace Arclight.Core.Formatting;

/// <summary>
/// The words and patterns <see cref="Format"/> uses. English by default; the desktop app swaps in
/// the strings for the user's language so durations and dates read naturally ("12,5 sa", "2 gün önce").
/// </summary>
public sealed record FormatStrings
{
    public static FormatStrings English { get; } = new();

    public CultureInfo Culture { get; init; } = CultureInfo.GetCultureInfo("en-US");
    public string NoPlaytime { get; init; } = "0 hours tracked";
    public string LessThanMinute { get; init; } = "< 1 min";
    public string Minutes { get; init; } = "{0} min";
    public string HoursMinutes { get; init; } = "{0} h {1} min";
    public string Hours { get; init; } = "{0} h";
    public string NotPlayed { get; init; } = "Not played";
    public string NeverPlayed { get; init; } = "Never played";
    public string JustNow { get; init; } = "Just now";
    public string MinutesAgo { get; init; } = "{0} min ago";
    public string HoursAgo { get; init; } = "{0} h ago";
    public string Yesterday { get; init; } = "Yesterday";
    public string DaysAgo { get; init; } = "{0} days ago";
    public string WeekAgo { get; init; } = "1 week ago";
    public string WeeksAgo { get; init; } = "{0} weeks ago";
    public string ShortDate { get; init; } = "d MMM";
    public string LongDate { get; init; } = "d MMM yyyy";
    public string LongDateTime { get; init; } = "d MMM yyyy, HH:mm";
}

/// <summary>Human friendly text for durations and dates used across the UI.</summary>
public static class Format
{
    /// <summary>Set by the host when the UI language changes.</summary>
    public static FormatStrings Strings { get; set; } = FormatStrings.English;

    private static CultureInfo Culture => Strings.Culture;

    public static string Playtime(long seconds)
    {
        var s = Strings;
        if (seconds <= 0)
        {
            return s.NoPlaytime;
        }

        if (seconds < 60)
        {
            return s.LessThanMinute;
        }

        var span = TimeSpan.FromSeconds(seconds);
        if (span.TotalHours < 1)
        {
            return string.Format(Culture, s.Minutes, (int)span.TotalMinutes);
        }

        return span.TotalHours < 10
            ? string.Format(Culture, s.HoursMinutes, (int)span.TotalHours, span.Minutes)
            : string.Format(Culture, s.Hours, span.TotalHours.ToString("#,0", Culture));
    }

    /// <summary>Compact form for cards: "12.5 h", "45 min", "Not played".</summary>
    public static string PlaytimeShort(long seconds)
    {
        var s = Strings;
        if (seconds <= 0)
        {
            return s.NotPlayed;
        }

        if (seconds < 3600)
        {
            return string.Format(Culture, s.Minutes, Math.Max(1, seconds / 60));
        }

        var hours = seconds / 3600d;
        return string.Format(Culture, s.Hours, hours.ToString(hours < 100 ? "0.#" : "#,0", Culture));
    }

    /// <summary>Hours with one decimal for statistics headlines.</summary>
    public static string Hours(long seconds) =>
        (seconds / 3600d).ToString(seconds >= 360_000 ? "#,0" : "#,0.#", Culture);

    public static string Relative(DateTimeOffset? when, DateTimeOffset now)
    {
        var s = Strings;
        if (when is null)
        {
            return s.NeverPlayed;
        }

        var delta = now - when.Value;
        if (delta < TimeSpan.FromMinutes(1)) return s.JustNow;
        if (delta < TimeSpan.FromHours(1)) return string.Format(Culture, s.MinutesAgo, (int)delta.TotalMinutes);
        if (delta < TimeSpan.FromDays(1)) return string.Format(Culture, s.HoursAgo, (int)delta.TotalHours);

        var local = when.Value.ToLocalTime();
        var days = (now.ToLocalTime().Date - local.Date).Days;
        if (days == 1) return s.Yesterday;
        if (days < 7) return string.Format(Culture, s.DaysAgo, days);
        if (days < 30) return days / 7 == 1 ? s.WeekAgo : string.Format(Culture, s.WeeksAgo, days / 7);
        return local.ToString(local.Year == now.Year ? s.ShortDate : s.LongDate, Culture);
    }

    public static string Date(DateTimeOffset? when) =>
        when is null ? "—" : when.Value.ToLocalTime().ToString(Strings.LongDate, Culture);

    public static string DateTime(DateTimeOffset? when) =>
        when is null ? "—" : when.Value.ToLocalTime().ToString(Strings.LongDateTime, Culture);

    public static string Count(int count, string singular, string? plural = null) =>
        $"{count.ToString("#,0", CultureInfo.InvariantCulture)} {(count == 1 ? singular : plural ?? singular + "s")}";

    public static string Bytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value.ToString(unit == 0 ? "0" : "0.#", Culture)} {units[unit]}";
    }
}
