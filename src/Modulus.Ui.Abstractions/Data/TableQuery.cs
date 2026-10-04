using System.Globalization;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;

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
public sealed record TablePage<T>(IReadOnlyList<T> Items, int Offset, int PageSize, int? Total, bool HasMore, string? NextCursor);

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
/// sort key, then by the id, so pages never overlap or skip rows that share a value.
/// </para>
/// <para>
/// Paging: <see cref="Page"/> reads a numbered page (<c>OFFSET</c>). With a cursor (infinite and virtual scrolling) the next
/// page continues after the last row's sort value and id (keyset paging: <c>WHERE (date, id) &lt; (…)</c>), which costs
/// the same however deep the user scrolls. Keys that may be null, and types that can't be compared in SQL, fall back to
/// <c>OFFSET</c> for the cursor.
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
    private string? _defaultField;
    private bool _defaultDescending;

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

    /// <summary>Lets the column search of <paramref name="field"/> (<c>s.{field}</c>) filter with <paramref name="predicate"/> (given the trimmed text).</summary>
    public TableQuery<T> Search(string field, Func<string, Expression<Func<T, bool>>> predicate)
    {
        _searches[field] = predicate;
        return this;
    }

    /// <summary>
    /// A column search that keeps rows whose text contains the query (<c>LIKE '%…%'</c>; case follows the database
    /// collation). <paramref name="ignoreCase"/> compares lower-cased text (<c>LOWER(col) LIKE</c>), for in-memory sources
    /// and case-sensitive collations; it can't use an index, so prefer a case-insensitive collation on large tables.
    /// </summary>
    public TableQuery<T> SearchText(string field, Expression<Func<T, string?>> text, bool ignoreCase = false)
    {
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
        var toLower = typeof(string).GetMethod(nameof(string.ToLower), Type.EmptyTypes)!;
        return Search(field, query =>
        {
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
            source = source.Where(predicate(query.Length > MaxSearchLength ? query[..MaxSearchLength] : query));
        }
        return source;
    }

    /// <summary>The source filtered and in the request's order (sort key, then id).</summary>
    public IQueryable<T> Apply(IQueryable<T> source, ProTableRequest request) => Order(Filter(source, request), Resolve(request));

    /// <summary>The page the request asks for: its numbered page, or the rows after its <see cref="ProTableRequest.Cursor"/>.</summary>
    public TablePage<T> Page(IQueryable<T> source, ProTableRequest request, CountMode count = CountMode.Exact)
    {
        var plan = Plan(source, request, count);
        return plan.Finish(plan.Rows.ToList(), plan.Count?.Invoke());
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
        return plan.Finish(await ToListAsync(plan.Rows, cancellationToken).ConfigureAwait(false), total);
    }

    // Planning ---------------------------------------------------------------------------------------------------------

    private readonly record struct Sorting(string Field, Key Key, bool Descending);

    private Sorting Resolve(ProTableRequest request)
    {
        if (request.SortField is { } field && request.SortOrder != SortDirection.None && _sorts.TryGetValue(field, out var key))
            return new(field.ToLowerInvariant(), key, request.SortOrder == SortDirection.Descending);
        if (_defaultField is not null) return new(_defaultField.ToLowerInvariant(), _sorts[_defaultField], _defaultDescending);
        return new("", _id, false);
    }

    private IQueryable<T> Order(IQueryable<T> source, Sorting sort)
    {
        var ordered = Call(source, sort.Descending ? "OrderByDescending" : "OrderBy", sort.Key);
        // Ties go by id in the same direction, which keeps the order total (and keyset paging exact).
        return ReferenceEquals(sort.Key, _id) ? ordered : Call(ordered, sort.Descending ? "ThenByDescending" : "ThenBy", _id);
    }

    private static IQueryable<T> Call(IQueryable<T> source, string method, Key key) =>
        source.Provider.CreateQuery<T>(Expression.Call(typeof(Queryable), method, [typeof(T), key.Type], source.Expression, Expression.Quote(key.Lambda)));

    private sealed class QueryPlan(IQueryable<T> filtered, IQueryable<T> rows, Func<int>? count, Func<List<T>, int?, TablePage<T>> finish)
    {
        public IQueryable<T> Filtered { get; } = filtered;
        public IQueryable<T> Rows { get; } = rows;
        public Func<int>? Count { get; } = count;
        public TablePage<T> Finish(List<T> rows, int? total) => finish(rows, total);
    }

    private QueryPlan Plan(IQueryable<T> source, ProTableRequest request, CountMode count)
    {
        var sort = Resolve(request);
        var filtered = Filter(source, request);
        var ordered = Order(filtered, sort);
        var size = request.PageSize;

        var offset = request.Skip;
        IQueryable<T> page;
        if (request.Cursor is { Length: > 0 } text && Cursor.Parse(text) is { } cursor && cursor.Field == sort.Field && cursor.Descending == sort.Descending)
        {
            offset = cursor.Offset;
            page = After(ordered, sort, cursor) is { } keyset ? keyset : ordered.Skip(offset);
        }
        else page = ordered.Skip(offset);

        // One row more than the page tells whether another page follows, without counting.
        var rows = page.Take(size + 1);
        Func<int>? counter = count == CountMode.Exact ? () => filtered.Count() : null;
        return new QueryPlan(filtered, rows, counter, (list, total) =>
        {
            var more = list.Count > size;
            if (more) list.RemoveAt(list.Count - 1);
            var next = more && list.Count > 0 ? Cursor.For(sort, _id, list[^1], offset + list.Count) : null;
            return new TablePage<T>(list, offset, size, total, more, next);
        });
    }

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

    private static async Task<List<T>> ToListAsync(IQueryable<T> query, CancellationToken cancellationToken)
    {
        if (query is not IAsyncEnumerable<T> rows) return query.ToList();
        var list = new List<T>();
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

        public static string For(Sorting sort, Key id, T last, int offset)
        {
            var value = ReferenceEquals(sort.Key, id) ? null : sort.Key.Text(sort.Key.Read(last));
            var json = JsonSerializer.Serialize(new object?[] { sort.Field, sort.Descending ? 1 : 0, value, id.Text(id.Read(last)), offset });
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
