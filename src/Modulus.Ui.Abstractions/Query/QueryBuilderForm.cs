using System.Globalization;

namespace Modulus.Ui.Abstractions.Query;

/// <summary>
/// The builder's form fields and the operations on its filter, shared by the MVC renderer and the Blazor component. A
/// node is named by its path among its siblings (<c>0</c> is the first item, <c>0.2</c> the third item of the group at
/// <c>0</c>); the top group's join is <c>{name}.join</c>. Buttons name the operation (<c>{name}.op</c>): <c>add-rule:0</c>,
/// <c>add-group:</c>, <c>remove:0.1</c>, <c>clear</c>.
/// </summary>
public static class QueryBuilderForm
{
    /// <summary>The usual name of the builder's fields: <c>f.0.field</c>.</summary>
    public const string DefaultName = "f";

    /// <summary>Most nodes read from a form (the limits of <see cref="FilterLimits"/> apply after).</summary>
    public const int MaxNodes = 200;

    private static readonly string[] Props = ["kind", "join", "field", "op", "value", "to"];

    /// <summary>The filter a query string or form posted: the nodes named under <paramref name="name"/>.</summary>
    public static FilterGroup Read(IEnumerable<KeyValuePair<string, string?>> query, string name = DefaultName)
    {
        var top = FilterJoin.And;
        var nodes = new Dictionary<string, Dictionary<string, string>>(StringComparer.Ordinal);
        var prefix = name + ".";
        foreach (var (key, value) in query)
        {
            if (value is null || !key.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var rest = key[prefix.Length..];
            if (rest == "join")
            {
                if (Enum.TryParse<FilterJoin>(value, ignoreCase: true, out var join)) top = join;
                continue;
            }
            var dot = rest.LastIndexOf('.');
            if (dot <= 0) continue;
            var path = rest[..dot];
            var prop = rest[(dot + 1)..];
            if (!Props.Contains(prop) || ParsePath(path) is null) continue;
            if (!nodes.TryGetValue(path, out var props))
            {
                if (nodes.Count >= MaxNodes) continue;
                nodes[path] = props = new Dictionary<string, string>(StringComparer.Ordinal);
            }
            // A multi-select posts one pair per option: its values join with the list separator.
            props[prop] = props.TryGetValue(prop, out var previous) ? previous + ColumnSearch.ValueSeparator + value : value;
        }
        return Build("", top, nodes, depth: 0);
    }

    /// <summary>
    /// The filter after a button: <c>clear</c>, <c>add-rule:{path}</c> (a rule on the first field, in the group at
    /// <paramref name="op"/>'s path), <c>add-group:{path}</c>, or <c>remove:{path}</c>. A change the limits refuse, or a
    /// path that isn't there, leaves the filter as it is.
    /// </summary>
    public static FilterGroup Edit(IReadOnlyList<QueryField> schema, FilterGroup model, string? op)
    {
        if (op == "clear") return FilterGroup.Empty;
        var colon = op?.IndexOf(':') ?? -1;
        if (op is null || colon < 0 || ParsePath(op[(colon + 1)..]) is not { } path) return model;
        switch (op[..colon])
        {
            case "add-rule" when schema.Count > 0 && path.Length < FilterLimits.MaxDepth && CountRules(model) < FilterLimits.MaxRules:
                var first = schema[0];
                var rule = new FilterRule(first.Key, QueryFieldOperators.For(first.Kind)[0]);
                return Change(model, path, 0, g => g with { Items = [.. g.Items, rule] });
            case "add-group" when path.Length + 1 < FilterLimits.MaxDepth:
                return Change(model, path, 0, g => g with { Items = [.. g.Items, FilterGroup.Empty] });
            case "remove" when path.Length > 0:
                return Replace(model, path, null);
            default:
                return model;
        }
    }

    /// <summary>The filter with the node at <paramref name="path"/> replaced (a rule edited in the Blazor component); a path that isn't there changes nothing.</summary>
    public static FilterGroup Replace(FilterGroup model, string path, FilterNode node) =>
        ParsePath(path) is { Length: > 0 } indexes ? Replace(model, indexes, node) : model;

    /// <summary>The path of a node's name ("0.2"), or null when it isn't one.</summary>
    public static string PathOf(IEnumerable<int> indexes) => string.Join('.', indexes.Select(i => i.ToString(CultureInfo.InvariantCulture)));

    // Reading ---------------------------------------------------------------------------------------------------

    // The path's indexes, or null when a segment isn't a small non-negative number. The empty path is the top group.
    internal static int[]? ParsePath(string path)
    {
        if (path.Length == 0) return [];
        var parts = path.Split('.');
        if (parts.Length > FilterLimits.MaxDepth * 10) return null;
        var indexes = new int[parts.Length];
        for (var i = 0; i < parts.Length; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out var index) || index >= 1000) return null;
            indexes[i] = index;
        }
        return indexes;
    }

    private static FilterGroup Build(string path, FilterJoin join, Dictionary<string, Dictionary<string, string>> nodes, int depth)
    {
        var children = nodes.Keys.Where(p => Parent(p) == path).OrderBy(Last).ToList();
        var items = new List<FilterNode>();
        foreach (var child in children)
        {
            var props = nodes[child];
            if (props.GetValueOrDefault("kind") == "group")
            {
                if (depth + 1 < FilterLimits.MaxDepth)
                {
                    var childJoin = Enum.TryParse<FilterJoin>(props.GetValueOrDefault("join"), ignoreCase: true, out var j) ? j : FilterJoin.And;
                    items.Add(Build(child, childJoin, nodes, depth + 1));
                }
            }
            else if (props.GetValueOrDefault("kind") == "rule"
                && Enum.TryParse<FilterOperator>(props.GetValueOrDefault("op"), ignoreCase: true, out var op))
            {
                items.Add(new FilterRule(props.GetValueOrDefault("field") ?? "", op, props.GetValueOrDefault("value"), props.GetValueOrDefault("to")));
            }
        }
        return new FilterGroup(join, items);
    }

    private static string Parent(string path)
    {
        var dot = path.LastIndexOf('.');
        return dot < 0 ? "" : path[..dot];
    }

    private static int Last(string path) => int.Parse(path[(path.LastIndexOf('.') + 1)..], CultureInfo.InvariantCulture);

    // Editing ---------------------------------------------------------------------------------------------------

    private static int CountRules(FilterGroup group) => group.Items.Sum(i => i is FilterGroup g ? CountRules(g) : 1);

    // Applies a change to the group at the path (the top group for an empty path). A path that isn't there returns the
    // group itself, so a refused edit is the same filter, not an equal copy.
    private static FilterGroup Change(FilterGroup group, int[] path, int depth, Func<FilterGroup, FilterGroup> change)
    {
        if (depth == path.Length) return change(group);
        if (path[depth] >= group.Items.Count || group.Items[path[depth]] is not FilterGroup sub) return group;
        var changed = Change(sub, path, depth + 1, change);
        if (ReferenceEquals(changed, sub)) return group;
        var items = group.Items.ToList();
        items[path[depth]] = changed;
        return group with { Items = items };
    }

    // Removes the node at the path (when node is null) or puts a node in its place.
    private static FilterGroup Replace(FilterGroup model, int[] path, FilterNode? node) =>
        Change(model, path[..^1], 0, g =>
        {
            var index = path[^1];
            if (index >= g.Items.Count) return g;
            var items = g.Items.ToList();
            if (node is null) items.RemoveAt(index);
            else items[index] = node;
            return g with { Items = items };
        });
}
