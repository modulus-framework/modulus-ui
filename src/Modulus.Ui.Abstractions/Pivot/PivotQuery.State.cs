using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Modulus.Ui.Abstractions.Pivot;

public sealed partial class PivotQuery<T>
{
    /// <summary>The state in the query string, checked against the fields; the default when the query has none.</summary>
    public PivotState State(IEnumerable<KeyValuePair<string, string?>> query) => PivotState.Parse(query) is { } state ? Normalize(state) : _default;

    /// <summary>
    /// Drops unknown group-bys and measures, a group-by used twice, levels beyond <see cref="MaxLevels"/> and a sort by
    /// an unknown measure. A date without an interval groups by month.
    /// </summary>
    public PivotState Normalize(PivotState state)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        IReadOnlyList<string> Clean(IReadOnlyList<string> groupBys) =>
            groupBys.Select(Canonical).OfType<string>().Where(used.Add).ToArray();
        var rows = Clean(state.Rows);
        var columns = Clean(state.Columns).Take(Math.Max(0, MaxLevels - rows.Count)).ToArray();
        rows = rows.Take(MaxLevels).ToArray();
        // The levels Expand all brings back: known, used once, below the axis's own, and within MaxLevels together.
        var fullRows = Full(state.RowsFull, rows, MaxLevels - columns.Length);
        var fullColumns = Full(state.ColumnsFull, columns, MaxLevels - fullRows.Length);
        string[] Full(IReadOnlyList<string> full, IReadOnlyList<string> groupBys, int max) =>
            full.Count > groupBys.Count && full.Take(groupBys.Count).Select(Canonical).SequenceEqual(groupBys)
                ? [.. groupBys.Concat(full.Skip(groupBys.Count).Select(Canonical).OfType<string>().Where(used.Add)).Take(Math.Max(groupBys.Count, max))]
                : [];
        var known = Measures.Select(m => m.Key).ToHashSet(StringComparer.Ordinal);
        // At most MaxMeasures shown, and at most MaxMeasures sums stored (a ratio stores its two measures).
        var measures = new List<string>();
        foreach (var m in state.Measures.Where(known.Contains).Distinct())
        {
            if (measures.Count == MaxMeasures) break;
            if (Stored([.. measures, m]).Count <= MaxMeasures) measures.Add(m);
        }
        if (measures.Count == 0) measures = _default.Measures.Count > 0 ? [.. _default.Measures] : [Count];
        var sorted = state.SortMeasure is { } s && measures.Contains(s);
        return state with
        {
            Rows = rows,
            Columns = columns,
            Measures = [.. measures],
            RowsFull = fullRows.Length > rows.Count ? fullRows : [],
            ColumnsFull = fullColumns.Length > columns.Length ? fullColumns : [],
            RowsAllDepth = Math.Clamp(state.RowsAllDepth, 0, rows.Count),
            ColumnsAllDepth = Math.Clamp(state.ColumnsAllDepth, 0, columns.Length),
            SortMeasure = sorted ? state.SortMeasure : null,
            SortColumn = sorted ? state.SortColumn : PivotPath.Root,
            SortDescending = sorted && state.SortDescending,
        };
    }

    /// <summary>A group-by key in its canonical form (<c>date</c> → <c>date:month</c>), or null when unknown.</summary>
    public string? Canonical(string groupBy)
    {
        var (name, interval) = Split(groupBy);
        var field = _fields.FirstOrDefault(f => f.Key == name);
        if (field is null) return null;
        if (!field.IsDate) return interval is null ? field.Key : null;
        if (interval is null) return $"{field.Key}:month";
        return Enum.TryParse<PivotInterval>(interval, true, out var i) && Enum.IsDefined(i) && !int.TryParse(interval, out _)
            ? $"{field.Key}:{i.ToString().ToLowerInvariant()}"
            : null;
    }

    /// <summary>The header title of a group-by (<c>Date: Month</c>).</summary>
    public string GroupByTitle(string groupBy)
    {
        var (name, interval) = Split(groupBy);
        var field = _fields.First(f => f.Key == name);
        return interval is null ? field.Title : $"{field.Title}: {CultureInfo.InvariantCulture.TextInfo.ToTitleCase(interval)}";
    }
}
