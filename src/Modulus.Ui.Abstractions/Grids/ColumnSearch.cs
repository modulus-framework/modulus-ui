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

/// <summary>A choice of a column's select in a table's filter row: its label and the column search it sets.</summary>
public sealed record ColumnChoice(string Label, string Query);

/// <summary>
/// A value in a column's checkbox list (the Excel-style filter menu): the text as displayed, an empty string for
/// blank cells, and how many rows have it (null when unknown, as on a server-paged table that doesn't count).
/// </summary>
public sealed record ColumnValue(string Text, int? Count = null);

/// <summary>
/// The per-column search of every table (data table, editable grid, nested grid; Blazor and MVC, mirrored by
/// modulus-widgets.js). The query is matched against the displayed text: it contains the query, <c>=x</c> is an exact
/// match and <c>!x</c> excludes. Number and date columns also take comparisons, the way ERP list filters do:
/// <c>&gt;1000</c>, <c>&lt;=5</c>, <c>=12</c>, <c>10..20</c>, <c>2026-01-01..2026-06-30</c> (open-ended ranges too).
/// A column's checkbox list writes a value list: <c>=a|b</c> is exactly one of the values and <c>!=a|b</c> none of
/// them (an empty value is a blank cell), whichever is shorter (<see cref="ValueQuery"/>).
/// </summary>
public static partial class ColumnSearch
{
    /// <summary>Query-string prefix of a column search: <c>?s.name=ada</c>.</summary>
    public const string Prefix = "s.";

    /// <summary>Longest query kept.</summary>
    public const int MaxLength = 100;

    /// <summary>Most column searches read from a query string.</summary>
    public const int MaxSearches = 20;

    /// <summary>Longest value list kept (<c>=a|b|…</c>, from a column's checkbox list).</summary>
    public const int MaxListLength = 2000;

    /// <summary>Most values a column's checkbox list shows.</summary>
    public const int MaxValues = 200;

    /// <summary>Separates the values of a value list. A value that contains it can't be listed.</summary>
    public const char ValueSeparator = '|';

    /// <summary>The label of blank cells in a column's checkbox list.</summary>
    public const string BlankText = "(Blanks)";

    /// <summary>Placeholder of a column's box in a table's filter row (under the headers).</summary>
    public static string FilterPlaceholder(ColumnKind kind) => kind switch
    {
        ColumnKind.Number => "e.g. >100 or 10..50",
        ColumnKind.Date => "e.g. 2027-01-01..2027-06-30",
        _ => "Filter…",
    };

    /// <summary>The empty choice of a column's select in the filter row ("Any status").</summary>
    public static string AnyText(string title) => $"Any {title.ToLowerInvariant()}";

    /// <summary>
    /// The choices of a filter-row select with the current search: a query that is not one of them (typed in the
    /// search box, or from a link) is kept as an extra choice, so the select shows what is applied.
    /// </summary>
    public static IReadOnlyList<ColumnChoice> WithCurrent(IReadOnlyList<ColumnChoice> choices, string? current) =>
        string.IsNullOrWhiteSpace(current) || choices.Any(c => c.Query == current.Trim()) ? choices : [.. choices, new ColumnChoice(Describe(current), current.Trim())];

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
        if (ValueList(q) is { } list) return list.Values.Contains(text.Trim()) != list.Exclude;
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
            var max = ValueList(v) is null ? MaxLength : MaxListLength;
            result[key] = v.Length > max ? v[..max] : v;
        }
        return result.Count == 0 ? null : result;
    }

    /// <summary>Whether <paramref name="key"/> is a well-formed column key (letters and digits), as a search or sort names it.</summary>
    public static bool IsKey(string key) => KeyPattern().IsMatch(key);

    /// <summary>The searches with one set (or removed, when <paramref name="value"/> is empty). Null when none remain.</summary>
    public static IReadOnlyDictionary<string, string?>? With(IReadOnlyDictionary<string, string?>? searches, string key, string? value)
    {
        var result = (searches ?? new Dictionary<string, string?>())
            .Where(s => s.Key != key && !string.IsNullOrWhiteSpace(s.Value))
            .ToDictionary(s => s.Key, s => s.Value, StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(value)) result[key] = value.Trim();
        return result.Count == 0 ? null : result;
    }

    /// <summary>
    /// The search of a column's checkbox list: null when every value is picked (no filter), otherwise the shorter of
    /// <c>=picked|…</c> and <c>!=unpicked|…</c>. <paramref name="values"/> are all the values the list shows; an
    /// empty <paramref name="picked"/> gives null too (the menu doesn't apply a list that hides every row).
    /// </summary>
    public static string? ValueQuery(IEnumerable<string> picked, IEnumerable<string> values)
    {
        var all = Distinct(values);
        var on = Distinct(picked);
        if (on.Count == 0 || all.All(v => on.Contains(v, StringComparer.CurrentCultureIgnoreCase))) return null;
        var off = all.Where(v => !on.Contains(v, StringComparer.CurrentCultureIgnoreCase)).ToList();
        var include = List("=", on);
        var exclude = List("!=", off);
        // "None of" only when it is shorter: a list of the picked values also leaves out values added later.
        return exclude.Length < include.Length ? exclude : include;
    }

    /// <summary>
    /// The values of a column's checkbox list that are checked for a search: all of them for no search or a typed
    /// one (contains, a comparison), the listed ones for <c>=a|b</c> or <c>=a</c>, the others for <c>!=a|b</c>.
    /// </summary>
    public static IReadOnlySet<string> Picked(string? query, IEnumerable<string> values)
    {
        var all = Distinct(values);
        var q = (query ?? "").Trim();
        var list = ValueList(q) ?? (q.Length > 1 && q[0] == '=' ? (Exclude: false, Values: Set([q[1..].Trim()])) : null);
        return Set(list is not { } l ? all : all.Where(v => l.Values.Contains(v) != l.Exclude));
    }

    /// <summary>Whether a search is a value list from a checkbox list (<c>=a|b</c>, <c>!=a|b</c>).</summary>
    public static bool IsValueList(string? query) => ValueList((query ?? "").Trim()) is not null;

    /// <summary>
    /// A search as a person reads it: a value list as its values ("Active, Pending", "Not Active", blanks as
    /// <see cref="BlankText"/>), anything else as typed.
    /// </summary>
    public static string Describe(string? query)
    {
        var q = (query ?? "").Trim();
        if (ValueList(q) is not { } list) return q;
        var text = string.Join(", ", list.Values.Select(v => v.Length == 0 ? BlankText : v));
        return list.Exclude ? "Not " + text : text;
    }

    /// <summary>
    /// The values of a checkbox list from the cells of a column (as displayed; blanks become one empty value), sorted
    /// with blanks last, each with its count, at most <see cref="MaxValues"/>. A value containing
    /// <see cref="ValueSeparator"/> is left out.
    /// </summary>
    public static IReadOnlyList<ColumnValue> ValuesOf(IEnumerable<string?> cells) =>
        [.. cells.Select(c => (c ?? "").Trim())
            .Where(c => c.IndexOf(ValueSeparator) < 0)
            .GroupBy(c => c, StringComparer.CurrentCultureIgnoreCase)
            .OrderBy(g => g.Key.Length == 0).ThenBy(g => g.Key, StringComparer.CurrentCultureIgnoreCase)
            .Take(MaxValues)
            .Select(g => new ColumnValue(g.First(), g.Count()))];

    /// <summary>The values of a checkbox list that contain the text typed in its search box (all of them for none).</summary>
    public static IEnumerable<ColumnValue> Narrow(IEnumerable<ColumnValue> values, string? text)
    {
        var t = (text ?? "").Trim();
        return t.Length == 0 ? values : values.Where(v => (v.Text.Length == 0 ? BlankText : v.Text).Contains(t, StringComparison.CurrentCultureIgnoreCase));
    }

    // "=a|b" (exact, one of) or "!=a|b" (none of): a "|" makes "=" a list; "!=a" alone is "not exactly a".
    private static (bool Exclude, HashSet<string> Values)? ValueList(string q)
    {
        var exclude = q.StartsWith("!=", StringComparison.Ordinal);
        if (exclude ? q.Length < 3 : q.Length < 2 || q[0] != '=' || q.IndexOf(ValueSeparator) < 0) return null;
        return (exclude, Set(q[(exclude ? 2 : 1)..].Split(ValueSeparator).Select(v => v.Trim())));
    }

    private static string List(string prefix, List<string> values) =>
        // One blank alone still needs a separator to read as a list: "=|".
        prefix + (values is [""] ? ValueSeparator.ToString() : string.Join(ValueSeparator, values));

    private static List<string> Distinct(IEnumerable<string> values) =>
        values.Select(v => (v ?? "").Trim()).Distinct(StringComparer.CurrentCultureIgnoreCase).ToList();

    private static HashSet<string> Set(IEnumerable<string> values) => new(values, StringComparer.CurrentCultureIgnoreCase);

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
