using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Modulus.Ui.Abstractions.Pivot;

namespace Modulus.Ui.Abstractions.Data;

/// <summary>Whether a page also counts every matching row.</summary>
public enum CountMode
{
    /// <summary>Count the rows (a <c>COUNT(*)</c>): the pager shows "1–50 of 100,000".</summary>
    Exact,

    /// <summary>
    /// Don't count: one row more than the page is read to know whether there is a next page. Use it for tables too large
    /// to count on every request, and for infinite and virtual scrolling after the first page.
    /// </summary>
    None,
}

/// <summary>One page of rows from <see cref="TableQuery{T}"/>.</summary>
/// <param name="Items">The rows of the page.</param>
/// <param name="Offset">Position of the first row among all matching rows (for row numbers and <c>aria-rowindex</c>).</param>
/// <param name="PageSize">Rows asked for.</param>
/// <param name="Total">Every matching row, or null when not counted (<see cref="CountMode.None"/>).</param>
/// <param name="HasMore">Whether rows follow this page.</param>
/// <param name="NextCursor">Opaque cursor for the next page (<see cref="ProTableRequest.Cursor"/>), or null on the last page.</param>
public sealed record TablePage<T>(IReadOnlyList<T> Items, int Offset, int PageSize, int? Total, bool HasMore, string? NextCursor)
{
    /// <summary>With <see cref="ProTableRequest.GroupBy"/> on a field that groups: the page's groups in row order; otherwise null.</summary>
    public IReadOnlyList<TableGroup>? Groups { get; init; }

    /// <summary>The registered totals over every matching row; null when none are registered.</summary>
    public IReadOnlyList<TableTotal>? Totals { get; init; }

    /// <summary>The page as a ProTable response (rows, count, groups, totals).</summary>
    public ProTableResponse<T> ToResponse() => new() { Data = Items, Total = Total ?? Offset + Items.Count + (HasMore ? 1 : 0), Groups = Groups, Totals = Totals };
}

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
public sealed class TableQuery<T>
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

    // Planning ---------------------------------------------------------------------------------------------------------

    private readonly record struct Sorting(string Field, Key Key, bool Descending);

    // The field the request groups by, when it may.
    private string? GroupField(ProTableRequest request) =>
        request.GroupBy is { } field && _groups.ContainsKey(field) ? _groups.Keys.First(k => string.Equals(k, field, StringComparison.OrdinalIgnoreCase)) : null;

    // The registered sorts of the request, in order (unknown fields skipped); else the default sort; else the id.
    // A group field goes first, in the direction the request sorts it (ascending when it doesn't).
    private Sorting[] Resolve(ProTableRequest request)
    {
        var list = new List<Sorting>();
        if (GroupField(request) is { } group)
            list.Add(new(group.ToLowerInvariant(), _sorts[group], SortKeys.Of(request.Sorts, group) == SortDirection.Descending));
        foreach (var sort in request.Sorts)
            if (_sorts.TryGetValue(sort.Field, out var key) && !list.Any(s => ReferenceEquals(s.Key, key)))
                list.Add(new(sort.Field.ToLowerInvariant(), key, sort.Descending));
        if (list.Count > 0 && (list.Count > 1 || GroupField(request) is null)) return [.. list];
        if (_defaultField is not null && !list.Any(s => ReferenceEquals(s.Key, _sorts[_defaultField])))
            list.Add(new(_defaultField.ToLowerInvariant(), _sorts[_defaultField], _defaultDescending));
        if (list.Count > 0) return [.. list];
        return [new("", _id, false)];
    }

    private IQueryable<T> Order(IQueryable<T> source, Sorting[] sorts)
    {
        var ordered = Call(source, sorts[0].Descending ? "OrderByDescending" : "OrderBy", sorts[0].Key);
        foreach (var sort in sorts.Skip(1)) ordered = Call(ordered, sort.Descending ? "ThenByDescending" : "ThenBy", sort.Key);
        // Ties go by id in the first sort's direction, which keeps the order total (and keyset paging exact).
        return sorts.Any(s => ReferenceEquals(s.Key, _id)) ? ordered : Call(ordered, sorts[0].Descending ? "ThenByDescending" : "ThenBy", _id);
    }

    // What a cursor was made for: the field of a single sort, or every key ("-salary,name") of several.
    private static string Signature(Sorting[] sorts) =>
        sorts.Length == 1 ? sorts[0].Field : string.Join(',', sorts.Select(s => (s.Descending ? "-" : "") + s.Field));

    private static IQueryable<T> Call(IQueryable<T> source, string method, Key key) =>
        source.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), method, [typeof(T), key.Type], source.Expression, Expression.Quote(key.Lambda)));

    private sealed class QueryPlan(IQueryable<T> filtered, IQueryable<T> rows, Func<int>? count, Func<List<T>, int?, TablePage<T>> finish)
    {
        public IQueryable<T> Filtered { get; } = filtered;
        public string? GroupField { get; init; }
        public List<object?> GroupKeys { get; } = [];
        public IQueryable<T> Rows { get; } = rows;
        public Func<int>? Count { get; } = count;
        public TablePage<T> Finish(List<T> rows, int? total) => finish(rows, total);
    }

    private QueryPlan Plan(IQueryable<T> source, ProTableRequest request, CountMode count)
    {
        var sorts = Resolve(request);
        var sort = sorts[0];
        var signature = Signature(sorts);
        var filtered = Filter(source, request);
        var ordered = Order(filtered, sorts);
        var size = request.PageSize;

        var group = GroupField(request);
        var offset = request.Skip;
        IQueryable<T> page;
        if (request.Cursor is { Length: > 0 } text && Cursor.Parse(text) is { } cursor && cursor.Field == signature && cursor.Descending == sort.Descending)
        {
            offset = cursor.Offset;
            // Keyset paging continues after one sort key and the id; several keys, and groups, page by offset.
            page = sorts.Length == 1 && group is null && After(ordered, sort, cursor) is { } keyset ? keyset : ordered.Skip(offset);
        }
        else page = ordered.Skip(offset);

        // A grouped page also reads the row before it, to know whether its first group started earlier.
        var before = group is not null && offset > 0 ? 1 : 0;
        if (before == 1) page = ordered.Skip(offset - 1);

        // One row more than the page tells whether another page follows, without counting (and whether the last group goes on).
        var rows = page.Take(size + 1 + before);
        Func<int>? counter = count == CountMode.Exact ? () => filtered.Count() : null;
        QueryPlan? plan = null;
        plan = new QueryPlan(filtered, rows, counter, (list, total) =>
        {
            var previous = before == 1 && list.Count > 0 ? list[0] : default;
            if (before == 1 && list.Count > 0) list.RemoveAt(0);
            var more = list.Count > size;
            var following = more ? list[size] : default;
            if (more) list.RemoveRange(size, list.Count - size);
            var next = more && list.Count > 0 ? Cursor.For(signature, sorts.Length == 1 ? sort : null, sort.Descending, _id, list[^1], offset + list.Count) : null;
            var result = new TablePage<T>(list, offset, size, total, more, next);
            return group is null ? result : result with { Groups = Spans(group, list, before == 1 && previous is not null, previous, more, following, plan!.GroupKeys) };
        }) { GroupField = group };
        return plan;
    }

    // Groups ----------------------------------------------------------------------------------------------------------

    private sealed record TotalDef(string Field, string Title, Expression<Func<T, decimal>> Value, PivotAggregate Aggregate, int Decimals);

    // One group's count and totals as the database returns them.
    private sealed record GroupSum(object? Key, int Count, decimal[] Values);

    // The page's runs of one group value, with their rows on the page (counts and totals come from SummarizeAsync).
    private List<TableGroup> Spans(string field, List<T> rows, bool hasPrevious, T? previous, bool hasFollowing, T? following, List<object?> keys)
    {
        var key = _sorts[field];
        var label = _groups[field] ?? DefaultLabel;
        var groups = new List<TableGroup>();
        for (var i = 0; i < rows.Count;)
        {
            var value = key.Read(rows[i]);
            var j = i;
            while (j + 1 < rows.Count && Equals(key.Read(rows[j + 1]), value)) j++;
            keys.Add(value);
            groups.Add(new TableGroup(key.Text(value) ?? "", label(value), j - i + 1, [])
            {
                Rows = j - i + 1,
                ContinuesBefore = i == 0 && hasPrevious && Equals(key.Read(previous!), value),
                ContinuesAfter = j == rows.Count - 1 && hasFollowing && Equals(key.Read(following!), value),
            });
            i = j + 1;
        }
        return groups;
    }

    private static string DefaultLabel(object? value) => value switch
    {
        null => "(none)",
        string { Length: 0 } => "(none)",
        DateTime d => d.TimeOfDay == TimeSpan.Zero ? d.ToString("d", CultureInfo.CurrentCulture) : d.ToString("g", CultureInfo.CurrentCulture),
        IFormattable f => f.ToString(null, CultureInfo.CurrentCulture),
        _ => value.ToString() ?? "",
    };

    // Fills in each group's count and totals over the whole group, and the totals over every matching row.
    private async Task<TablePage<T>> SummarizeAsync(QueryPlan plan, TablePage<T> page, bool async, CancellationToken cancellationToken)
    {
        if (page.Groups is { Count: > 0 } groups)
        {
            var key = _sorts[plan.GroupField!];
            var sums = await RunAsync(key.Type, plan.Filtered, key.Lambda, plan.GroupKeys, async, cancellationToken).ConfigureAwait(false);
            var filled = new List<TableGroup>(groups.Count);
            for (var i = 0; i < groups.Count; i++)
            {
                var sum = sums.FirstOrDefault(s => Equals(s.Key, plan.GroupKeys[i]));
                filled.Add(groups[i] with { Count = sum?.Count ?? groups[i].Rows, Totals = Totals(sum) });
            }
            page = page with { Groups = filled };
        }
        if (_totals.Count > 0)
        {
            Expression<Func<T, int>> all = x => 1;
            var sums = await RunAsync(typeof(int), plan.Filtered, all, null, async, cancellationToken).ConfigureAwait(false);
            page = page with { Totals = Totals(sums.FirstOrDefault()) };
        }
        return page;
    }

    private List<TableTotal> Totals(GroupSum? sum) => [.. _totals.Select((t, i) => new TableTotal(t.Field, t.Title, t.Aggregate,
        sum is null || sum.Count == 0 ? (t.Aggregate == PivotAggregate.Sum ? 0m : null)
        : t.Aggregate == PivotAggregate.Average ? sum.Values[i] / sum.Count : sum.Values[i], t.Decimals))];

    private static readonly MethodInfo RunMethod = typeof(TableQuery<T>).GetMethod(nameof(RunTypedAsync), BindingFlags.NonPublic | BindingFlags.Instance)!;

    private Task<List<GroupSum>> RunAsync(Type keyType, IQueryable<T> source, LambdaExpression key, List<object?>? only, bool async, CancellationToken cancellationToken) =>
        (Task<List<GroupSum>>)RunMethod.MakeGenericMethod(keyType).Invoke(this, [source, key, only, async, cancellationToken])!;

    // source.Where(x => only.Contains(key(x))).GroupBy(key).Select(g => new TableGroupLeaf { Key = g.Key, Count = g.Count(), V0 = g.Sum(…), … })
    private async Task<List<GroupSum>> RunTypedAsync<TKey>(IQueryable<T> source, LambdaExpression key, List<object?>? only, bool async, CancellationToken cancellationToken)
    {
        var lambda = (Expression<Func<T, TKey>>)key;
        if (only is not null)
        {
            var values = only.Select(v => (TKey)v!).Distinct().ToList();
            var contains = Expression.Call(typeof(Enumerable), nameof(Enumerable.Contains), [typeof(TKey)], Box.Of(values, typeof(List<TKey>)), lambda.Body);
            source = source.Where(Expression.Lambda<Func<T, bool>>(contains, lambda.Parameters));
        }
        var g = Expression.Parameter(typeof(IGrouping<TKey, T>), "g");
        var leaf = typeof(TableGroupLeaf<TKey>);
        var bindings = new List<MemberBinding>
        {
            Expression.Bind(leaf.GetProperty(nameof(TableGroupLeaf<int>.Key))!, Expression.Property(g, nameof(IGrouping<int, T>.Key))),
            Expression.Bind(leaf.GetProperty(nameof(TableGroupLeaf<int>.Count))!, Expression.Call(CountMethod, g)),
        };
        for (var i = 0; i < _totals.Count; i++)
        {
            var method = _totals[i].Aggregate switch { PivotAggregate.Min => MinMethod, PivotAggregate.Max => MaxMethod, _ => SumMethod };
            bindings.Add(Expression.Bind(leaf.GetProperty("V" + i.ToString(CultureInfo.InvariantCulture))!, Expression.Call(method, g, _totals[i].Value)));
        }
        var select = Expression.Lambda<Func<IGrouping<TKey, T>, TableGroupLeaf<TKey>>>(Expression.MemberInit(Expression.New(leaf), bindings), g);
        var query = source.GroupBy(lambda).Select(select);
        var leaves = async ? await ToListAsync(query, cancellationToken).ConfigureAwait(false) : query.ToList();
        return [.. leaves.Select(l => new GroupSum(l.Key, l.Count, [.. new[] { l.V0, l.V1, l.V2, l.V3, l.V4, l.V5, l.V6, l.V7 }.Take(_totals.Count)]))];
    }

    private static readonly MethodInfo CountMethod = new Func<IEnumerable<T>, int>(Enumerable.Count).Method;
    private static readonly MethodInfo SumMethod = new Func<IEnumerable<T>, Func<T, decimal>, decimal>(Enumerable.Sum).Method;
    private static readonly MethodInfo MinMethod = new Func<IEnumerable<T>, Func<T, decimal>, decimal>(Enumerable.Min).Method;
    private static readonly MethodInfo MaxMethod = new Func<IEnumerable<T>, Func<T, decimal>, decimal>(Enumerable.Max).Method;

    // Rows after the cursor's sort value and id: key > v OR (key = v AND id > i), or < for a descending sort.
    private IQueryable<T>? After(IQueryable<T> ordered, Sorting sort, Cursor cursor)
    {
        if (!_id.Keyset || cursor.Id is null || _id.Parse(cursor.Id) is not { } id) return null;
        var parameter = Expression.Parameter(typeof(T), "x");
        var idKey = _id.Body(parameter);
        var greater = !sort.Descending;
        Expression test;
        if (ReferenceEquals(sort.Key, _id)) test = Compare(idKey, id, _id.Type, greater);
        else
        {
            if (!sort.Key.Keyset || cursor.Value is null || sort.Key.Parse(cursor.Value) is not { } value) return null;
            var key = sort.Key.Body(parameter);
            test = Expression.OrElse(
                Compare(key, value, sort.Key.Type, greater),
                Expression.AndAlso(Expression.Equal(key, Box.Of(value, sort.Key.Type)), Compare(idKey, id, _id.Type, greater)));
        }
        return ordered.Where(Expression.Lambda<Func<T, bool>>(test, parameter));
    }

    private static Expression Compare(Expression left, object value, Type type, bool greater)
    {
        Expression right = Box.Of(value, type);
        if (type == typeof(string))
        {
            var compare = Expression.Call(typeof(string).GetMethod(nameof(string.Compare), [typeof(string), typeof(string)])!, left, right);
            return greater ? Expression.GreaterThan(compare, Expression.Constant(0)) : Expression.LessThan(compare, Expression.Constant(0));
        }
        if (type.IsEnum)
        {
            var underlying = Enum.GetUnderlyingType(type);
            left = Expression.Convert(left, underlying);
            right = Expression.Convert(right, underlying);
        }
        return greater ? Expression.GreaterThan(left, right) : Expression.LessThan(left, right);
    }

    private static async Task<List<TRow>> ToListAsync<TRow>(IQueryable<TRow> query, CancellationToken cancellationToken)
    {
        if (query is not IAsyncEnumerable<TRow> rows) return query.ToList();
        var list = new List<TRow>();
        await foreach (var row in rows.WithCancellation(cancellationToken).ConfigureAwait(false)) list.Add(row);
        return list;
    }

    // Keys -------------------------------------------------------------------------------------------------------------

    private static readonly HashSet<Type> KeysetTypes =
    [
        typeof(int), typeof(long), typeof(short), typeof(byte), typeof(decimal), typeof(double), typeof(float),
        typeof(DateTime), typeof(DateTimeOffset), typeof(DateOnly), typeof(TimeOnly), typeof(string),
    ];

    private sealed class Key
    {
        public required LambdaExpression Lambda { get; init; }
        public required Type Type { get; init; }
        public required bool Keyset { get; init; }
        public required Func<T, object?> Read { get; init; }

        public static Key Create<TKey>(Expression<Func<T, TKey>> lambda, bool keyset)
        {
            var type = typeof(TKey);
            var read = Expression.Lambda<Func<T, object?>>(Expression.Convert(lambda.Body, typeof(object)), lambda.Parameters).Compile();
            return new Key
            {
                Lambda = lambda,
                Type = type,
                Keyset = keyset && Nullable.GetUnderlyingType(type) is null && (type.IsEnum || KeysetTypes.Contains(type)),
                Read = row => read(row),
            };
        }

        /// <summary>The key's body over another parameter.</summary>
        public Expression Body(ParameterExpression parameter) => new Rebind(Lambda.Parameters[0], parameter).Visit(Lambda.Body);

        public string? Text(object? value) => value switch
        {
            null => null,
            string s => s,
            DateTime d => d.ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset d => d.ToString("O", CultureInfo.InvariantCulture),
            DateOnly d => d.ToString("O", CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString("O", CultureInfo.InvariantCulture),
            Enum e => Convert.ToInt64(e, CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString(),
        };

        /// <summary>The cursor's text as a value of the key's type, or null when it doesn't parse (a tampered cursor).</summary>
        public object? Parse(string text)
        {
            var c = CultureInfo.InvariantCulture;
            try
            {
                return Type switch
                {
                    _ when Type == typeof(string) => text,
                    _ when Type.IsEnum => Enum.ToObject(Type, long.Parse(text, c)),
                    _ when Type == typeof(DateTime) => DateTime.Parse(text, c, DateTimeStyles.RoundtripKind),
                    _ when Type == typeof(DateTimeOffset) => DateTimeOffset.Parse(text, c, DateTimeStyles.RoundtripKind),
                    _ when Type == typeof(DateOnly) => DateOnly.Parse(text, c),
                    _ when Type == typeof(TimeOnly) => TimeOnly.Parse(text, c),
                    _ => Convert.ChangeType(text, Type, c),
                };
            }
            catch (Exception e) when (e is FormatException or OverflowException or InvalidCastException or ArgumentException)
            {
                return null;
            }
        }
    }

    private sealed class Rebind(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : node;
    }

    // Cursor -----------------------------------------------------------------------------------------------------------

    // [field, descending, sort value, id, offset] as base64url JSON. It only ever becomes typed values in a WHERE, never SQL text.
    private sealed record Cursor(string Field, bool Descending, string? Value, string? Id, int Offset)
    {
        private const int MaxLength = 1024;

        // single: the one sort whose value keyset paging continues after (null for several sorts, which page by offset).
        public static string For(string field, Sorting? single, bool descending, Key id, T last, int offset)
        {
            var value = single is not { } sort || ReferenceEquals(sort.Key, id) ? null : sort.Key.Text(sort.Key.Read(last));
            var json = JsonSerializer.Serialize(new object?[] { field, descending ? 1 : 0, value, id.Text(id.Read(last)), offset });
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        }

        public static Cursor? Parse(string text)
        {
            if (text.Length > MaxLength) return null;
            try
            {
                var base64 = text.Replace('-', '+').Replace('_', '/');
                base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
                using var doc = JsonDocument.Parse(Convert.FromBase64String(base64));
                var a = doc.RootElement;
                if (a.ValueKind != JsonValueKind.Array || a.GetArrayLength() != 5) return null;
                var offset = a[4].GetInt32();
                return offset < 0 ? null : new Cursor(a[0].GetString() ?? "", a[1].GetInt32() == 1, Str(a[2]), Str(a[3]), offset);
            }
            catch (Exception e) when (e is FormatException or JsonException or InvalidOperationException)
            {
                return null;
            }

            static string? Str(JsonElement e) => e.ValueKind == JsonValueKind.Null ? null : e.GetString();
        }
    }
}

/// <summary>One group's row count and totals, as <see cref="TableQuery{T}"/> reads them in a <c>GROUP BY</c> (not for direct use).</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TableGroupLeaf<TKey>
{
    public TKey Key { get; set; } = default!;
    public int Count { get; set; }
    public decimal V0 { get; set; }
    public decimal V1 { get; set; }
    public decimal V2 { get; set; }
    public decimal V3 { get; set; }
    public decimal V4 { get; set; }
    public decimal V5 { get; set; }
    public decimal V6 { get; set; }
    public decimal V7 { get; set; }
}

/// <summary>
/// A value as a member of a closure object, which LINQ providers send as a query parameter (not a literal in the SQL
/// text), so every value shares one cached plan.
/// </summary>
internal static class Box
{
    public static Expression Of(object? value, Type type)
    {
        var holder = Activator.CreateInstance(typeof(Holder<>).MakeGenericType(type), value)!;
        return Expression.Field(Expression.Constant(holder), holder.GetType().GetField(nameof(Holder<int>.Value))!);
    }

    private sealed class Holder<TValue>(TValue value)
    {
        public readonly TValue Value = value;
    }
}

/// <summary>A column's checkbox list (<c>=a|b</c>, <c>!=a|b</c>) as an expression tree: <c>IN</c> or <c>NOT IN</c> in SQL.</summary>
internal static class ValueListSearch
{
    private static readonly MethodInfo Contains = typeof(Enumerable).GetMethods()
        .Single(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2).MakeGenericMethod(typeof(string));

    private static readonly MethodInfo ToLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;

    /// <summary>
    /// The test of a value list over text (<paramref name="body"/>, a string), or null when <paramref name="query"/> isn't
    /// one. An empty value is a blank cell (null or empty). <paramref name="ignoreCase"/> compares lower-cased text.
    /// </summary>
    public static Expression? Test(string query, Expression body, bool ignoreCase) =>
        ColumnSearch.ValueList(query.Trim()) is { } list ? Test(list.Exclude, list.Values, body, ignoreCase) : null;

    /// <summary>The test of a parsed value list (<see cref="ColumnSearch.ValueList"/>) over text.</summary>
    public static Expression Test(bool exclude, IReadOnlySet<string> listed, Expression body, bool ignoreCase)
    {
        var values = listed.Where(v => v.Length > 0).Select(v => ignoreCase ? v.ToLowerInvariant() : v).ToList();
        var subject = ignoreCase ? Expression.Call(body, ToLower) : body;
        var isNull = Expression.Equal(body, Expression.Constant(null, typeof(string)));
        Expression test = Expression.Constant(false);
        if (values.Count > 0)
            test = Expression.AndAlso(Expression.Not(isNull), Expression.Call(Contains, Box.Of(values, typeof(List<string>)), subject));
        if (listed.Contains(""))
            test = Expression.OrElse(test, Expression.OrElse(isNull, Expression.Equal(body, Expression.Constant(""))));
        return exclude ? Expression.Not(test) : test;
    }
}
