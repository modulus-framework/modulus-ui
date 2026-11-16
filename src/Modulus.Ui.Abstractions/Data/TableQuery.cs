using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using Modulus.Ui.Abstractions.Query;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Modulus.Ui.Abstractions.Pivot;

namespace Modulus.Ui.Abstractions.Data;

/// <summary>
/// Turns a table request (<see cref="ProTableRequest"/>: page, page size, sort, column searches, cursor) into a query the
/// database runs, so a table over a million rows only ever reads one page:
/// <code>
/// static readonly TableQuery&lt;Order&gt; Orders = TableQuery&lt;Order&gt;.By(o => o.Id)
///     .Sort("date", o => o.Date).Sort("amount", o => o.Amount).Sort("customer", o => o.Customer)
///     .SearchText("customer", o => o.Customer)
///     .DefaultSort("date", descending: true);
///
/// var page = await Orders.PageAsync(db.Orders.Where(o => o.Status == status), request, CountMode.Exact, (q, ct) => q.CountAsync(ct), ct);
/// </code>
/// <para>
/// Only registered fields sort and search: anything else in the query string is ignored. Rows are always ordered by the
/// sort keys (<see cref="ProTableRequest.Sorts"/>: the first, then each <c>thenBy</c> one), then by the id, so pages
/// never overlap or skip rows that share a value.
/// </para>
/// <para>
/// Paging: <see cref="Page"/> reads a numbered page (<c>OFFSET</c>). With a cursor (infinite and virtual scrolling) the next
/// page continues after the last row's sort value and id (keyset paging: <c>WHERE (date, id) &lt; (…)</c>), which costs
/// the same however deep the user scrolls. Keys that may be null, types that can't be compared in SQL, and a sort on
/// several keys fall back to <c>OFFSET</c> for the cursor.
/// </para>
/// <para>
/// Grouping (<see cref="ProTableRequest.GroupBy"/>) on a field registered with <see cref="Group"/> sorts by that field
/// first and returns the page's groups (<see cref="TablePage{T}.Groups"/>) with each whole group's row count and the
/// totals (<c>Total(…)</c>): one <c>GROUP BY … WHERE key IN (the page's values)</c>. The totals over every matching row are one
/// more aggregate query (only when totals are registered). A grouped page always pages by <c>OFFSET</c>.
/// </para>
/// <para>
/// Everything stays <see cref="IQueryable{T}"/> (Where, OrderBy, ThenBy, Skip, Take, <c>string.Compare</c>), which
/// Entity Framework Core and other LINQ providers translate to SQL. The async methods read through
/// <see cref="IAsyncEnumerable{T}"/> when the provider offers it; pass <c>countAsync</c> to count asynchronously too.
/// </para>
/// </summary>
public sealed partial class TableQuery<T>
{
    /// <summary>Longest column search that is applied; longer text is cut (a query string can't send a huge LIKE).</summary>
    public const int MaxSearchLength = 200;

    private readonly Key _id;
    private readonly Dictionary<string, Key> _sorts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<string, Expression<Func<T, bool>>>> _searches = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<object?, string>?> _groups = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<TotalDef> _totals = [];
    private string? _defaultField;
    private bool _defaultDescending;
    private QueryBuilder<T>? _builder;

    /// <summary>The most totals a query keeps.</summary>
    public const int MaxTotals = 8;

    private TableQuery(Key id) => _id = id;

    /// <summary>Starts a query over rows identified by <paramref name="id"/> (unique; the last sort key, so the order is stable).</summary>
    public static TableQuery<T> By<TId>(Expression<Func<T, TId>> id) => new(Key.Create(id, keyset: true));

    /// <summary>Lets <paramref name="field"/> (the <c>sortField</c> of the request) sort by <paramref name="key"/>.</summary>
    /// <param name="keyset">
    /// Whether a cursor may continue after this key's value. Leave it on for keys that are never null; nullable value
    /// types turn it off by themselves (rows with a null would be skipped).
    /// </param>
    public TableQuery<T> Sort<TKey>(string field, Expression<Func<T, TKey>> key, bool keyset = true)
    {
        _sorts[field] = Key.Create(key, keyset);
        return this;
    }

    /// <summary>The order when the request names no registered field (otherwise: by id, ascending).</summary>
    public TableQuery<T> DefaultSort(string field, bool descending = false)
    {
        if (!_sorts.ContainsKey(field)) throw new ArgumentException($"Register the sort '{field}' before making it the default.", nameof(field));
        _defaultField = field;
        _defaultDescending = descending;
        return this;
    }

    /// <summary>
    /// Lets the rows group by <paramref name="field"/>, a registered sort (<c>?groupBy={field}</c>): the page then sorts
    /// by it first and carries its groups. <paramref name="label"/> turns a value into the text of its group header
    /// (default: the value in the current culture, "(none)" for null or empty).
    /// </summary>
    public TableQuery<T> Group(string field, Func<object?, string>? label = null)
    {
        if (!_sorts.ContainsKey(field)) throw new ArgumentException($"Register the sort '{field}' before grouping by it.", nameof(field));
        _groups[field] = label;
        return this;
    }

    /// <summary>
    /// A total of <paramref name="field"/>'s <paramref name="value"/>s for each group and for every matching row (the
    /// footer), combined by <paramref name="aggregate"/>. At most <see cref="MaxTotals"/>.
    /// </summary>
    public TableQuery<T> Total(string field, string title, Expression<Func<T, decimal>> value, PivotAggregate aggregate = PivotAggregate.Sum, int decimals = 2)
    {
        if (_totals.Count >= MaxTotals) throw new InvalidOperationException($"A table query keeps at most {MaxTotals} totals.");
        _totals.Add(new TotalDef(field, title, value, aggregate, decimals));
        return this;
    }

    /// <summary><see cref="Total(string, string, Expression{Func{T, decimal}}, PivotAggregate, int)"/> over a whole number.</summary>
    public TableQuery<T> Total(string field, string title, Expression<Func<T, int>> value, PivotAggregate aggregate = PivotAggregate.Sum, int decimals = 0) =>
        Total(field, title, Expression.Lambda<Func<T, decimal>>(Expression.Convert(value.Body, typeof(decimal)), value.Parameters), aggregate, decimals);

    /// <summary>Whether <paramref name="field"/> can group the rows.</summary>
    public bool CanGroup(string field) => _groups.ContainsKey(field);

    /// <summary>
    /// The fields a visual filter may ask about: the request's <see cref="ProTableRequest.Where"/> filters the rows by
    /// them, after the column searches. A filter naming a field the builder doesn't have drops that rule.
    /// </summary>
    public TableQuery<T> Filters(QueryBuilder<T> builder)
    {
        _builder = builder;
        return this;
    }

    /// <summary>Lets the column search of <paramref name="field"/> (<c>s.{field}</c>) filter with <paramref name="predicate"/> (given the trimmed text).</summary>
    public TableQuery<T> Search(string field, Func<string, Expression<Func<T, bool>>> predicate)
    {
        _searches[field] = predicate;
        return this;
    }

    /// <summary>
    /// A column search that keeps rows whose text contains the query (<c>LIKE '%…%'</c>; case follows the database
    /// collation); a checkbox list (<c>=a|b</c>, <c>!=a|b</c>) keeps the rows with (or without) those exact values
    /// (<c>IN</c>). <paramref name="ignoreCase"/> compares lower-cased text (<c>LOWER(col) LIKE</c>), for in-memory sources
    /// and case-sensitive collations; it can't use an index, so prefer a case-insensitive collation on large tables.
    /// </summary>
    public TableQuery<T> SearchText(string field, Expression<Func<T, string?>> text, bool ignoreCase = false)
    {
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        var toLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
        return Search(field, query =>
        {
            // A checkbox list (=a|b, !=a|b): x => values.Contains(x.Text), an IN over a parameter.
            if (ValueListSearch.Test(query, text.Body, ignoreCase) is { } list) return Expression.Lambda<Func<T, bool>>(list, text.Parameters);
            // x => x.Text != null && x.Text.Contains(query), with the query as a parameter (not a literal) for plan caching.
            var body = text.Body;
            var value = Box.Of(ignoreCase ? query.ToLowerInvariant() : query, typeof(string));
            var subject = ignoreCase ? Expression.Call(body, toLower) : body;
            var test = Expression.AndAlso(Expression.NotEqual(body, Expression.Constant(null, typeof(string))), Expression.Call(subject, contains, value));
            return Expression.Lambda<Func<T, bool>>(test, text.Parameters);
        });
    }

    /// <summary>
    /// A copy without the sorts and column searches of the fields <paramref name="allow"/> refuses
    /// (<see cref="State.FieldAccess.For"/>): a request naming one of them is treated like an unknown field. The default
    /// sort falls back to the id when its field is refused.
    /// </summary>
    public TableQuery<T> Restrict(Func<string, bool> allow)
    {
        var copy = new TableQuery<T>(_id);
        foreach (var (field, key) in _sorts) if (allow(field)) copy._sorts[field] = key;
        foreach (var (field, search) in _searches) if (allow(field)) copy._searches[field] = search;
        foreach (var (field, label) in _groups) if (copy._sorts.ContainsKey(field)) copy._groups[field] = label;
        copy._totals.AddRange(_totals.Where(t => allow(t.Field)));
        copy._builder = _builder?.Restrict(allow);
        if (_defaultField is not null && copy._sorts.ContainsKey(_defaultField))
        {
            copy._defaultField = _defaultField;
            copy._defaultDescending = _defaultDescending;
        }
        return copy;
    }

    /// <summary>Whether <paramref name="field"/> can sort (for the header's sorter).</summary>
    public bool CanSort(string field) => _sorts.ContainsKey(field);

    /// <summary>The source with the request's column searches applied (before sorting; use it to count or aggregate).</summary>
    public IQueryable<T> Filter(IQueryable<T> source, ProTableRequest request)
    {
        foreach (var (field, text) in request.Searches ?? new Dictionary<string, string?>())
        {
            var query = text?.Trim();
            if (string.IsNullOrEmpty(query) || !_searches.TryGetValue(field, out var predicate)) continue;
            // A checkbox list keeps its whole list: a cut one would lose values or split one.
            var max = ColumnSearch.IsValueList(query) ? ColumnSearch.MaxListLength : MaxSearchLength;
            source = source.Where(predicate(query.Length > max ? query[..max] : query));
        }
        if (_builder?.Compile(FilterJson.FromText(request.Where)) is { } where) source = source.Where(where);
        return source;
    }

    /// <summary>The source filtered and in the request's order (sort key, then id).</summary>
    public IQueryable<T> Apply(IQueryable<T> source, ProTableRequest request) => Order(Filter(source, request), Resolve(request));

    /// <summary>The sorts the request gets: its registered fields in order, else the default sort, else the id.</summary>
    public IReadOnlyList<SortKey> SortsOf(ProTableRequest request) =>
        [.. Resolve(request).Where(s => s.Field.Length > 0).Select(s => new SortKey(s.Field, s.Descending ? SortDirection.Descending : SortDirection.Ascending))];

    /// <summary>The page the request asks for: its numbered page, or the rows after its <see cref="ProTableRequest.Cursor"/>.</summary>
    public TablePage<T> Page(IQueryable<T> source, ProTableRequest request, CountMode count = CountMode.Exact)
    {
        var plan = Plan(source, request, count);
        var page = plan.Finish(plan.Rows.ToList(), plan.Count?.Invoke());
        return SummarizeAsync(plan, page, async: false, default).GetAwaiter().GetResult();
    }

    /// <summary>
    /// <see cref="Page"/>, reading the rows asynchronously (<see cref="IAsyncEnumerable{T}"/>, as Entity Framework Core offers).
    /// </summary>
    /// <param name="countAsync">Counts asynchronously, e.g. <c>(q, ct) => q.CountAsync(ct)</c>; without it the count runs synchronously.</param>
    public async Task<TablePage<T>> PageAsync(IQueryable<T> source, ProTableRequest request, CountMode count = CountMode.Exact,
        Func<IQueryable<T>, CancellationToken, Task<int>>? countAsync = null, CancellationToken cancellationToken = default)
    {
        var plan = Plan(source, request, count);
        int? total = count == CountMode.Exact
            ? countAsync is null ? plan.Count!() : await countAsync(plan.Filtered, cancellationToken).ConfigureAwait(false)
            : null;
        var page = plan.Finish(await ToListAsync(plan.Rows, cancellationToken).ConfigureAwait(false), total);
        return await SummarizeAsync(plan, page, async: true, cancellationToken).ConfigureAwait(false);
    }
}
