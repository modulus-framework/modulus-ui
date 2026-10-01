using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Modulus.Ui.Abstractions.Pivot;

/// <summary>How a measure combines the values of a group (Odoo's <c>aggregator</c>); the record count is the <c>count</c> measure.</summary>
public enum PivotAggregate
{
    Sum,
    Average,
    Min,
    Max,
}

/// <summary>The steps a date field groups by (<c>date:month</c>).</summary>
public enum PivotInterval
{
    Year,
    Quarter,
    Month,
    Week,
    Day,
}

/// <summary>A field the pivot can group by, for the header dropdowns.</summary>
/// <param name="Key">The group-by key (<c>customer</c>), or the field of a date (<c>date</c>, grouped as <c>date:month</c>).</param>
public sealed record PivotFieldInfo(string Key, string Title, bool IsDate);

/// <summary>A measure the pivot can show.</summary>
public sealed record PivotMeasureInfo(string Key, string Title, int Decimals);

/// <summary>
/// The group key of a pivot query: one slot per group-by level, text or number. A record, so in-memory sources group
/// by value; database providers only see the slots that are set.
/// </summary>
public sealed record PivotKey
{
    public string? S0 { get; init; }
    public string? S1 { get; init; }
    public string? S2 { get; init; }
    public string? S3 { get; init; }
    public string? S4 { get; init; }
    public string? S5 { get; init; }
    public string? S6 { get; init; }
    public string? S7 { get; init; }
    public long? N0 { get; init; }
    public long? N1 { get; init; }
    public long? N2 { get; init; }
    public long? N3 { get; init; }
    public long? N4 { get; init; }
    public long? N5 { get; init; }
    public long? N6 { get; init; }
    public long? N7 { get; init; }
}

/// <summary>One group of a level, as the database returns it: its key, record count and one value per measure.</summary>
public sealed class PivotLeaf
{
    public PivotKey Key { get; set; } = new();
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
/// One grouped query of a pivot: the groups of the first <paramref name="RowDepth"/> row group-bys and the first
/// <paramref name="ColumnDepth"/> column group-bys, inside the headers that are open at those depths.
/// </summary>
public sealed record PivotLevel(int RowDepth, int ColumnDepth, IQueryable<PivotLeaf> Groups);

/// <summary>
/// A pivot table over a queryable source, in the manner of <see cref="Data.TableQuery{T}"/>: only registered fields
/// group and only registered measures aggregate, so nothing from the query string reaches the query unchecked.
/// <code>
/// static readonly PivotQuery&lt;Shipment&gt; Pivot = PivotQuery&lt;Shipment&gt;.Create()
///     .Dimension("customer", "Customer", s => s.Customer)
///     .Dimension("status", "Status", s => s.Status)
///     .Date("date", "Date", s => s.Date)
///     .Measure("amount", "Amount", s => s.Amount)
///     .Default(rows: ["customer"], columns: ["date:year"], measures: ["count", "amount"]);
///
/// var state = Pivot.State(query);                          // from the query string, checked
/// var result = Pivot.Run(db.Shipments.Where(…), state);    // GROUP BYs in the database
/// </code>
/// <para>
/// As in Odoo, the pivot reads only what it shows. <see cref="Run"/> sends one <c>GroupBy</c> per pair of depths (row
/// depth × column depth) that has open headers, each filtered to the records under those open headers. Every query
/// returns a record count and one sum (or min, or max) per measure for each of its groups, so every total comes
/// straight from the database; averages are sums divided by counts. A closed header costs one group, however many
/// records or group-bys lie under it.
/// </para>
/// </summary>
public sealed class PivotQuery<T>
{
    /// <summary>Group-by levels of both axes together.</summary>
    public const int MaxLevels = 8;

    /// <summary>Measures shown at once (<c>count</c> included).</summary>
    public const int MaxMeasures = 8;

    /// <summary>The record count measure, always available.</summary>
    public const string Count = "count";

    private readonly List<Field> _fields = [];
    private readonly List<MeasureDef> _measures = [];
    private PivotState _default = new() { Measures = [Count] };
    private int _maxGroups = 10_000;

    private PivotQuery() { }

    public static PivotQuery<T> Create() => new();

    /// <summary>The fields to group by, in the order the header dropdown lists them.</summary>
    public IReadOnlyList<PivotFieldInfo> Fields => _fields.Select(f => new PivotFieldInfo(f.Key, f.Title, f.IsDate)).ToArray();

    /// <summary>The measures, in the order the Measures menu lists them (<c>count</c> last, as in Odoo).</summary>
    public IReadOnlyList<PivotMeasureInfo> Measures =>
        _measures.Select(m => new PivotMeasureInfo(m.Key, m.Title, m.Decimals)).Append(new PivotMeasureInfo(Count, "Count", 0)).ToArray();

    /// <summary>The state of a page that names none.</summary>
    public PivotState DefaultState => _default;

    /// <summary>Most groups one table may read (about the cells it shows); beyond it the result is an error, not a huge table.</summary>
    public int MaxGroups => _maxGroups;

    /// <summary>Groups by a text value; a null groups as "None".</summary>
    public PivotQuery<T> Dimension(string key, string title, Expression<Func<T, string?>> value) =>
        Add(new Field(key, title, false, _ => (value, true), (_, k) => k.Length == 0 ? "None" : k));

    /// <summary>Groups by an enum value, labelled with <paramref name="label"/> or the member's name. The database groups the number.</summary>
    public PivotQuery<T> Dimension<TEnum>(string key, string title, Expression<Func<T, TEnum>> value, Func<TEnum, string>? label = null)
        where TEnum : struct, Enum
    {
        var number = Expression.Lambda(Expression.Convert(Expression.Convert(value.Body, typeof(long)), typeof(long?)), value.Parameters);
        return Add(new Field(key, title, false, _ => (number, false), (_, k) =>
        {
            if (!long.TryParse(k, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)) return "None";
            var member = (TEnum)Enum.ToObject(typeof(TEnum), n);
            return label?.Invoke(member) ?? member.ToString();
        }));
    }

    /// <summary>Groups by a date, by year, quarter, month, week (from Monday) or day (<c>{key}:month</c>).</summary>
    public PivotQuery<T> Date(string key, string title, Expression<Func<T, DateOnly>> value)
    {
        var d = value.Body;
        Expression P(string name) => Expression.Convert(Expression.Property(d, name), typeof(long));
        Expression C(long v) => Expression.Constant(v);
        LambdaExpression Keyed(Expression body) => Expression.Lambda(Expression.Convert(body, typeof(long?)), value.Parameters);
        var year = P(nameof(DateOnly.Year));
        var month = P(nameof(DateOnly.Month));
        var keys = new Dictionary<PivotInterval, LambdaExpression>
        {
            [PivotInterval.Year] = Keyed(year),
            // 20243 is the third quarter of 2024; 202407 is July 2024; 20240715 the 15th of July. All sort as numbers.
            [PivotInterval.Quarter] = Keyed(Expression.Add(Expression.Multiply(year, C(10)), Expression.Divide(Expression.Add(month, C(2)), C(3)))),
            [PivotInterval.Month] = Keyed(Expression.Add(Expression.Multiply(year, C(100)), month)),
            // Days since 0001-01-01, a Monday, in whole weeks: the week's number, from Monday.
            [PivotInterval.Week] = Keyed(Expression.Divide(P(nameof(DateOnly.DayNumber)), C(7))),
            [PivotInterval.Day] = Keyed(Expression.Add(Expression.Multiply(year, C(10_000)), Expression.Add(Expression.Multiply(month, C(100)), P(nameof(DateOnly.Day))))),
        };
        return Add(new Field(key, title, true, interval => (keys[interval], false), DateLabel));
    }

    /// <summary>A measure: the <paramref name="aggregate"/> of a number per group, shown with <paramref name="decimals"/> decimals.</summary>
    public PivotQuery<T> Measure(string key, string title, Expression<Func<T, decimal>> value, PivotAggregate aggregate = PivotAggregate.Sum, int decimals = 2)
    {
        if (key == Count || _measures.Any(m => m.Key == key)) throw new ArgumentException($"The measure '{key}' is already registered.", nameof(key));
        _measures.Add(new MeasureDef(key, title, value, aggregate, decimals));
        return this;
    }

    /// <summary>A whole-number measure (quantities).</summary>
    public PivotQuery<T> Measure(string key, string title, Expression<Func<T, int>> value, PivotAggregate aggregate = PivotAggregate.Sum, int decimals = 0) =>
        Measure(key, title, Expression.Lambda<Func<T, decimal>>(Expression.Convert(value.Body, typeof(decimal)), value.Parameters), aggregate, decimals);

    /// <summary>What a page shows when its URL names no state: these group-bys, with both "Total" headers open.</summary>
    public PivotQuery<T> Default(IReadOnlyList<string> rows, IReadOnlyList<string> columns, IReadOnlyList<string> measures)
    {
        var state = new PivotState { Rows = rows, Columns = columns, Measures = measures };
        state = state with
        {
            RowPaths = rows.Count > 0 ? [PivotPath.Root] : [],
            ColumnPaths = columns.Count > 0 ? [PivotPath.Root] : [],
        };
        _default = Normalize(state);
        return this;
    }

    /// <summary>Most groups one table may read (default 10,000).</summary>
    public PivotQuery<T> Limit(int maxGroups)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxGroups, 1);
        _maxGroups = maxGroups;
        return this;
    }

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
        var known = Measures.Select(m => m.Key).ToHashSet(StringComparer.Ordinal);
        var measures = state.Measures.Where(known.Contains).Distinct().Take(MaxMeasures).ToArray();
        if (measures.Length == 0) measures = _default.Measures.Count > 0 ? [.. _default.Measures] : [Count];
        var sorted = state.SortMeasure is { } s && measures.Contains(s);
        return state with
        {
            Rows = rows,
            Columns = columns,
            Measures = measures,
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

    /// <summary>
    /// The grouped queries of a state: one per row depth and column depth that shows groups, deepest first, each over
    /// the records under the open headers of those depths. They stay <see cref="IQueryable{T}"/>s, so a database provider
    /// runs each as one <c>GROUP BY</c>.
    /// </summary>
    public IReadOnlyList<PivotLevel> Queries(IQueryable<T> source, PivotState state)
    {
        state = Normalize(state);
        var rows = Opened(state, PivotAxis.Rows);
        var columns = Opened(state, PivotAxis.Columns);
        var list = new List<PivotLevel>();
        for (var i = rows.Count; i >= 0; i--)
        for (var j = columns.Count; j >= 0; j--)
        {
            var query = source;
            if (i > 0 && rows[i - 1] is { } r) query = query.Where(r);
            if (j > 0 && columns[j - 1] is { } c) query = query.Where(c);
            list.Add(new PivotLevel(i, j, Group(query, state.Rows.Take(i).Concat(state.Columns.Take(j)).ToArray(), state)));
        }
        return list;
    }

    /// <summary>Runs the pivot: the grouped queries of the headers that show, then the table.</summary>
    public PivotResult Run(IQueryable<T> source, PivotState state)
    {
        state = Normalize(state);
        var measures = state.Measures.Select(k => Measures.First(m => m.Key == k)).ToArray();
        var left = _maxGroups;
        var levels = new List<(int RowDepth, int ColumnDepth, IReadOnlyList<PivotLeaf> Groups)>();
        foreach (var level in Queries(source, state))
        {
            var groups = level.Groups.Take(left + 1).ToList();
            if (groups.Count > left)
                return PivotResult.TooMany(state, Fields, Measures, measures, _maxGroups, GroupByTitle);
            left -= groups.Count;
            levels.Add((level.RowDepth, level.ColumnDepth, groups));
        }
        var aggregates = state.Measures.Where(m => m != Count).Select(m => _measures.First(x => x.Key == m).Aggregate).ToArray();
        return PivotResult.Build(state, Fields, Measures, measures, aggregates, levels, Label, GroupByTitle, Order);
    }

    /// <summary>
    /// For each depth of an axis, from the top, which records lie under a header of that depth that is open and shows:
    /// null for all of them. The list stops at the first depth with no open header.
    /// </summary>
    private List<Expression<Func<T, bool>>?> Opened(PivotState state, PivotAxis axis)
    {
        var groupBys = state.GroupBys(axis);
        var all = axis == PivotAxis.Rows ? state.RowsAllDepth : state.ColumnsAllDepth;
        var paths = axis == PivotAxis.Rows ? state.RowPaths : state.ColumnPaths;
        var x = Expression.Parameter(typeof(T), "x");
        var list = new List<Expression<Func<T, bool>>?>();
        for (var depth = 0; depth < groupBys.Count; depth++)
        {
            Expression? filter;
            if (depth < all)
            {
                // Expand all: open, unless it or a header above it is closed (the paths name the closed ones).
                var closed = paths.Where(p => PivotPath.Depth(p) <= depth).ToArray();
                if (closed.Contains(PivotPath.Root)) break;
                filter = closed.Length == 0 ? null : Expression.Not(AnyOf(closed, groupBys, x));
            }
            else
            {
                // Open when listed, and the headers above it show.
                var open = paths.Where(p => PivotPath.Depth(p) == depth && state.Shows(axis, p)).ToArray();
                if (open.Length == 0) break;
                filter = open.Contains(PivotPath.Root) ? null : AnyOf(open, groupBys, x);
            }
            list.Add(filter is null ? null : Expression.Lambda<Func<T, bool>>(filter, x));
        }
        return list;
    }

    /// <summary>Whether a record lies under one of the headers (<c>x.City == "Berlin" &amp;&amp; x.Date.Year == 2024 || …</c>).</summary>
    private Expression AnyOf(IReadOnlyList<string> paths, IReadOnlyList<string> groupBys, ParameterExpression x)
    {
        Expression? any = null;
        foreach (var path in paths)
        {
            Expression match = Expression.Constant(true);
            var keys = PivotPath.Keys(path);
            for (var k = 0; k < keys.Count; k++)
            {
                var (lambda, text) = KeyOf(groupBys[k]);
                var eq = KeyEquals(Rebind(lambda, x), keys[k], text);
                match = k == 0 ? eq : Expression.AndAlso(match, eq);
            }
            any = any is null ? match : Expression.OrElse(any, match);
        }
        return any ?? Expression.Constant(false);
    }

    /// <summary>
    /// A group key equals a header's key: "None" (empty) is null; values go in as parameters, not into the SQL text.
    /// </summary>
    private static Expression KeyEquals(Expression key, string value, bool text)
    {
        if (text)
        {
            if (value.Length == 0)
                return Expression.OrElse(Expression.Equal(key, Expression.Constant(null, typeof(string))), Expression.Equal(key, Expression.Constant("")));
            return Expression.Equal(key, Parameter<string?>(value));
        }
        if (value.Length == 0) return Expression.Equal(key, Expression.Constant(null, typeof(long?)));
        return long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)
            ? Expression.Equal(key, Parameter<long?>(n))
            : Expression.Constant(false); // a key no group has (a hand-edited URL)
    }

    /// <summary>A value as a closure member, which database providers send as a query parameter.</summary>
    private static Expression Parameter<TValue>(TValue value) =>
        Expression.Property(Expression.Constant(new Boxed<TValue>(value)), nameof(Boxed<TValue>.Value));

    private sealed class Boxed<TValue>(TValue value)
    {
        public TValue Value { get; } = value;
    }

    /// <summary>
    /// <c>source.GroupBy(x => new PivotKey { S0 = x.City, N1 = … }).Select(g => new PivotLeaf { Key = g.Key, Count =
    /// g.Count(), V0 = g.Sum(y => y.Amount), … })</c>: the rows' group-bys in the first slots, then the columns'.
    /// </summary>
    private IQueryable<PivotLeaf> Group(IQueryable<T> source, IReadOnlyList<string> levels, PivotState state)
    {
        var measures = state.Measures.Where(m => m != Count).Select(m => _measures.First(x => x.Key == m)).ToArray();

        var x = Expression.Parameter(typeof(T), "x");
        var bindings = new List<MemberBinding>();
        for (var i = 0; i < levels.Count; i++)
        {
            var (lambda, text) = KeyOf(levels[i]);
            bindings.Add(Expression.Bind(typeof(PivotKey).GetProperty((text ? "S" : "N") + i)!, Rebind(lambda, x)));
        }
        var keySelector = Expression.Lambda<Func<T, PivotKey>>(Expression.MemberInit(Expression.New(typeof(PivotKey)), bindings), x);
        var grouped = source.GroupBy(keySelector);

        var g = Expression.Parameter(typeof(IGrouping<PivotKey, T>), "g");
        var leaf = new List<MemberBinding>
        {
            Expression.Bind(typeof(PivotLeaf).GetProperty(nameof(PivotLeaf.Key))!, Expression.Property(g, nameof(IGrouping<PivotKey, T>.Key))),
            Expression.Bind(typeof(PivotLeaf).GetProperty(nameof(PivotLeaf.Count))!, Expression.Call(CountMethod, g)),
        };
        for (var i = 0; i < measures.Length; i++)
        {
            var method = measures[i].Aggregate switch
            {
                PivotAggregate.Min => MinMethod,
                PivotAggregate.Max => MaxMethod,
                _ => SumMethod,
            };
            leaf.Add(Expression.Bind(typeof(PivotLeaf).GetProperty("V" + i)!, Expression.Call(method, g, measures[i].Value)));
        }
        var select = Expression.Lambda<Func<IGrouping<PivotKey, T>, PivotLeaf>>(Expression.MemberInit(Expression.New(typeof(PivotLeaf)), leaf), g);
        return grouped.Select(select);
    }

    // Fields ---------------------------------------------------------------------------------------------------

    private sealed record Field(string Key, string Title, bool IsDate, Func<PivotInterval, (LambdaExpression Key, bool Text)> KeyFor,
        Func<PivotInterval, string, string> Label);

    private sealed record MeasureDef(string Key, string Title, Expression<Func<T, decimal>> Value, PivotAggregate Aggregate, int Decimals);

    private PivotQuery<T> Add(Field field)
    {
        if (field.Key.Length == 0 || field.Key.Contains(',') || field.Key.Contains(':'))
            throw new ArgumentException("A field key is not empty and has no ',' or ':'.", nameof(field));
        if (_fields.Any(f => f.Key == field.Key)) throw new ArgumentException($"The field '{field.Key}' is already registered.", nameof(field));
        _fields.Add(field);
        return this;
    }

    private static (string Name, string? Interval) Split(string groupBy)
    {
        var colon = groupBy.IndexOf(':');
        return colon < 0 ? (groupBy, null) : (groupBy[..colon], groupBy[(colon + 1)..]);
    }

    private (LambdaExpression Key, bool Text) KeyOf(string groupBy)
    {
        var (name, interval) = Split(groupBy);
        var field = _fields.First(f => f.Key == name);
        return field.KeyFor(interval is null ? PivotInterval.Month : Enum.Parse<PivotInterval>(interval, true));
    }

    /// <summary>A group's label (<c>March 2025</c>, <c>None</c>).</summary>
    private string Label(string groupBy, string key)
    {
        var (name, interval) = Split(groupBy);
        var field = _fields.First(f => f.Key == name);
        return field.Label(interval is null ? PivotInterval.Month : Enum.Parse<PivotInterval>(interval, true), key);
    }

    /// <summary>How the groups of a level sort: dates and enums by their number, text by the culture; "None" last.</summary>
    private int Order(string groupBy, string a, string b)
    {
        if (a.Length == 0 || b.Length == 0) return (a.Length == 0).CompareTo(b.Length == 0);
        if (KeyOf(groupBy).Text) return string.Compare(a, b, StringComparison.CurrentCultureIgnoreCase);
        return long.Parse(a, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture)
            .CompareTo(long.Parse(b, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture));
    }

    private static string DateLabel(PivotInterval interval, string key)
    {
        if (!long.TryParse(key, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var n)) return "None";
        var culture = CultureInfo.CurrentCulture;
        return interval switch
        {
            PivotInterval.Year => n.ToString(CultureInfo.InvariantCulture),
            PivotInterval.Quarter => $"Q{n % 10} {n / 10}",
            PivotInterval.Month => new DateTime((int)(n / 100), (int)(n % 100), 1).ToString("MMMM yyyy", culture),
            PivotInterval.Week => WeekLabel(DateOnly.FromDayNumber((int)(n * 7))),
            _ => new DateTime((int)(n / 10_000), (int)(n / 100 % 100), (int)(n % 100)).ToString("dd MMM yyyy", culture),
        };

        static string WeekLabel(DateOnly monday)
        {
            var date = monday.ToDateTime(TimeOnly.MinValue);
            return $"W{ISOWeek.GetWeekOfYear(date).ToString("00", CultureInfo.InvariantCulture)} {ISOWeek.GetYear(date).ToString(CultureInfo.InvariantCulture)}";
        }
    }

    // Expressions ----------------------------------------------------------------------------------------------

    private static readonly MethodInfo CountMethod = new Func<IEnumerable<T>, int>(Enumerable.Count).Method;
    private static readonly MethodInfo SumMethod = new Func<IEnumerable<T>, Func<T, decimal>, decimal>(Enumerable.Sum).Method;
    private static readonly MethodInfo MinMethod = new Func<IEnumerable<T>, Func<T, decimal>, decimal>(Enumerable.Min).Method;
    private static readonly MethodInfo MaxMethod = new Func<IEnumerable<T>, Func<T, decimal>, decimal>(Enumerable.Max).Method;

    private static Expression Rebind(LambdaExpression lambda, ParameterExpression to) =>
        new Replace(lambda.Parameters[0], to).Visit(lambda.Body);

    private sealed class Replace(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }
}
