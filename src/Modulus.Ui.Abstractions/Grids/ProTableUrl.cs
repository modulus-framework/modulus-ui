namespace Modulus.Ui.Abstractions;

/// <summary>
/// A table's paging, sort and column searches in a query string, with the keys the server-rendered grids use
/// (<c>current</c>, <c>pageSize</c>, <c>sortField</c>, <c>sortOrder</c>, <c>thenBy</c>, <c>groupBy</c>, <c>s.{key}</c>). The Blazor ProTable's
/// UrlState reads and writes them, so a Blazor table and an MVC one share their URLs (and saved views).
/// </summary>
public static class ProTableUrl
{
    public const string Current = "current";
    public const string PageSize = "pageSize";
    public const string SortField = "sortField";
    public const string SortOrder = "sortOrder";
    public const string ThenBy = "thenBy";
    public const string GroupBy = TableGroups.QueryKey;

    /// <summary>Whether <paramref name="key"/> is one of the table's own keys.</summary>
    public static bool IsTableKey(string key) =>
        key is Current or PageSize or SortField or SortOrder or ThenBy or GroupBy || key.StartsWith(ColumnSearch.Prefix, StringComparison.Ordinal);

    /// <summary>The request a query names; missing or malformed values fall back to page 1, <paramref name="pageSize"/> and no sort.</summary>
    public static ProTableRequest Read(IEnumerable<KeyValuePair<string, string?>> query, int pageSize = 10)
    {
        var list = query as IReadOnlyList<KeyValuePair<string, string?>> ?? query.ToList();
        string? Value(string key) => list.LastOrDefault(q => q.Key == key).Value;
        var request = new ProTableRequest
        {
            Current = int.TryParse(Value(Current), out var current) ? current : 1,
            PageSize = int.TryParse(Value(PageSize), out var size) ? size : pageSize,
            Searches = ColumnSearch.FromQuery(list),
            GroupBy = Value(GroupBy),
        };
        if (Value(SortField) is { Length: > 0 } field && ColumnSearch.IsKey(field)
            && Enum.TryParse<SortDirection>(Value(SortOrder), ignoreCase: true, out var order) && order != SortDirection.None)
        {
            request.Sorts = [new SortKey(field, order), .. SortKeys.Parse(Value(ThenBy))];
        }
        return request;
    }

    /// <summary>The query pairs of <paramref name="request"/>, leaving out the defaults (page 1, <paramref name="pageSize"/>, no sort).</summary>
    public static IEnumerable<KeyValuePair<string, string?>> Write(ProTableRequest request, int pageSize = 10)
    {
        var sorts = request.Sorts;
        if (sorts.Count > 0)
        {
            yield return new(SortField, sorts[0].Field);
            yield return new(SortOrder, sorts[0].Direction.ToString());
            if (SortKeys.Format(sorts.Skip(1)) is { } then) yield return new(ThenBy, then);
        }
        if (request.GroupBy is { } group) yield return new(GroupBy, group);
        foreach (var (key, value) in request.Searches ?? new Dictionary<string, string?>())
            if (!string.IsNullOrWhiteSpace(value)) yield return new(ColumnSearch.Prefix + key, value);
        if (request.PageSize != pageSize) yield return new(PageSize, request.PageSize.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (request.Current > 1) yield return new(Current, request.Current.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    /// <summary>A query string of <paramref name="pairs"/> (empty values dropped), without the "?".</summary>
    public static string Query(IEnumerable<KeyValuePair<string, string?>> pairs) =>
        string.Join('&', pairs.Where(p => !string.IsNullOrEmpty(p.Key) && !string.IsNullOrEmpty(p.Value))
            .Select(p => Uri.EscapeDataString(p.Key) + "=" + Uri.EscapeDataString(p.Value!)));

    /// <summary>Whether two queries hold the same pairs, in any order.</summary>
    public static bool SameQuery(IEnumerable<KeyValuePair<string, string?>> a, IEnumerable<KeyValuePair<string, string?>> b)
    {
        static string Key(IEnumerable<KeyValuePair<string, string?>> q) => string.Join('&', q
            .Where(p => !string.IsNullOrEmpty(p.Key) && !string.IsNullOrEmpty(p.Value))
            .Select(p => p.Key + "=" + p.Value).Order(StringComparer.Ordinal));
        return Key(a) == Key(b);
    }
}
