namespace Modulus.Ui.Abstractions;

public enum SortDirection
{
    None,
    Ascending,
    Descending,
}

/// <summary>
/// Paging/sorting request for a server-driven table. Blazor's ProTable builds it from
/// table state; MVC endpoints model-bind it from the query string
/// (<c>?current=2&amp;pageSize=20&amp;sortField=name&amp;sortOrder=Ascending&amp;thenBy=-salary,office</c>).
/// Search/filter values stay on the caller's own model — they are app-specific.
/// </summary>
public class ProTableRequest
{
    private int _current = 1;
    private int _pageSize = 10;

    public int Current { get => _current; set => _current = Math.Max(1, value); }

    /// <summary>Clamped to 1..200 so a crafted query string cannot request unbounded pages.</summary>
    public int PageSize { get => _pageSize; set => _pageSize = Math.Clamp(value, 1, 200); }

    public string? SortField { get; set; }

    /// <summary>
    /// A visual filter, as <see cref="Query.FilterJson"/> text, that the table's <c>Filters</c> query applies (no filter when
    /// empty or not a filter). Plain text so MVC binds it from the query string.
    /// </summary>
    public string? Where { get; set; }

    public SortDirection SortOrder { get; set; }

    /// <summary>
    /// The sorts after the first, as <see cref="SortKeys.Format"/> writes them (<c>-salary,office</c>: a minus is
    /// descending). Plain text so MVC binds it from the query string; read <see cref="Sorts"/> for the whole list.
    /// </summary>
    public string? ThenBy { get; set; }

    /// <summary>Every sort in order: <see cref="SortField"/> first, then <see cref="ThenBy"/> (at most <see cref="SortKeys.Max"/>).</summary>
    public IReadOnlyList<SortKey> Sorts
    {
        get
        {
            if (SortField is not { Length: > 0 } first || SortOrder == SortDirection.None) return [];
            return [new SortKey(first, SortOrder), .. SortKeys.Parse(ThenBy).Where(k => !k.Is(first)).Take(SortKeys.Max - 1)];
        }
        set
        {
            var keys = SortKeys.Clean(value);
            SortField = keys.Count == 0 ? null : keys[0].Field;
            SortOrder = keys.Count == 0 ? SortDirection.None : keys[0].Direction;
            ThenBy = SortKeys.Format(keys.Skip(1));
        }
    }

    /// <summary>
    /// The field whose values group the rows (<c>?groupBy=department</c>), or null for none. The server sorts by it first
    /// and returns the page's groups with their counts and totals (<see cref="ProTableResponse{T}.Groups"/>).
    /// </summary>
    public string? GroupBy
    {
        get;
        set => field = value is { Length: > 0 } && ColumnSearch.IsKey(value) ? value : null;
    }

    /// <summary>Column searches from the headers (field → query, see <see cref="ColumnSearch"/>); null when none.</summary>
    public IReadOnlyDictionary<string, string?>? Searches { get; set; }

    /// <summary>
    /// Where the next rows start, from the previous page (<c>TablePage.NextCursor</c>): infinite and virtual scrolling pass it
    /// back instead of a page number. Opaque; an invalid or stale one starts at the numbered page.
    /// </summary>
    public string? Cursor { get; set; }

    public int Skip => (Current - 1) * PageSize;
}

/// <summary>One sort of a table: a field and its direction (never <see cref="SortDirection.None"/> in a list).</summary>
public sealed record SortKey(string Field, SortDirection Direction)
{
    public bool Descending => Direction == SortDirection.Descending;

    public bool Is(string field) => string.Equals(Field, field, StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// A table's sort list: what a header click does to it, and its text in the URL. A plain click sorts by that column
/// alone (ascending, descending, unsorted); a Shift+click adds it after the others, or turns or drops it if it is
/// already there, as in Excel's and AG Grid's multi-sort.
/// </summary>
public static class SortKeys
{
    /// <summary>The most sorts a table keeps (a query string can't ask for an unbounded ORDER BY).</summary>
    public const int Max = 4;

    /// <summary>The sorts as text: <c>-salary,office</c> (a minus is descending), or null when there are none.</summary>
    public static string? Format(IEnumerable<SortKey> keys)
    {
        var text = string.Join(',', keys.Where(k => k.Direction != SortDirection.None).Select(k => (k.Descending ? "-" : "") + k.Field));
        return text.Length == 0 ? null : text;
    }

    /// <summary>The sorts <paramref name="text"/> names; malformed and repeated fields are dropped, at most <see cref="Max"/>.</summary>
    public static IReadOnlyList<SortKey> Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > 400) return [];
        return Clean(text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(part =>
            part.StartsWith('-') ? new SortKey(part[1..], SortDirection.Descending) : new SortKey(part.TrimStart('+'), SortDirection.Ascending)));
    }

    /// <summary>The keys with valid field names, each field once, no unsorted ones, at most <see cref="Max"/>.</summary>
    public static IReadOnlyList<SortKey> Clean(IEnumerable<SortKey>? keys)
    {
        var list = new List<SortKey>();
        foreach (var key in keys ?? [])
        {
            if (key.Direction == SortDirection.None || !ColumnSearch.IsKey(key.Field) || list.Any(k => k.Is(key.Field))) continue;
            list.Add(key);
            if (list.Count == Max) break;
        }
        return list;
    }

    /// <summary>The direction <paramref name="field"/> sorts in, or <see cref="SortDirection.None"/>.</summary>
    public static SortDirection Of(IReadOnlyList<SortKey> sorts, string field) =>
        sorts.FirstOrDefault(k => k.Is(field))?.Direction ?? SortDirection.None;

    /// <summary>The 1-based place of <paramref name="field"/> among several sorts (for the header's badge), or 0 when it is the only one or unsorted.</summary>
    public static int Rank(IReadOnlyList<SortKey> sorts, string field)
    {
        if (sorts.Count < 2) return 0;
        for (var i = 0; i < sorts.Count; i++) if (sorts[i].Is(field)) return i + 1;
        return 0;
    }

    /// <summary>
    /// The sorts after a click on <paramref name="field"/>'s header. Without <paramref name="add"/> it is the only sort,
    /// cycling ascending, descending, unsorted. With it (Shift+click) the other sorts stay: a new field goes last,
    /// ascending; one already there turns descending, then drops out.
    /// </summary>
    public static IReadOnlyList<SortKey> Click(IReadOnlyList<SortKey> sorts, string field, bool add)
    {
        var next = ColumnSearch.NextSort(Of(sorts, field));
        if (!add) return next == SortDirection.None ? [] : [new SortKey(field, next)];
        var list = sorts.ToList();
        var at = list.FindIndex(k => k.Is(field));
        if (at < 0)
        {
            if (list.Count >= Max) return list;
            list.Add(new SortKey(field, SortDirection.Ascending));
        }
        else if (next == SortDirection.None) list.RemoveAt(at);
        else list[at] = list[at] with { Direction = next };
        return list;
    }
}

public class ProTableResponse<T>
{
    public IReadOnlyList<T> Data { get; init; } = [];

    public int Total { get; init; }

    /// <summary>With <see cref="ProTableRequest.GroupBy"/>: the groups of the page's rows, in row order; null when not grouped.</summary>
    public IReadOnlyList<TableGroup>? Groups { get; init; }

    /// <summary>Totals over every matching row (the table's footer), or null when the source has none.</summary>
    public IReadOnlyList<TableTotal>? Totals { get; init; }
}

public static class ProTableQueryExtensions
{
    /// <summary>Applies paging to an already filtered and sorted sequence.</summary>
    public static ProTableResponse<T> ToPage<T>(this IEnumerable<T> source, ProTableRequest request)
    {
        var list = source as IReadOnlyCollection<T> ?? source.ToList();
        return new ProTableResponse<T>
        {
            Total = list.Count,
            Data = list.Skip(request.Skip).Take(request.PageSize).ToList(),
        };
    }

    /// <summary>
    /// Sorts by every sort of the request (<see cref="ProTableRequest.Sorts"/>) that <paramref name="keys"/> knows, in
    /// order; unknown fields are skipped. Without a known sort the source is returned as it is.
    /// </summary>
    public static IEnumerable<T> SortBy<T>(this IEnumerable<T> source, ProTableRequest request, IReadOnlyDictionary<string, Func<T, object?>> keys)
    {
        IOrderedEnumerable<T>? ordered = null;
        foreach (var sort in request.Sorts)
        {
            var key = keys.FirstOrDefault(k => sort.Is(k.Key)).Value;
            if (key is null) continue;
            ordered = (ordered, sort.Descending) switch
            {
                (null, false) => source.OrderBy(key, Comparer<object?>.Default),
                (null, true) => source.OrderByDescending(key, Comparer<object?>.Default),
                (_, false) => ordered.ThenBy(key, Comparer<object?>.Default),
                (_, true) => ordered.ThenByDescending(key, Comparer<object?>.Default),
            };
        }
        return ordered ?? source;
    }

    /// <summary>Sorts by <paramref name="key"/> when the request targets <paramref name="field"/>.</summary>
    public static IEnumerable<T> SortBy<T, TKey>(this IEnumerable<T> source, ProTableRequest request, string field, Func<T, TKey> key)
    {
        if (!string.Equals(request.SortField, field, StringComparison.OrdinalIgnoreCase)) return source;
        return request.SortOrder switch
        {
            SortDirection.Ascending => source.OrderBy(key),
            SortDirection.Descending => source.OrderByDescending(key),
            _ => source,
        };
    }
}
