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
        })
        { GroupField = group };
        return plan;
    }
}
