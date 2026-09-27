using System.Globalization;
using System.Text;

namespace Modulus.Ui.Abstractions.Forms;

/// <summary>
/// Parsing and formatting of the numbers people type into business fields (amounts, quantities, ranges).
/// Culture-invariant: "." is the decimal point and ",", spaces, apostrophes and underscores group thousands.
/// Accounting negatives in parentheses ("(1,250.00)") and a leading or trailing minus are accepted.
/// Mirrored by <c>mNum</c> in modulus-widgets.js, so the MVC fields format exactly like the Blazor ones.
/// </summary>
public static class NumberText
{
    /// <summary>Reads a typed number. False for empty text, letters, or more than one decimal point.</summary>
    public static bool TryParse(string? text, out decimal value)
    {
        value = 0;
        if (string.IsNullOrWhiteSpace(text)) return false;
        var s = text.Trim();
        var negative = false;
        if (s.Length > 1 && s[0] == '(' && s[^1] == ')') { negative = true; s = s[1..^1].Trim(); }
        if (s.StartsWith('-')) { negative = !negative; s = s[1..].Trim(); }
        else if (s.EndsWith('-')) { negative = !negative; s = s[..^1].Trim(); }
        else if (s.StartsWith('+')) s = s[1..].Trim();

        var digits = new StringBuilder(s.Length);
        var dot = false;
        foreach (var c in s)
        {
            if (char.IsAsciiDigit(c)) digits.Append(c);
            else if (c == '.' && !dot) { dot = true; digits.Append(c); }
            else if (c is ',' or ' ' or '\u00a0' or '\'' or '_') continue;
            else return false;
        }
        var number = digits.ToString();
        if (number.Length == 0 || number == ".") return false;
        if (!decimal.TryParse(number, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out value)) return false;
        if (negative) value = -value;
        return true;
    }

    /// <summary>The typed number, or null when the text is not one.</summary>
    public static decimal? Parse(string? text) => TryParse(text, out var v) ? v : null;

    /// <summary>Rounds half away from zero, the way invoices round.</summary>
    public static decimal Round(decimal value, int decimals) =>
        Math.Round(value, Math.Clamp(decimals, 0, 10), MidpointRounding.AwayFromZero);

    /// <summary>"1,234.50" for (1234.5, 2); "" for null. <paramref name="group"/> false drops the thousands separators.</summary>
    public static string Format(decimal? value, int decimals, bool group = true)
    {
        if (value is null) return "";
        var d = Math.Clamp(decimals, 0, 10);
        return Round(value.Value, d).ToString((group ? "#,0" : "0") + (d > 0 ? "." + new string('0', d) : ""), CultureInfo.InvariantCulture);
    }

    /// <summary>The value as posted and stored: invariant, no grouping, no trailing zeros ("1234.5"); "" for null.</summary>
    public static string Invariant(decimal? value) =>
        value is null ? "" : value.Value.ToString("0.############################", CultureInfo.InvariantCulture);
}
