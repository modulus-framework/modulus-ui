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
/// (<c>?current=2&amp;pageSize=20&amp;sortField=name&amp;sortOrder=Ascending</c>).
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

    public SortDirection SortOrder { get; set; }

    /// <summary>Column searches from the headers (field → query, see <see cref="ColumnSearch"/>); null when none.</summary>
    public IReadOnlyDictionary<string, string?>? Searches { get; set; }

    /// <summary>
    /// Where the next rows start, from the previous page (<c>TablePage.NextCursor</c>): infinite and virtual scrolling pass it
    /// back instead of a page number. Opaque; an invalid or stale one starts at the numbered page.
    /// </summary>
    public string? Cursor { get; set; }

    public int Skip => (Current - 1) * PageSize;
}

public class ProTableResponse<T>
{
    public IReadOnlyList<T> Data { get; init; } = [];

    public int Total { get; init; }
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
