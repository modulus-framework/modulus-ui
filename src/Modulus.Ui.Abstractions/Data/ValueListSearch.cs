using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using Modulus.Ui.Abstractions.Query;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Modulus.Ui.Abstractions.Pivot;

namespace Modulus.Ui.Abstractions.Data;

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
