using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using Modulus.Ui.Abstractions.Query;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Modulus.Ui.Abstractions.Pivot;

namespace Modulus.Ui.Abstractions.Data;

public sealed partial class TableQuery<T>
{
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
}
