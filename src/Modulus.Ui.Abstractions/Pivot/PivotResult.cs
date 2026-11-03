using System.Globalization;

namespace Modulus.Ui.Abstractions.Pivot;

/// <summary>A row or column header: "Total" at the root, then one header per group, to the depth of the axis.</summary>
public sealed class PivotHeader
{
    internal PivotHeader(string path, string key, string label, int depth, string? groupBy, PivotHeader? parent)
    {
        Path = path;
        Key = key;
        Label = label;
        Depth = depth;
        GroupBy = groupBy;
        Parent = parent;
    }

    /// <summary>Names the header in links (<see cref="PivotPath"/>).</summary>
    public string Path { get; }

    /// <summary>The group's key as the query returned it (a number for dates and enums; empty for "None").</summary>
    public string Key { get; }

    public string Label { get; }

    /// <summary>0 for "Total", 1 for its groups, and so on.</summary>
    public int Depth { get; }

    /// <summary>The group-by this header is a group of; null for "Total".</summary>
    public string? GroupBy { get; }

    public PivotHeader? Parent { get; }

    /// <summary>Its groups, in order, when it is open (a closed header's groups are not read).</summary>
    public IReadOnlyList<PivotHeader> Children => _children;

    public bool Expanded { get; internal set; }

    internal List<PivotHeader> _children = [];

    internal Dictionary<string, PivotHeader> ByKey = new(StringComparer.Ordinal);

    /// <summary>The groups from "Total" down to this header, as the group-by, key and label of each.</summary>
    public IReadOnlyList<PivotGroupValue> Groups()
    {
        var list = new List<PivotGroupValue>();
        for (var h = this; h.GroupBy is not null; h = h.Parent!) list.Insert(0, new PivotGroupValue(h.GroupBy, h.Key, h.Label));
        return list;
    }

    internal bool ShowsChildren => Expanded && _children.Count > 0;
}

/// <summary>One group on the way to a header (a drill-down link filters by these).</summary>
public sealed record PivotGroupValue(string GroupBy, string Key, string Label);

/// <summary>A column of values: a column header that shows no groups, or the "Total" column at the end.</summary>
public sealed record PivotColumn(PivotHeader Header, bool Total);

/// <summary>A cell of the column header rows.</summary>
public sealed record PivotHeaderCell(PivotHeader Header, int ColSpan, int RowSpan, bool Total);

/// <summary>A value cell, for drill-down links: its row and column groups and the measure.</summary>
public sealed record PivotCell(PivotHeader Row, PivotColumn Column, string Measure)
{
    /// <summary>Every group the cell lies in, rows then columns.</summary>
    public IReadOnlyList<PivotGroupValue> Groups => Row.Groups().Concat(Column.Header.Groups()).ToArray();
}

/// <summary>
/// A computed pivot table: the header trees of both axes, their values, and the header layout that
/// <see cref="PivotHtml"/> and <see cref="PivotXlsx"/> draw.
/// </summary>
public sealed class PivotResult
{
    private readonly Dictionary<(string Row, string Column), Totals> _cells;
    private readonly string[] _stored;
    private readonly PivotAggregate[] _aggregates;
    private readonly IReadOnlyDictionary<string, (string Numerator, string Denominator)> _ratios;

    private PivotResult(PivotState state, IReadOnlyList<PivotFieldInfo> fields, IReadOnlyList<PivotMeasureInfo> allMeasures,
        IReadOnlyList<PivotMeasureInfo> measures, PivotHeader rows, PivotHeader columns, Dictionary<(string, string), Totals> cells,
        IReadOnlyList<string> stored, PivotAggregate[] aggregates, IReadOnlyDictionary<string, (string, string)> ratios,
        Func<string, string> groupByTitle, string? error)
    {
        State = state;
        Fields = fields;
        AllMeasures = allMeasures;
        Measures = measures;
        Rows = rows;
        Columns = columns;
        _cells = cells;
        _stored = [.. stored];
        _aggregates = aggregates;
        _ratios = ratios;
        GroupByTitle = groupByTitle;
        Error = error;
    }

    public PivotState State { get; }

    /// <summary>The fields a header can group by.</summary>
    public IReadOnlyList<PivotFieldInfo> Fields { get; }

    /// <summary>Every measure (the Measures menu).</summary>
    public IReadOnlyList<PivotMeasureInfo> AllMeasures { get; }

    /// <summary>The measures shown, in order.</summary>
    public IReadOnlyList<PivotMeasureInfo> Measures { get; }

    /// <summary>The "Total" row header and its groups.</summary>
    public PivotHeader Rows { get; }

    public PivotHeader Columns { get; }

    /// <summary>The title of a group-by (<c>Date: Month</c>).</summary>
    public Func<string, string> GroupByTitle { get; }

    /// <summary>Why the table could not be built (too many groups), or null.</summary>
    public string? Error { get; }

    /// <summary>Whether no record matched.</summary>
    public bool IsEmpty => !_cells.ContainsKey((PivotPath.Root, PivotPath.Root));

    /// <summary>
    /// The value of a measure for a row and column header (for the headers' groups), or null when no record falls in
    /// both. A ratio is its numerator over its denominator for that cell, null when the denominator is zero.
    /// </summary>
    public decimal? Value(string rowPath, string columnPath, string measure)
    {
        if (!_cells.TryGetValue((rowPath, columnPath), out var t)) return null;
        if (!_ratios.TryGetValue(measure, out var ratio)) return Base(t, measure);
        var numerator = Base(t, ratio.Numerator);
        var denominator = Base(t, ratio.Denominator);
        return numerator is { } n && denominator is { } d && d != 0 ? n / d : null;
    }

    private decimal? Base(Totals t, string measure)
    {
        if (measure == PivotQuery<object>.Count) return t.Count;
        var i = Array.IndexOf(_stored, measure);
        if (i < 0) return null;
        return _aggregates[i] switch
        {
            PivotAggregate.Average => t.Count == 0 ? null : t.Values[i] / t.Count,
            _ => t.Values[i],
        };
    }

    /// <summary>The group-bys a header's dropdown offers: every field not grouped yet (each unused interval of a date).</summary>
    public IReadOnlyList<(PivotFieldInfo Field, IReadOnlyList<string> GroupBys)> Choices()
    {
        if (State.Rows.Count + State.Columns.Count >= PivotQuery<object>.MaxLevels) return [];
        var used = State.Rows.Concat(State.Columns).ToHashSet(StringComparer.Ordinal);
        var list = new List<(PivotFieldInfo, IReadOnlyList<string>)>();
        foreach (var f in Fields)
        {
            var options = f.IsDate
                ? Enum.GetValues<PivotInterval>().Select(i => $"{f.Key}:{i.ToString().ToLowerInvariant()}").Where(g => !used.Contains(g)).ToArray()
                : used.Contains(f.Key) ? [] : new[] { f.Key };
            if (options.Length > 0) list.Add((f, options));
        }
        return list;
    }

    // Layout ---------------------------------------------------------------------------------------------------

    /// <summary>The columns of values, left to right: the column headers that show no groups, then "Total" when "Total" is open.</summary>
    public IReadOnlyList<PivotColumn> ValueColumns()
    {
        var list = new List<PivotColumn>();
        void Walk(PivotHeader h)
        {
            if (h.ShowsChildren) foreach (var c in h.Children) Walk(c);
            else list.Add(new PivotColumn(h, false));
        }
        Walk(Columns);
        if (Columns.ShowsChildren) list.Add(new PivotColumn(Columns, true));
        return list;
    }

    /// <summary>
    /// The column header rows above the measure row: an open header spans its groups, a closed one reaches down to the
    /// measure row, and the "Total" column's header sits under "Total".
    /// </summary>
    public IReadOnlyList<IReadOnlyList<PivotHeaderCell>> ColumnHeaderRows()
    {
        var m = Math.Max(1, Measures.Count);
        var leaves = ValueColumns().Where(c => !c.Total).ToArray();
        var depth = leaves.Max(c => c.Header.Depth);
        var rows = Enumerable.Range(0, depth + 1).Select(_ => new List<PivotHeaderCell>()).ToArray();
        int Walk(PivotHeader h)
        {
            if (!h.ShowsChildren)
            {
                rows[h.Depth].Add(new PivotHeaderCell(h, m, depth - h.Depth + 1, false));
                return 1;
            }
            var cell = rows[h.Depth].Count;
            rows[h.Depth].Add(null!);
            var count = h.Children.Sum(Walk);
            rows[h.Depth][cell] = new PivotHeaderCell(h, (count + (h.Depth == 0 ? 1 : 0)) * m, 1, false);
            return count;
        }
        Walk(Columns);
        if (Columns.ShowsChildren) rows[1].Add(new PivotHeaderCell(Columns, m, depth, true));
        return rows;
    }

    /// <summary>The row headers that show, top to bottom: "Total", then each open header's groups under it.</summary>
    public IReadOnlyList<PivotHeader> BodyRows()
    {
        var list = new List<PivotHeader>();
        void Walk(PivotHeader h)
        {
            list.Add(h);
            if (h.ShowsChildren) foreach (var c in h.Children) Walk(c);
        }
        Walk(Rows);
        return list;
    }

    /// <summary>A measure's value as the table shows it (group separators, the measure's decimals; blank for none).</summary>
    public string Format(decimal? value, PivotMeasureInfo measure)
    {
        if (value is not { } v) return "";
        var format = "N" + measure.Decimals.ToString(CultureInfo.InvariantCulture);
        return measure.Percent ? (v * 100).ToString(format, CultureInfo.CurrentCulture) + "%" : v.ToString(format, CultureInfo.CurrentCulture);
    }

    // Building -------------------------------------------------------------------------------------------------

    internal sealed class Totals(int measures)
    {
        public long Count;
        public readonly decimal[] Values = new decimal[measures];
    }

    internal static PivotResult TooMany(PivotState state, IReadOnlyList<PivotFieldInfo> fields, IReadOnlyList<PivotMeasureInfo> allMeasures,
        IReadOnlyList<PivotMeasureInfo> measures, int max, Func<string, string> groupByTitle) =>
        new(state, fields, allMeasures, measures, Root(), Root(), [], [], [], new Dictionary<string, (string, string)>(), groupByTitle,
            $"This table would show more than {max.ToString("N0", CultureInfo.CurrentCulture)} groups. Collapse a header or group by fewer fields.");

    private static PivotHeader Root() => new(PivotPath.Root, "", "Total", 0, null, null);

    /// <summary>
    /// Builds the table from the grouped queries: each group of a level is one cell (and makes the headers it lies
    /// under), with the values the database computed for it, so nothing is added up here.
    /// </summary>
    internal static PivotResult Build(PivotState state, IReadOnlyList<PivotFieldInfo> fields, IReadOnlyList<PivotMeasureInfo> allMeasures,
        IReadOnlyList<PivotMeasureInfo> measures, IReadOnlyList<string> stored, PivotAggregate[] aggregates,
        IReadOnlyDictionary<string, (string Numerator, string Denominator)> ratios,
        IReadOnlyList<(int RowDepth, int ColumnDepth, IReadOnlyList<PivotLeaf> Groups)> levels,
        Func<string, string, string> label, Func<string, string> groupByTitle, Func<string, string, string, int> order)
    {
        var rows = Root();
        var columns = Root();
        var cells = new Dictionary<(string, string), Totals>();
        var n = aggregates.Length;

        foreach (var (r, c, groups) in levels)
            foreach (var leaf in groups)
            {
                var row = rows;
                for (var i = 0; i < r; i++) row = Child(row, state.Rows[i], Slot(leaf.Key, i), label);
                var column = columns;
                for (var j = 0; j < c; j++) column = Child(column, state.Columns[j], Slot(leaf.Key, r + j), label);
                var t = new Totals(n) { Count = leaf.Count };
                for (var k = 0; k < n; k++) t.Values[k] = Value(leaf, k);
                cells[(row.Path, column.Path)] = t;
            }

        var result = new PivotResult(state, fields, allMeasures, measures, rows, columns, cells, stored, aggregates, ratios, groupByTitle, null);
        Finish(rows, state, PivotAxis.Rows, order, result);
        Finish(columns, state, PivotAxis.Columns, order, null);
        return result;
    }

    private static PivotHeader Child(PivotHeader parent, string groupBy, string key, Func<string, string, string> label)
    {
        if (parent.ByKey.TryGetValue(key, out var h)) return h;
        h = new PivotHeader(PivotPath.Child(parent.Path, key), key, label(groupBy, key), parent.Depth + 1, groupBy, parent);
        parent.ByKey[key] = h;
        parent._children.Add(h);
        return h;
    }

    /// <summary>Orders each header's groups (by the sort measure, for rows sorted by one) and marks the open headers.</summary>
    private static void Finish(PivotHeader h, PivotState state, PivotAxis axis, Func<string, string, string, int> order, PivotResult? sortBy)
    {
        h.Expanded = state.IsExpanded(axis, h.Path);
        if (h._children.Count == 0) return;
        var groupBy = h._children[0].GroupBy!;
        Comparison<PivotHeader> natural = (a, b) => order(groupBy, a.Key, b.Key);
        if (sortBy is not null && state.SortMeasure is { } measure)
        {
            var sign = state.SortDescending ? -1 : 1;
            h._children.Sort((a, b) =>
            {
                var va = sortBy.Value(a.Path, state.SortColumn, measure);
                var vb = sortBy.Value(b.Path, state.SortColumn, measure);
                var cmp = Nullable.Compare(va, vb) * sign;
                return cmp != 0 ? cmp : natural(a, b);
            });
        }
        else h._children.Sort(natural);
        foreach (var child in h._children) Finish(child, state, axis, order, sortBy);
    }

    private static decimal Value(PivotLeaf leaf, int i) => i switch
    {
        0 => leaf.V0,
        1 => leaf.V1,
        2 => leaf.V2,
        3 => leaf.V3,
        4 => leaf.V4,
        5 => leaf.V5,
        6 => leaf.V6,
        _ => leaf.V7,
    };

    /// <summary>A level's key as text: the text slot, or the number slot in invariant digits; empty for null.</summary>
    private static string Slot(PivotKey k, int i)
    {
        var (text, number) = i switch
        {
            0 => (k.S0, k.N0),
            1 => (k.S1, k.N1),
            2 => (k.S2, k.N2),
            3 => (k.S3, k.N3),
            4 => (k.S4, k.N4),
            5 => (k.S5, k.N5),
            6 => (k.S6, k.N6),
            _ => (k.S7, k.N7),
        };
        return text ?? (number is { } v ? v.ToString(CultureInfo.InvariantCulture) : "");
    }
}
