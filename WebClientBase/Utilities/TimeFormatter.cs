using Metaplay.Core;

namespace WebClientBase.Utilities;

/// <summary>
/// Utility methods for formatting time values for display.
/// </summary>
public static class TimeFormatter
{
    /// <summary>
    /// Format a MetaDuration as a human-readable string.
    /// </summary>
    /// <param name="duration">The duration to format.</param>
    /// <returns>Formatted string (e.g., "2h 30m", "5m 20s", "45s").</returns>
    public static string FormatDuration(MetaDuration duration)
    {
        TimeSpan ts = duration.ToTimeSpan();
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    /// <summary>
    /// Format a TimeSpan as a human-readable string.
    /// </summary>
    /// <param name="ts">The timespan to format.</param>
    /// <returns>Formatted string (e.g., "2h 30m", "5m 20s", "45s").</returns>
    public static string FormatDuration(TimeSpan ts)
    {
        if (ts.TotalHours >= 1)
            return $"{(int)ts.TotalHours}h {ts.Minutes}m";
        if (ts.TotalMinutes >= 1)
            return $"{ts.Minutes}m {ts.Seconds}s";
        return $"{ts.Seconds}s";
    }

    /// <summary>
    /// Format a MetaTime as a human-readable date/time string.
    /// </summary>
    /// <param name="time">The time to format.</param>
    /// <param name="neverText">Text to display if time is Epoch (default: "Never").</param>
    /// <param name="format">DateTime format string (default: "yyyy-MM-dd HH:mm:ss").</param>
    /// <returns>Formatted date/time string or neverText if time is Epoch.</returns>
    public static string FormatDateTime(MetaTime time, string neverText = "Never", string format = "yyyy-MM-dd HH:mm:ss")
    {
        if (time == MetaTime.Epoch)
            return neverText;
        return time.ToDateTime().ToString(format);
    }

    /// <summary>
    /// Format a MetaTime as a relative time string (e.g., "5 minutes ago").
    /// </summary>
    /// <param name="time">The time to format.</param>
    /// <param name="relativeTo">The reference time (defaults to now).</param>
    /// <returns>Relative time string.</returns>
    public static string FormatRelative(MetaTime time, MetaTime? relativeTo = null)
    {
        if (time == MetaTime.Epoch)
            return "Never";

        MetaTime reference = relativeTo ?? MetaTime.Now;
        MetaDuration diff = reference - time;
        TimeSpan diffTs = diff.ToTimeSpan();

        if (diffTs.TotalSeconds < 0)
        {
            // Future time
            diff = time - reference;
            return FormatDuration(diff) + " from now";
        }

        if (diffTs.TotalSeconds < 60)
            return "Just now";

        return FormatDuration(diff) + " ago";
    }
}
