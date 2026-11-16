using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Modulus.Ui.Abstractions.Pivot;

public sealed partial class PivotQuery<T>
{
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
        var stored = Stored(state.Measures);
        var aggregates = stored.Select(m => _measures.First(x => x.Key == m).Aggregate).ToArray();
        var ratios = _ratios.ToDictionary(r => r.Key, r => (r.Numerator, r.Denominator), StringComparer.Ordinal);
        return PivotResult.Build(state, Fields, Measures, measures, stored, aggregates, ratios, levels, Label, GroupByTitle, Order);
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
    private bool IsBase(string key) => key == Count || _measures.Any(m => m.Key == key);

    // The sums a set of shown measures needs from the database, in slot order: a ratio needs its two measures.
    private IReadOnlyList<string> Stored(IEnumerable<string> shown)
    {
        var list = new List<string>();
        void Add(string key)
        {
            if (key != Count && !list.Contains(key)) list.Add(key);
        }
        foreach (var key in shown)
        {
            if (_ratios.Find(r => r.Key == key) is { } ratio)
            {
                Add(ratio.Numerator);
                Add(ratio.Denominator);
            }
            else Add(key);
        }
        return list;
    }

    private IQueryable<PivotLeaf> Group(IQueryable<T> source, IReadOnlyList<string> levels, PivotState state)
    {
        var measures = Stored(state.Measures).Select(m => _measures.First(x => x.Key == m)).ToArray();

        var x = Expression.Parameter(typeof(T), "x");
        var bindings = new List<MemberBinding>();
        for (var i = 0; i < levels.Count; i++)
        {
            var (lambda, text) = KeyOf(levels[i]);
            bindings.Add(Expression.Bind(typeof(PivotKey).GetProperty((text ? "S" : "N") + i)!, Rebind(lambda, x)));
        }
        var keySelector = Expression.Lambda<Func<T, PivotKey>>(
            bindings.Count > 0 ? Expression.MemberInit(Expression.New(typeof(PivotKey)), bindings) : Expression.New(typeof(PivotKey)), x);
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
}
