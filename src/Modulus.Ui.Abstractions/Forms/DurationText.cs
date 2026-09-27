using System.Globalization;
using System.Text.RegularExpressions;

namespace Modulus.Ui.Abstractions.Forms;

/// <summary>
/// Durations typed the way timesheets take them, stored as whole minutes: "1:30", "1.5" or "1,5" (hours),
/// "90m", "1h 30m", "1h30", "2h". A bare number is hours. Shown as "h:mm". Mirrored by <c>mDuration</c> in
/// modulus-widgets.js.
/// </summary>
public static partial class DurationText
{
    /// <summary>Upper bound: 9,999 hours.</summary>
    public const int MaxMinutes = 9999 * 60;

    public static bool TryParse(string? text, out int minutes)
    {
        minutes = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim().ToLowerInvariant();
        double total;

        if (Clock().Match(s) is { Success: true } clock)
            total = int.Parse(clock.Groups[1].Value, CultureInfo.InvariantCulture) * 60 + int.Parse(clock.Groups[2].Value, CultureInfo.InvariantCulture);
        else if (Hours().Match(s) is { Success: true } hours)
            total = Number(hours.Groups[1].Value) * 60;
        else if (Units().Match(s) is { Success: true } units && (units.Groups[1].Success || units.Groups[2].Success))
            total = (units.Groups[1].Success ? Number(units.Groups[1].Value) * 60 : 0) +
                    (units.Groups[2].Success ? int.Parse(units.Groups[2].Value, CultureInfo.InvariantCulture) : 0);
        else
            return false;

        if (total > MaxMinutes) return false;
        minutes = (int)Math.Round(total, MidpointRounding.AwayFromZero);
        return true;
    }

    public static int? Parse(string? text) => TryParse(text, out var m) ? m : null;

    /// <summary>"1:30" for 90; "" for null.</summary>
    public static string Format(int? minutes) =>
        minutes is not { } m ? "" : string.Create(CultureInfo.InvariantCulture, $"{m / 60}:{m % 60:00}");

    private static double Number(string s) => double.Parse(s.Replace(',', '.'), CultureInfo.InvariantCulture);

    [GeneratedRegex(@"^(\d{1,4}):([0-5]?\d)$")]
    private static partial Regex Clock();

    [GeneratedRegex(@"^(\d{1,4}(?:[.,]\d+)?)\s*(?:h|hr|hrs|hours?)?$")]
    private static partial Regex Hours();

    [GeneratedRegex(@"^(?:(\d{1,4}(?:[.,]\d+)?)\s*(?:h|hr|hrs|hours?))?\s*(?:(\d{1,6})\s*(?:m|min|mins|minutes?)?)?$")]
    private static partial Regex Units();
}
