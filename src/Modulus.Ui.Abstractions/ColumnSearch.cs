using System.Globalization;
using System.Text.RegularExpressions;

namespace Modulus.Ui.Abstractions;

/// <summary>How a column search compares a column's values.</summary>
public enum ColumnKind
{
    Text,
    Number,
    Date,
}

/// <summary>
/// The per-column search of every table (data table, editable grid, nested grid; Blazor and MVC, mirrored by
/// modulus-widgets.js). The query is matched against the displayed text: it contains the query, <c>=x</c> is an exact
/// match and <c>!x</c> excludes. Number and date columns also take comparisons, the way ERP list filters do:
/// <c>&gt;1000</c>, <c>&lt;=5</c>, <c>=12</c>, <c>10..20</c>, <c>2026-01-01..2026-06-30</c> (open-ended ranges too).
/// </summary>
public static partial class ColumnSearch
{
    /// <summary>Query-string prefix of a column search: <c>?s.name=ada</c>.</summary>
    public const string Prefix = "s.";

    /// <summary>Longest query kept.</summary>
    public const int MaxLength = 100;

    /// <summary>Most column searches read from a query string.</summary>
    public const int MaxSearches = 20;

    /// <summary>The help line under the search box.</summary>
    public static string Hint(ColumnKind kind) => kind switch
    {
        ColumnKind.Number => "Contains, or >100, <=50, 10..20",
        ColumnKind.Date => "Contains, or >2026-01-01, 2026-01-01..2026-06-30",
        _ => "Contains, =exact or !exclude",
    };

    /// <summary>Whether a cell matches a query.</summary>
    /// <param name="text">The cell as displayed.</param>
    /// <param name="number">The value of a <see cref="ColumnKind.Number"/> cell.</param>
    /// <param name="date">The value of a <see cref="ColumnKind.Date"/> cell.</param>
    public static bool Matches(string? query, string? text, ColumnKind kind = ColumnKind.Text, decimal? number = null, DateOnly? date = null)
    {
        var q = (query ?? "").Trim();
        if (q.Length == 0) return true;
        text ??= "";
        var compared = kind switch
        {
            ColumnKind.Number => Compare(q, number, ParseNumber),
            ColumnKind.Date => Compare(q, date, ParseDate),
            _ => null,
        };
        if (compared is { } result) return result;
        if (q.Length > 1 && q[0] == '=') return string.Equals(text.Trim(), q[1..].Trim(), StringComparison.CurrentCultureIgnoreCase);
        if (q.Length > 1 && q[0] == '!') return !text.Contains(q[1..].Trim(), StringComparison.CurrentCultureIgnoreCase);
        return text.Contains(q, StringComparison.CurrentCultureIgnoreCase);
    }

    /// <summary>
    /// The column searches in a query string (<c>s.{key}=value</c>): keys of letters and digits, non-empty values,
    /// at most <see cref="MaxSearches"/>, each cut to <see cref="MaxLength"/>. Null when there are none.
    /// </summary>
    public static IReadOnlyDictionary<string, string?>? FromQuery(IEnumerable<KeyValuePair<string, string?>> query)
    {
        var result = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var (name, value) in query)
        {
            if (result.Count == MaxSearches) break;
            if (!name.StartsWith(Prefix, StringComparison.Ordinal) || string.IsNullOrWhiteSpace(value)) continue;
            var key = name[Prefix.Length..];
            if (!KeyPattern().IsMatch(key)) continue;
            var v = value.Trim();
            result[key] = v.Length > MaxLength ? v[..MaxLength] : v;
        }
        return result.Count == 0 ? null : result;
    }

    /// <summary>The searches with one set (or removed, when <paramref name="value"/> is empty). Null when none remain.</summary>
    public static IReadOnlyDictionary<string, string?>? With(IReadOnlyDictionary<string, string?>? searches, string key, string? value)
    {
        var result = (searches ?? new Dictionary<string, string?>())
            .Where(s => s.Key != key && !string.IsNullOrWhiteSpace(s.Value))
            .ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(value)) result[key] = value.Trim();
        return result.Count == 0 ? null : result;
    }

    /// <summary>The next sort of a header click: ascending, then descending, then none.</summary>
    public static SortDirection NextSort(SortDirection current) => current switch
    {
        SortDirection.None => SortDirection.Ascending,
        SortDirection.Ascending => SortDirection.Descending,
        _ => SortDirection.None,
    };

    // A comparison (">5", "<=5", "=5", "5..9", "..9", "5..") of this kind, or null to match the query as text.
    private static bool? Compare<T>(string q, T? value, Func<string, T?> parse) where T : struct, IComparable<T>
    {
        var range = q.IndexOf("..", StringComparison.Ordinal);
        if (range >= 0)
        {
            var left = q[..range].Trim();
            var right = q[(range + 2)..].Trim();
            T? from = left.Length == 0 ? null : parse(left);
            T? to = right.Length == 0 ? null : parse(right);
            if ((left.Length > 0 && from is null) || (right.Length > 0 && to is null) || (from is null && to is null)) return null;
            return value is { } v && (from is not { } f || v.CompareTo(f) >= 0) && (to is not { } t || v.CompareTo(t) <= 0);
        }
        var op = q.StartsWith(">=", StringComparison.Ordinal) || q.StartsWith("<=", StringComparison.Ordinal) ? q[..2]
            : q[0] is '>' or '<' or '=' ? q[..1]
            : null;
        if (op is null || parse(q[op.Length..].Trim()) is not { } operand) return null;
        if (value is not { } x) return false;
        var c = x.CompareTo(operand);
        return op switch
        {
            ">" => c > 0,
            ">=" => c >= 0,
            "<" => c < 0,
            "<=" => c <= 0,
            _ => c == 0,
        };
    }

    // Digits, a point and a minus sign; currency symbols and group separators are ignored ("$1,200" is 1200).
    private static decimal? ParseNumber(string s)
    {
        var digits = NotNumber().Replace(s, "");
        return digits.Length > 0 && decimal.TryParse(digits, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
    }

    private static DateOnly? ParseDate(string s) =>
        DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d) ? d : null;

    [GeneratedRegex("^[A-Za-z0-9]{1,40}$")]
    private static partial Regex KeyPattern();

    [GeneratedRegex("[^0-9.\\-]")]
    private static partial Regex NotNumber();
}
