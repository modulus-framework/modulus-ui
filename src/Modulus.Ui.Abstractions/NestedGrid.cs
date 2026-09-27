using System.Globalization;

namespace Modulus.Ui.Abstractions;

/// <summary>
/// One level of a nested grid (Blazor <c>NestedGrid</c>, MVC <c>app-nested-grid</c>): its columns, paging, search,
/// sorting and actions. Level 0 lists the top rows; level <c>n + 1</c> is the child table of a level <c>n</c> row.
/// </summary>
public sealed class NestedGridLevel
{
    /// <summary>Title of the level's tables ("Styles").</summary>
    public string Title { get; set; } = "";

    /// <summary>Columns when <see cref="ColumnSetField"/> picks none.</summary>
    public IReadOnlyList<GridColumn> Columns { get; set; } = [];

    /// <summary>
    /// A field of the <b>parent</b> row that picks this level's columns from <see cref="ColumnSets"/>, so different
    /// parents get different child columns (a Fabric row's children get Width and GSM, an Accessory row's get Size).
    /// </summary>
    public string? ColumnSetField { get; set; }

    /// <summary>Column sets by the parent's <see cref="ColumnSetField"/> value.</summary>
    public IReadOnlyDictionary<string, IReadOnlyList<GridColumn>> ColumnSets { get; set; } = new Dictionary<string, IReadOnlyList<GridColumn>>();

    public int PageSize { get; set; } = 5;

    /// <summary>Cells can be edited, and rows added and deleted (with <see cref="AllowAdd"/> / <see cref="AllowDelete"/>).</summary>
    public bool Editable { get; set; } = true;

    public bool AllowAdd { get; set; } = true;

    public bool AllowDelete { get; set; } = true;

    /// <summary>A keyword box that searches the text of every visible column.</summary>
    public bool Searchable { get; set; } = true;

    /// <summary>A "Sort by" choice over the level's columns.</summary>
    public bool Sortable { get; set; } = true;

    /// <summary>Rows of this level expand into the next level, when there is one.</summary>
    public bool Expandable { get; set; } = true;

    /// <summary>Only rows for which this returns true expand (default: all).</summary>
    public Func<GridRecord, bool>? RowExpandable { get; set; }
}

/// <summary>
/// The levels of a nested grid. The number of levels is the nesting limit: rows of the last level never expand.
/// </summary>
public sealed class NestedGridSettings
{
    public IReadOnlyList<NestedGridLevel> Levels { get; set; } = [];

    public NestedGridLevel? Level(int depth) => depth >= 0 && depth < Levels.Count ? Levels[depth] : null;

    /// <summary>The columns of level <paramref name="depth"/> under <paramref name="parent"/> (clones, safe to edit).</summary>
    public List<GridColumn> ColumnsFor(int depth, GridRecord? parent)
    {
        if (Level(depth) is not { } level) return [];
        var set = level.ColumnSetField is { } field && parent?[field] is { } key && level.ColumnSets.TryGetValue(key, out var picked)
            ? picked
            : level.Columns;
        return set.Select(c => c.Clone()).ToList();
    }

    /// <summary>Whether a row of level <paramref name="depth"/> can expand into the next level.</summary>
    public bool CanExpand(int depth, GridRecord row) =>
        Level(depth) is { Expandable: true } level && depth + 1 < Levels.Count && (level.RowExpandable?.Invoke(row) ?? true);
}

/// <summary>
/// What a nested grid asks for: one page of level <see cref="Level"/> under <see cref="ParentId"/>, searched,
/// filtered (column key → option value, from the level's <see cref="GridColumn.Filterable"/> columns) and sorted.
/// </summary>
public sealed record NestedGridRequest(int Level, string? ParentId, string? Keyword = null, string? SortField = null, bool Descending = false, int Page = 1, int PageSize = 5,
    IReadOnlyDictionary<string, string?>? Filters = null)
{
    /// <summary>The filters that have a value.</summary>
    public IEnumerable<KeyValuePair<string, string?>> ActiveFilters => (Filters ?? new Dictionary<string, string?>()).Where(f => !string.IsNullOrEmpty(f.Value));

    /// <summary>The same request with one filter set (or cleared, when <paramref name="value"/> is empty).</summary>
    public NestedGridRequest WithFilter(string key, string? value)
    {
        var filters = ActiveFilters.Where(f => f.Key != key).ToDictionary(f => f.Key, f => f.Value, StringComparer.Ordinal);
        if (!string.IsNullOrEmpty(value)) filters[key] = value;
        return this with { Filters = filters.Count == 0 ? null : filters, Page = 1 };
    }
}

/// <summary>A page of rows and the total that match the request.</summary>
public sealed record NestedGridPage(IReadOnlyList<GridRecord> Rows, int Total);

/// <summary>What Save hands the app: the rows on the page as edited (new rows keep their temporary ids) and the ids deleted.</summary>
public sealed record NestedGridChanges(int Level, string? ParentId, IReadOnlyList<GridColumn> Columns, IReadOnlyList<GridRecord> Rows, IReadOnlyList<string> DeletedIds);

/// <summary>Search, sort and paging over in-memory rows, by the grid's own formatting rules.</summary>
public static class NestedGridQuery
{
    public static NestedGridPage Apply(IEnumerable<GridRecord> rows, IReadOnlyList<GridColumn> columns, NestedGridRequest request)
    {
        IEnumerable<GridRecord> result = rows;
        if (!string.IsNullOrWhiteSpace(request.Keyword) || request.Filters is not null)
            result = result.Where(r => GridSchema.Matches(columns, r, request.Keyword, request.Filters, CultureInfo.InvariantCulture));
        if (request.SortField is { } field && GridSchema.Find(columns, field) is { } column)
        {
            result = result.OrderBy(r => SortKey(column, r[field]), request.Descending ? SortComparer.Desc : SortComparer.Asc);
        }
        var list = result.ToList();
        var size = Math.Clamp(request.PageSize, 1, 200);
        var pages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)size));
        var page = Math.Clamp(request.Page, 1, pages);
        return new NestedGridPage(list.Skip((page - 1) * size).Take(size).ToList(), list.Count);
    }

    /// <summary>The page a request lands on once clamped to the rows there are.</summary>
    public static int ClampPage(NestedGridRequest request, int total) =>
        Math.Clamp(request.Page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)Math.Max(1, request.PageSize))));

    // Numbers and dates sort by value, everything else by its displayed text; empty cells go last.
    private static IComparable? SortKey(GridColumn c, string? value) => c.Type switch
    {
        GridCellType.Number or GridCellType.Currency or GridCellType.Percent or GridCellType.Progress or GridCellType.Rating => GridSchema.Number(value),
        GridCellType.Date => GridSchema.Date(value),
        GridCellType.Switch => GridSchema.Flag(value) ? 1 : 0,
        _ => string.IsNullOrEmpty(value) ? null : GridSchema.Format(c, value, CultureInfo.InvariantCulture),
    };

    private sealed class SortComparer : IComparer<IComparable?>
    {
        public static readonly SortComparer Asc = new(false), Desc = new(true);

        private readonly bool _desc;

        private SortComparer(bool desc) => _desc = desc;

        public int Compare(IComparable? x, IComparable? y) => (x, y) switch
        {
            (null, null) => 0,
            (null, _) => 1,
            (_, null) => -1,
            (string a, string b) => (_desc ? -1 : 1) * string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase),
            _ => (_desc ? -1 : 1) * x.CompareTo(y),
        };
    }
}
