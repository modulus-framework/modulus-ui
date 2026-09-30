using System.Globalization;

namespace Modulus.Ui.Abstractions.Forms;

/// <summary>A named date range for a range picker's preset buttons ("Last 7 days", "This month" …). Both ends are inclusive.</summary>
public sealed record DateRangePreset(string Label, DateOnly From, DateOnly To);

/// <summary>
/// Date periods shared by the Blazor pickers and the MVC <c>app-date-picker</c> / <c>app-range-picker</c>: range presets,
/// and quarters as <c>yyyy-Qn</c> values (the MVC quarter picker posts them; Blazor's quarter picker shows the same text).
/// </summary>
public static class DatePeriods
{
    /// <summary>The usual reporting ranges, ending today: today, yesterday, last 7 and 30 days, this and last month, this quarter, this year.</summary>
    public static IReadOnlyList<DateRangePreset> Presets(DateOnly today)
    {
        var month = new DateOnly(today.Year, today.Month, 1);
        var quarter = QuarterStart(today);
        return
        [
            new("Today", today, today),
            new("Yesterday", today.AddDays(-1), today.AddDays(-1)),
            new("Last 7 days", today.AddDays(-6), today),
            new("Last 30 days", today.AddDays(-29), today),
            new("This month", month, month.AddMonths(1).AddDays(-1)),
            new("Last month", month.AddMonths(-1), month.AddDays(-1)),
            new("This quarter", quarter, quarter.AddMonths(3).AddDays(-1)),
            new("This year", new DateOnly(today.Year, 1, 1), new DateOnly(today.Year, 12, 31)),
        ];
    }

    /// <summary>1–4.</summary>
    public static int QuarterOf(DateOnly date) => (date.Month - 1) / 3 + 1;

    public static DateOnly QuarterStart(DateOnly date) => new(date.Year, (QuarterOf(date) - 1) * 3 + 1, 1);

    /// <summary>"2026-Q3".</summary>
    public static string Quarter(DateOnly date) => $"{date.Year.ToString("D4", CultureInfo.InvariantCulture)}-Q{QuarterOf(date)}";

    /// <summary>Reads "2026-Q3" (case-insensitive, optional spaces) as the first day of that quarter.</summary>
    public static bool TryParseQuarter(string? text, out DateOnly start)
    {
        start = default;
        var parts = (text ?? "").Replace(" ", "", StringComparison.Ordinal).Split('-');
        if (parts.Length != 2 || parts[1].Length != 2 || char.ToUpperInvariant(parts[1][0]) != 'Q') return false;
        if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var year) || year is < 1 or > 9999) return false;
        var q = parts[1][1] - '0';
        if (q is < 1 or > 4) return false;
        start = new DateOnly(year, (q - 1) * 3 + 1, 1);
        return true;
    }

    /// <summary>Every quarter from <paramref name="fromYear"/> to <paramref name="toYear"/>: ("2026-Q3", "Q3 2026").</summary>
    public static IReadOnlyList<(string Value, string Label)> Quarters(int fromYear, int toYear) =>
        Enumerable.Range(fromYear, Math.Max(0, toYear - fromYear + 1))
            .SelectMany(y => Enumerable.Range(1, 4).Select(q => ($"{y:D4}-Q{q}", $"Q{q} {y}")))
            .ToList();
}
