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

    /// <summary>Column headers sort the level (ascending, descending, unsorted) and each has a column search.</summary>
    public bool Sortable { get; set; } = true;

    /// <summary>
    /// A drag handle on each row to reorder the page (not while a column sort is on). Save hands the rows over in the
    /// new order with <see cref="NestedGridChanges.Ordered"/> set; <see cref="NestedGridQuery.StoreOrder"/> keeps it.
    /// </summary>
    public bool RowDraggable { get; set; } = true;

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
/// filtered (column key → option value, from the level's <see cref="GridColumn.Filterable"/> columns), searched by
/// column (column key → query, see <see cref="ColumnSearch"/>) and sorted.
/// </summary>
public sealed record NestedGridRequest(int Level, string? ParentId, string? Keyword = null, string? SortField = null, bool Descending = false, int Page = 1, int PageSize = 5,
    IReadOnlyDictionary<string, string?>? Filters = null, IReadOnlyDictionary<string, string?>? Searches = null)
{
    /// <summary>
    /// The page size was picked by the user (not the level's default), so the level's URLs carry it
    /// (<c>size=20</c>). The page size itself is <see cref="PageSize"/>.
    /// </summary>
    public bool CustomPageSize { get; init; }

    /// <summary>The same request with no keyword, filters or column searches, back on page 1 (a Clear button).</summary>
    public NestedGridRequest Cleared() => this with { Keyword = null, Filters = null, Searches = null, Page = 1 };

    /// <summary>Whether a keyword, a filter or a column search is set.</summary>
    public bool Filtered => !string.IsNullOrWhiteSpace(Keyword) || ActiveFilters.Any() || ActiveSearches.Any();

    /// <summary>The column searches that have a value.</summary>
    public IEnumerable<KeyValuePair<string, string?>> ActiveSearches => (Searches ?? new Dictionary<string, string?>()).Where(s => !string.IsNullOrWhiteSpace(s.Value));

    /// <summary>The same request with one column search set (or cleared), back on page 1.</summary>
    public NestedGridRequest WithSearch(string key, string? value) => this with { Searches = ColumnSearch.With(Searches, key, value), Page = 1 };

    /// <summary>The sort of <paramref name="field"/> as a header shows it.</summary>
    public SortDirection SortOf(string field) => SortField != field ? SortDirection.None : Descending ? SortDirection.Descending : SortDirection.Ascending;

    /// <summary>The request after a click on <paramref name="field"/>'s header: ascending, descending, then unsorted; back on page 1.</summary>
    public NestedGridRequest WithSort(string field) => ColumnSearch.NextSort(SortOf(field)) switch
    {
        SortDirection.Ascending => this with { SortField = field, Descending = false, Page = 1 },
        SortDirection.Descending => this with { SortField = field, Descending = true, Page = 1 },
        _ => this with { SortField = null, Descending = false, Page = 1 },
    };

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
public sealed record NestedGridChanges(int Level, string? ParentId, IReadOnlyList<GridColumn> Columns, IReadOnlyList<GridRecord> Rows, IReadOnlyList<string> DeletedIds)
{
    /// <summary>
    /// <see cref="Rows"/> are in the order the user left them (no column sort was on), so the app can store that
    /// order (see <see cref="NestedGridQuery.StoreOrder"/>). False when a column sort decided it.
    /// </summary>
    public bool Ordered { get; init; }
}

/// <summary>Search, sort and paging over in-memory rows, by the grid's own formatting rules.</summary>
public static class NestedGridQuery
{
    public static NestedGridPage Apply(IEnumerable<GridRecord> rows, IReadOnlyList<GridColumn> columns, NestedGridRequest request)
    {
        IEnumerable<GridRecord> result = rows;
        // Matched in the current culture, the one the cells are displayed in ("$1,200" finds "$1,2").
        if (!string.IsNullOrWhiteSpace(request.Keyword) || request.Filters is not null || request.Searches is not null)
            result = result.Where(r => GridSchema.Matches(columns, r, request.Keyword, request.Filters, searches: request.Searches));
        if (request.SortField is { } field && GridSchema.Find(columns, field) is { } column)
            result = GridSchema.Sort(result, column, request.Descending);
        var list = result.ToList();
        var size = Math.Clamp(request.PageSize, 1, 200);
        var pages = Math.Max(1, (int)Math.Ceiling(list.Count / (double)size));
        var page = Math.Clamp(request.Page, 1, pages);
        return new NestedGridPage(list.Skip((page - 1) * size).Take(size).ToList(), list.Count);
    }

    /// <summary>
    /// Stores the order of a saved page in the level's full list: the page's rows take the slots they held (rows on
    /// other pages or filtered out keep theirs), and rows added on the page (<paramref name="added"/>, already in
    /// <paramref name="all"/>) join that block where they were placed. <paramref name="order"/> is the page's ids as saved.
    /// </summary>
    public static void StoreOrder(List<GridRecord> all, IReadOnlyList<string> order, IReadOnlySet<string> added)
    {
        var newRows = all.Where(r => added.Contains(r.Id)).ToList();
        if (newRows.Count > 0)
        {
            all.RemoveAll(r => added.Contains(r.Id));
            var pageIds = order.ToHashSet(StringComparer.Ordinal);
            var first = all.FindIndex(r => pageIds.Contains(r.Id));
            all.InsertRange(first < 0 ? all.Count : first, newRows);
        }
        GridSchema.Reorder(all, order, r => r.Id);
    }

    /// <summary>The page a request lands on once clamped to the rows there are.</summary>
    public static int ClampPage(NestedGridRequest request, int total) =>
        Math.Clamp(request.Page, 1, Math.Max(1, (int)Math.Ceiling(total / (double)Math.Max(1, request.PageSize))));
}
