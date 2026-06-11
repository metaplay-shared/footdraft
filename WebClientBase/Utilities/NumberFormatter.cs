namespace WebClientBase.Utilities;

/// <summary>
/// Utility methods for formatting numbers for display.
/// </summary>
public static class NumberFormatter
{
    /// <summary>
    /// Format a large number with K, M, B abbreviations.
    /// </summary>
    /// <param name="value">The number to format.</param>
    /// <returns>Formatted string (e.g., "1.5K", "2.3M", "1B").</returns>
    public static string FormatAbbreviated(long value)
    {
        if (value >= 1_000_000_000) return $"{value / 1_000_000_000.0:0.#}B";
        if (value >= 1_000_000) return $"{value / 1_000_000.0:0.#}M";
        if (value >= 1_000) return $"{value / 1_000.0:0.#}K";
        return value.ToString();
    }

    /// <summary>
    /// Format a large number with K, M, B abbreviations.
    /// </summary>
    /// <param name="value">The number to format.</param>
    /// <returns>Formatted string (e.g., "1.5K", "2.3M", "1B").</returns>
    public static string FormatAbbreviated(int value) => FormatAbbreviated((long)value);

    /// <summary>
    /// Format a rate value with appropriate decimal places.
    /// </summary>
    /// <param name="rate">The rate to format.</param>
    /// <returns>Formatted string with 1 decimal for rates >= 1, 2 decimals otherwise.</returns>
    public static string FormatRate(double rate) => rate >= 1 ? rate.ToString("0.#") : rate.ToString("0.##");
}
