using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Modulus.Ui.Abstractions;

namespace Modulus.Ui.Abstractions.Query;

/// <summary>
/// The fields a visual filter can ask about, registered like <see cref="Data.TableQuery{T}"/> registers its sorts. It
/// checks a <see cref="FilterGroup"/> against them (<see cref="Normalize"/>), compiles it into a predicate the database
/// can translate (<see cref="Compile"/>) and describes it in words (<see cref="Describe"/>). A filter names only these
/// fields and operators, so a query string can't reach a column the page didn't register.
/// </summary>
/// <example>
/// <code>
/// var builder = QueryBuilder&lt;Shipment&gt;.Create()
///     .Text("customer", "Customer", s =&gt; s.Customer)
///     .Choice("status", "Status", s =&gt; s.Status.ToString(), statuses)
///     .Number("amount", "Amount", s =&gt; s.Amount)
///     .Date("date", "Date", s =&gt; s.Date);
///
/// var filter = FilterJson.FromText(request.Where);            // from the page's query or form
/// var predicate = builder.Compile(filter);                     // null when the filter has no rules
/// var rows = Shipments.Query.Where(predicate);
/// </code>
/// </example>
public sealed class QueryBuilder<T>
{
    private readonly List<Entry> _fields = [];

    private QueryBuilder() { }

    /// <summary>A builder with no fields yet.</summary>
    public static QueryBuilder<T> Create() => new();

    /// <summary>The fields, in the order the field menu lists them.</summary>
    public IReadOnlyList<QueryField> Schema => _fields.Select(e => e.Field).ToArray();

    /// <summary>Text, matched by contains, equals, starts with, or empty.</summary>
    public QueryBuilder<T> Text(string key, string title, Expression<Func<T, string?>> value) => Add(key, title, QueryFieldKind.Text, value, null);

    /// <summary>A fixed list: the value is compared with the options' values (<see cref="GridOption.Value"/>).</summary>
    public QueryBuilder<T> Choice(string key, string title, Expression<Func<T, string?>> value, IReadOnlyList<GridOption> options) =>
        Add(key, title, QueryFieldKind.Choice, value, options);

    /// <summary>A number, compared or ranged.</summary>
    public QueryBuilder<T> Number(string key, string title, Expression<Func<T, decimal>> value) => Add(key, title, QueryFieldKind.Number, value, null);

    /// <summary>A whole number, compared or ranged.</summary>
    public QueryBuilder<T> Number(string key, string title, Expression<Func<T, int>> value) =>
        Number(key, title, Expression.Lambda<Func<T, decimal>>(Expression.Convert(value.Body, typeof(decimal)), value.Parameters));

    /// <summary>A date, compared or ranged (the values are <c>yyyy-MM-dd</c>).</summary>
    public QueryBuilder<T> Date(string key, string title, Expression<Func<T, DateOnly>> value) => Add(key, title, QueryFieldKind.Date, value, null);

    /// <summary>
    /// A copy without the fields <paramref name="allow"/> refuses (<see cref="State.FieldAccess.For"/>): a filter naming one
    /// of them drops that rule, like an unknown field.
    /// </summary>
    public QueryBuilder<T> Restrict(Func<string, bool> allow)
    {
        var copy = new QueryBuilder<T>();
        copy._fields.AddRange(_fields.Where(e => allow(e.Field.Key)));
        return copy;
    }

    /// <summary>
    /// The filter as the builder keeps it: unknown fields, operators the field's kind doesn't offer, values that don't
    /// parse or aren't options, and anything past <see cref="FilterLimits"/> are dropped. Empty groups stay (they match
    /// everything), so the form still shows them.
    /// </summary>
    public FilterGroup Normalize(FilterGroup group)
    {
        var budget = FilterLimits.MaxRules;
        return Clean(group, 0, ref budget);
    }

    /// <summary>
    /// The predicate of a filter, or null when it has no rules (show every row). The rules are combined as the groups
    /// say: AND within a group that joins by AND, OR within one that joins by OR. An empty group adds nothing.
    /// </summary>
    public Expression<Func<T, bool>>? Compile(FilterGroup? group)
    {
        if (group is null) return null;
        var clean = Normalize(group);
        var x = Expression.Parameter(typeof(T), "x");
        return Body(clean, x) is { } body ? Expression.Lambda<Func<T, bool>>(body, x) : null;
    }

    /// <summary>The filter in words: "Status is Shipped and (Amount is at least 100 or Customer contains Acme)". "All rows" when it has no rules.</summary>
    public string Describe(FilterGroup group)
    {
        var text = Words(Normalize(group), top: true);
        return text.Length == 0 ? "All rows" : text;
    }

    private QueryBuilder<T> Add(string key, string title, QueryFieldKind kind, LambdaExpression member, IReadOnlyList<GridOption>? options)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 60) throw new ArgumentException("A field needs a key of up to 60 characters.", nameof(key));
        if (_fields.Any(e => e.Field.Key == key)) throw new ArgumentException($"The field '{key}' is already registered.", nameof(key));
        _fields.Add(new Entry(new QueryField(key, title, kind, options ?? []), member));
        return this;
    }

    private Entry? Find(string key) => _fields.Find(e => e.Field.Key == key);

    // Normalizing ----------------------------------------------------------------------------------------------

    private FilterGroup Clean(FilterGroup group, int depth, ref int budget)
    {
        var items = new List<FilterNode>();
        foreach (var item in group.Items)
        {
            if (item is FilterGroup sub)
            {
                if (depth + 1 < FilterLimits.MaxDepth) items.Add(Clean(sub, depth + 1, ref budget));
            }
            else if (item is FilterRule rule && budget > 0 && Valid(rule) is { } clean)
            {
                items.Add(clean);
                budget--;
            }
        }
        return new FilterGroup(group.Join, items);
    }

    // The rule with its values checked (and choice values reduced to the options), or null when it can't mean anything.
    private FilterRule? Valid(FilterRule rule)
    {
        if (Find(rule.Field) is not { } entry) return null;
        var field = entry.Field;
        if (!QueryFieldOperators.For(field.Kind).Contains(rule.Operator)) return null;
        if (!QueryFieldOperators.NeedsValue(rule.Operator)) return new FilterRule(rule.Field, rule.Operator);

        var value = Text(rule.Value);
        var to = QueryFieldOperators.NeedsTo(rule.Operator) ? Text(rule.To) : null;
        if (value is null || (QueryFieldOperators.NeedsTo(rule.Operator) && to is null)) return null;

        switch (field.Kind)
        {
            case QueryFieldKind.Number:
                if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) return null;
                if (to is not null && !decimal.TryParse(to, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) return null;
                break;
            case QueryFieldKind.Date:
                if (!DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return null;
                if (to is not null && !DateOnly.TryParseExact(to, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) return null;
                break;
            case QueryFieldKind.Choice:
                var picked = Options(value).Where(v => field.Options.Any(o => o.Value == v)).ToArray();
                if (picked.Length == 0) return null;
                value = string.Join(ColumnSearch.ValueSeparator, picked);
                break;
        }
        return new FilterRule(rule.Field, rule.Operator, value, to);
    }

    private static string? Text(string? value)
    {
        var text = value?.Trim();
        return string.IsNullOrEmpty(text) || text.Length > FilterLimits.MaxValueLength ? null : text;
    }

    private static IEnumerable<string> Options(string value) => value.Split(ColumnSearch.ValueSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Compiling ------------------------------------------------------------------------------------------------

    private Expression? Body(FilterGroup group, ParameterExpression x)
    {
        Expression? acc = null;
        foreach (var item in group.Items)
        {
            var part = item is FilterGroup sub ? Body(sub, x) : Rule((FilterRule)item, x);
            if (part is null) continue;
            acc = acc is null ? part : group.Join == FilterJoin.And ? Expression.AndAlso(acc, part) : Expression.OrElse(acc, part);
        }
        return acc;
    }

    private Expression Rule(FilterRule rule, ParameterExpression x)
    {
        var entry = Find(rule.Field)!;
        var member = Rebind(entry.Member, x);
        switch (entry.Field.Kind)
        {
            case QueryFieldKind.Number:
                return Compare(member, rule, decimal.Parse(rule.Value!, CultureInfo.InvariantCulture), rule.To is null ? default : decimal.Parse(rule.To, CultureInfo.InvariantCulture));
            case QueryFieldKind.Date:
                return Compare(member, rule, DateOnly.ParseExact(rule.Value!, "yyyy-MM-dd", CultureInfo.InvariantCulture), rule.To is null ? default : DateOnly.ParseExact(rule.To, "yyyy-MM-dd", CultureInfo.InvariantCulture));
            default:
                return TextTest(member, rule);
        }
    }

    // Comparisons over a value type. The value (and the range's end) travel as constants, which SQL providers parameterise.
    private static Expression Compare<TValue>(Expression member, FilterRule rule, TValue value, TValue to) where TValue : struct, IComparable<TValue>
    {
        var constant = Expression.Constant(value, typeof(TValue));
        return rule.Operator switch
        {
            FilterOperator.Equals => Expression.Equal(member, constant),
            FilterOperator.NotEquals => Expression.NotEqual(member, constant),
            FilterOperator.GreaterThan => Expression.GreaterThan(member, constant),
            FilterOperator.GreaterOrEqual => Expression.GreaterThanOrEqual(member, constant),
            FilterOperator.LessThan => Expression.LessThan(member, constant),
            FilterOperator.LessOrEqual => Expression.LessThanOrEqual(member, constant),
            _ => Expression.AndAlso(
                Expression.GreaterThanOrEqual(member, constant),
                Expression.LessThanOrEqual(member, Expression.Constant(to, typeof(TValue)))),
        };
    }

    // Text and choices: the stored text, null-safe where a null row must not match by accident.
    private static Expression TextTest(Expression member, FilterRule rule)
    {
        var none = Expression.Constant(null, typeof(string));
        var isNull = Expression.Equal(member, none);
        var notNull = Expression.NotEqual(member, none);
        var value = Expression.Constant(rule.Value, typeof(string));
        return rule.Operator switch
        {
            FilterOperator.IsEmpty => Expression.Call(IsNullOrEmpty, member),
            FilterOperator.IsNotEmpty => Expression.Not(Expression.Call(IsNullOrEmpty, member)),
            FilterOperator.Equals => Expression.Equal(member, value),
            FilterOperator.NotEquals => Expression.OrElse(isNull, Expression.NotEqual(member, value)),
            FilterOperator.Contains => Expression.AndAlso(notNull, Expression.Call(member, Contains, value)),
            FilterOperator.StartsWith => Expression.AndAlso(notNull, Expression.Call(member, StartsWith, value)),
            FilterOperator.In => Expression.Call(EnumerableContains, Expression.Constant(Options(rule.Value!).ToArray()), member),
            _ => Expression.OrElse(isNull, Expression.Not(Expression.Call(EnumerableContains, Expression.Constant(Options(rule.Value!).ToArray()), member))),
        };
    }

    private static readonly MethodInfo IsNullOrEmpty = typeof(string).GetMethod(nameof(string.IsNullOrEmpty), [typeof(string)])!;
    private static readonly MethodInfo Contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;
    private static readonly MethodInfo StartsWith = typeof(string).GetMethod(nameof(string.StartsWith), [typeof(string)])!;
    private static readonly MethodInfo EnumerableContains = typeof(Enumerable).GetMethods()
        .Single(m => m.Name == nameof(Enumerable.Contains) && m.GetParameters().Length == 2).MakeGenericMethod(typeof(string));

    private static Expression Rebind(LambdaExpression member, ParameterExpression x) => new ParameterRebinder(member.Parameters[0], x).Visit(member.Body)!;

    private sealed class ParameterRebinder(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) => node == from ? to : base.VisitParameter(node);
    }

    // Describing -----------------------------------------------------------------------------------------------

    private string Words(FilterGroup group, bool top)
    {
        var parts = new List<string>();
        foreach (var item in group.Items)
        {
            var text = item is FilterGroup sub ? Words(sub, top: false) : RuleWords((FilterRule)item);
            if (text.Length > 0) parts.Add(text);
        }
        if (parts.Count == 0) return "";
        var joined = string.Join(group.Join == FilterJoin.And ? " and " : " or ", parts);
        return parts.Count > 1 && !top ? $"({joined})" : joined;
    }

    private string RuleWords(FilterRule rule)
    {
        var title = Find(rule.Field)?.Field.Title ?? rule.Field;
        var label = QueryFieldOperators.Label(rule.Operator);
        if (!QueryFieldOperators.NeedsValue(rule.Operator)) return $"{title} {label}";
        var value = Find(rule.Field)?.Field.Kind == QueryFieldKind.Choice
            ? string.Join(", ", Options(rule.Value!).Select(v => Find(rule.Field)!.Field.Options.FirstOrDefault(o => o.Value == v)?.Label ?? v))
            : rule.Value;
        return QueryFieldOperators.NeedsTo(rule.Operator) ? $"{title} {label} {value} and {rule.To}" : $"{title} {label} {value}";
    }

    private sealed record Entry(QueryField Field, LambdaExpression Member);
}
