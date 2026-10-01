using System.Globalization;
using System.Text;

namespace Modulus.Ui.Abstractions.Pivot;

/// <summary>The rows or the columns of a pivot table.</summary>
public enum PivotAxis
{
    Rows,
    Columns,
}

/// <summary>
/// Header paths: the root ("Total") is <c>*</c>, and each group adds <c>/</c> and its key, escaped
/// (<c>*/Berlin/2024</c>). A path names one header on its axis, so it can travel in a URL.
/// </summary>
public static class PivotPath
{
    public const string Root = "*";

    public static string Child(string path, string key) => path + "/" + Uri.EscapeDataString(key);

    /// <summary>How many groups deep the header is (the root is 0).</summary>
    public static int Depth(string path) => path.Count(c => c == '/');

    /// <summary>Whether <paramref name="path"/> is <paramref name="ancestor"/> or lies under it.</summary>
    public static bool Within(string path, string ancestor) =>
        path == ancestor || path.StartsWith(ancestor + "/", StringComparison.Ordinal);

    /// <summary>The group keys of the path, root first excluded.</summary>
    public static IReadOnlyList<string> Keys(string path) =>
        path == Root ? [] : path[(Root.Length + 1)..].Split('/').Select(Uri.UnescapeDataString).ToArray();

    /// <summary>Whether the text is a well-formed path (it comes from a query string).</summary>
    public static bool IsValid(string? path) => path is not null && (path == Root || path.StartsWith(Root + "/", StringComparison.Ordinal));
}

/// <summary>
/// Everything a pivot table shows, as it travels in the query string, in the manner of Odoo's pivot view:
/// the group-bys of each axis (<c>rows=city,customer&amp;cols=date:year</c>), the measures, which headers are expanded,
/// and the sort. Each change (expand, collapse, flip, expand all, a measure, a sort) is a new state, so every
/// control of the table is a plain link.
/// </summary>
/// <remarks>
/// A header is expanded when its path is listed. <see cref="ExpandAll"/> opens every header down to the levels the axis
/// has then (<see cref="RowsAllDepth"/>); above that depth the list names the collapsed headers instead, so the URL stays
/// short however many groups there are, and a group-by added later still opens only the header it was picked on.
/// </remarks>
public sealed record PivotState
{
    /// <summary>Group-bys of the rows, outermost first: a field key, or <c>field:interval</c> for a date.</summary>
    public IReadOnlyList<string> Rows { get; init; } = [];

    public IReadOnlyList<string> Columns { get; init; } = [];

    /// <summary>Measure keys, in the order the columns show them; <c>count</c> is the record count.</summary>
    public IReadOnlyList<string> Measures { get; init; } = [];

    /// <summary>Row headers less deep than this are expanded unless <see cref="RowPaths"/> lists them (0: none).</summary>
    public int RowsAllDepth { get; init; }

    /// <summary>
    /// Row headers: collapsed ones above <see cref="RowsAllDepth"/>, expanded ones from that depth on.
    /// </summary>
    public IReadOnlyList<string> RowPaths { get; init; } = [];

    public int ColumnsAllDepth { get; init; }

    public IReadOnlyList<string> ColumnPaths { get; init; } = [];

    /// <summary>The measure the rows are sorted by (within each group), or null for the groups' natural order.</summary>
    public string? SortMeasure { get; init; }

    /// <summary>The column header whose values sort the rows.</summary>
    public string SortColumn { get; init; } = PivotPath.Root;

    public bool SortDescending { get; init; }

    public IReadOnlyList<string> GroupBys(PivotAxis axis) => axis == PivotAxis.Rows ? Rows : Columns;

    private int AllDepth(PivotAxis axis) => axis == PivotAxis.Rows ? RowsAllDepth : ColumnsAllDepth;

    private IReadOnlyList<string> Paths(PivotAxis axis) => axis == PivotAxis.Rows ? RowPaths : ColumnPaths;

    /// <summary>Whether the header shows its groups (it has a group-by below it and is open).</summary>
    public bool IsExpanded(PivotAxis axis, string path)
    {
        var depth = PivotPath.Depth(path);
        if (depth >= GroupBys(axis).Count) return false;
        return depth < AllDepth(axis) ? !Paths(axis).Contains(path) : Paths(axis).Contains(path);
    }

    /// <summary>Opens a header on the next group-by of its axis.</summary>
    public PivotState Expand(PivotAxis axis, string path)
    {
        var paths = PivotPath.Depth(path) < AllDepth(axis) ? Paths(axis).Where(p => p != path).ToArray() : Paths(axis).Append(path).Distinct().ToArray();
        return With(axis, GroupBys(axis), AllDepth(axis), paths);
    }

    /// <summary>
    /// Opens a header of the last level by a new group-by (the header's dropdown): the group-by is added to its axis,
    /// below the header's own level.
    /// </summary>
    public PivotState ExpandBy(PivotAxis axis, string path, string groupBy)
    {
        var groupBys = GroupBys(axis).Take(PivotPath.Depth(path)).Append(groupBy).ToArray();
        return With(axis, groupBys, Math.Min(AllDepth(axis), groupBys.Length), Paths(axis)).Expand(axis, path);
    }

    /// <summary>
    /// Closes a header. As in Odoo, the axis keeps only the group-bys that an open header still uses, so a collapsed
    /// "Total" starts over with the group-by dropdown.
    /// </summary>
    public PivotState Collapse(PivotAxis axis, string path)
    {
        var all = AllDepth(axis);
        if (path == PivotPath.Root) return With(axis, [], 0, []);
        // Headers under the collapsed one forget their state: opened again, it shows its groups closed.
        var rest = Paths(axis).Where(p => !PivotPath.Within(p, path));
        if (PivotPath.Depth(path) < all) return With(axis, GroupBys(axis), all, rest.Append(path).ToArray());
        var paths = rest.ToArray();
        var height = Math.Max(all, paths.Where(p => PivotPath.Depth(p) >= all).Select(p => PivotPath.Depth(p) + 1).DefaultIfEmpty(0).Max());
        return With(axis, GroupBys(axis).Take(height).ToArray(), all, paths);
    }

    /// <summary>Opens every header of both axes on all their group-bys.</summary>
    public PivotState ExpandAll() => this with { RowsAllDepth = Rows.Count, RowPaths = [], ColumnsAllDepth = Columns.Count, ColumnPaths = [] };

    /// <summary>Rows become columns and columns rows.</summary>
    public PivotState Flip() => this with
    {
        Rows = Columns, Columns = Rows,
        RowsAllDepth = ColumnsAllDepth, ColumnsAllDepth = RowsAllDepth,
        RowPaths = ColumnPaths, ColumnPaths = RowPaths,
        SortMeasure = null, SortColumn = PivotPath.Root, SortDescending = false,
    };

    /// <summary>Adds or removes a measure; the last one stays.</summary>
    public PivotState ToggleMeasure(string key)
    {
        if (!Measures.Contains(key)) return this with { Measures = Measures.Append(key).ToArray() };
        if (Measures.Count == 1) return this;
        var state = this with { Measures = Measures.Where(m => m != key).ToArray() };
        return SortMeasure == key ? state with { SortMeasure = null, SortColumn = PivotPath.Root, SortDescending = false } : state;
    }

    /// <summary>Sorts the rows by a measure of a column header: ascending first, then the other way on each click.</summary>
    public PivotState SortBy(string measure, string column) =>
        SortMeasure == measure && SortColumn == column
            ? this with { SortDescending = !SortDescending }
            : this with { SortMeasure = measure, SortColumn = column, SortDescending = false };

    private PivotState With(PivotAxis axis, IReadOnlyList<string> groupBys, int allDepth, IReadOnlyList<string> paths)
    {
        var state = axis == PivotAxis.Rows
            ? this with { Rows = groupBys, RowsAllDepth = allDepth, RowPaths = paths }
            : this with { Columns = groupBys, ColumnsAllDepth = allDepth, ColumnPaths = paths };
        // A sort by a column header that no longer shows is dropped.
        return axis == PivotAxis.Columns && state.SortMeasure is not null && !state.Shows(PivotAxis.Columns, state.SortColumn)
            ? state with { SortMeasure = null, SortColumn = PivotPath.Root, SortDescending = false }
            : state;
    }

    /// <summary>Whether a header shows: every header above it is open.</summary>
    internal bool Shows(PivotAxis axis, string path)
    {
        if (path == PivotPath.Root) return true;
        var parent = path[..path.LastIndexOf('/')];
        return Shows(axis, parent) && IsExpanded(axis, parent);
    }

    // Query string ------------------------------------------------------------------------------------------------

    /// <summary>The query keys a pivot state uses; other keys (the page's own filters) are left alone.</summary>
    public static readonly IReadOnlyList<string> QueryKeys = ["rows", "cols", "measures", "rx", "ra", "cx", "ca", "sort", "sortcol", "desc"];

    /// <summary>The state as query values (lists joined by commas, paths repeated).</summary>
    public IEnumerable<KeyValuePair<string, string>> ToQuery()
    {
        yield return new("rows", string.Join(',', Rows));
        yield return new("cols", string.Join(',', Columns));
        yield return new("measures", string.Join(',', Measures));
        if (RowsAllDepth > 0) yield return new("ra", Invariant(RowsAllDepth));
        foreach (var p in RowPaths) yield return new("rx", p);
        if (ColumnsAllDepth > 0) yield return new("ca", Invariant(ColumnsAllDepth));
        foreach (var p in ColumnPaths) yield return new("cx", p);
        if (SortMeasure is not null)
        {
            yield return new("sort", SortMeasure);
            if (SortColumn != PivotPath.Root) yield return new("sortcol", SortColumn);
            if (SortDescending) yield return new("desc", "1");
        }
    }

    /// <summary>The state as an encoded query string, without the <c>?</c>.</summary>
    public string ToQueryString() => Encode(ToQuery());

    /// <summary>Encodes query values (<c>a=1&amp;b=x%20y</c>).</summary>
    public static string Encode(IEnumerable<KeyValuePair<string, string>> values)
    {
        var sb = new StringBuilder();
        foreach (var (key, value) in values)
        {
            if (sb.Length > 0) sb.Append('&');
            sb.Append(Uri.EscapeDataString(key)).Append('=').Append(Uri.EscapeDataString(value));
        }
        return sb.ToString();
    }

    /// <summary>
    /// A link to <paramref name="state"/> on the page at <paramref name="path"/>: the page's other query values (its
    /// filters) stay, the pivot's own keys are replaced.
    /// </summary>
    public static string Link(string path, IEnumerable<KeyValuePair<string, string?>> query, PivotState state)
    {
        var kept = query.Where(q => !QueryKeys.Contains(q.Key) && !string.IsNullOrEmpty(q.Value)).Select(q => new KeyValuePair<string, string>(q.Key, q.Value!));
        return path + "?" + Encode(kept.Concat(state.ToQuery()));
    }

    /// <summary>The values of a query string (with or without its <c>?</c>), in order; a key may repeat.</summary>
    public static IEnumerable<KeyValuePair<string, string?>> ReadQuery(string? query)
    {
        if (string.IsNullOrEmpty(query)) yield break;
        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = part.IndexOf('=');
            static string Decode(string s) => Uri.UnescapeDataString(s.Replace('+', ' '));
            yield return eq < 0 ? new(Decode(part), "") : new(Decode(part[..eq]), Decode(part[(eq + 1)..]));
        }
    }

    /// <summary>
    /// Reads a state from query values, or returns null when the query has none (the page then shows its default).
    /// Nothing is checked against the fields here; <see cref="PivotQuery{T}.State"/> does that.
    /// </summary>
    public static PivotState? Parse(IEnumerable<KeyValuePair<string, string?>> query)
    {
        var values = query.Where(q => QueryKeys.Contains(q.Key)).ToList();
        if (!values.Any(q => q.Key is "rows" or "cols" or "measures")) return null;
        string? One(string key) => values.LastOrDefault(q => q.Key == key).Value;
        IReadOnlyList<string> List(string key) => (One(key) ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        static int Depth(string? text) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var d) ? Math.Min(d, 16) : 0;
        IReadOnlyList<string> PathsOf(string key) => values.Where(q => q.Key == key && PivotPath.IsValid(q.Value)).Select(q => q.Value!).Distinct().ToArray();
        return new PivotState
        {
            Rows = List("rows"),
            Columns = List("cols"),
            Measures = List("measures"),
            RowsAllDepth = Depth(One("ra")),
            RowPaths = PathsOf("rx"),
            ColumnsAllDepth = Depth(One("ca")),
            ColumnPaths = PathsOf("cx"),
            SortMeasure = string.IsNullOrWhiteSpace(One("sort")) ? null : One("sort"),
            SortColumn = PivotPath.IsValid(One("sortcol")) ? One("sortcol")! : PivotPath.Root,
            SortDescending = One("desc") == "1",
        };
    }

    public override string ToString() => ToQueryString();

    internal static string Invariant(long value) => value.ToString(CultureInfo.InvariantCulture);
}
