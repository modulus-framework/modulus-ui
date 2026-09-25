using System.Globalization;

namespace Modulus.Samples.Shared;

/// <summary>Display formatting shared by every sample flavor (invariant culture, US-style money).</summary>
public static class Fmt
{
    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("en-US");

    public static string Money(decimal value) => value.ToString("$#,0.00;-$#,0.00", Culture);

    public static string Money0(decimal value) => value.ToString("$#,0;-$#,0", Culture);

    public static string Number(double value, string format = "N0") => value.ToString(format, Culture);

    public static string Date(DateTime value) => value.ToString("MMM d, yyyy", Culture);

    public static string DateTime(DateTime value) => value.ToString("MMM d, HH:mm", Culture);

    public static string Time(DateTime value) => value.ToString("HH:mm", Culture);

    public static string Percent(double value) => value.ToString("P0", Culture).Replace(" %", "%");

    public static string Delta(double percent) => (percent >= 0 ? "+" : "") + percent.ToString("0.0", Culture) + "%";

    /// <summary>"just now", "12 min ago", "3 h ago", "Yesterday", "4 days ago", then the date.</summary>
    public static string Relative(DateTime at)
    {
        var span = System.DateTime.Now - at;
        if (span.TotalSeconds < 0) return at.Date == System.DateTime.Today ? "Today, " + Time(at) : Date(at);
        if (span.TotalMinutes < 1) return "just now";
        if (span.TotalHours < 1) return $"{(int)span.TotalMinutes} min ago";
        if (span.TotalDays < 1) return $"{(int)span.TotalHours} h ago";
        if (span.TotalDays < 2) return "Yesterday";
        return span.TotalDays < 7 ? $"{(int)span.TotalDays} days ago" : Date(at);
    }

    /// <summary>value ÷ of as a CSS percentage number ("42.5"), for inline widths.</summary>
    public static string Pct(double value, double of) =>
        (of <= 0 ? 0 : Math.Clamp(value / of * 100, 0, 100)).ToString("0.##", CultureInfo.InvariantCulture);

    public static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));

    /// <summary>Stable avatar tint (1–8) for a name.</summary>
    public static int Tint(string name) => Math.Abs(name.Aggregate(17, (h, c) => h * 31 + c)) % 8 + 1;
}
