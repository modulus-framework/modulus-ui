using Modulus.Ui.Abstractions;

namespace Modulus.Ui.Abstractions.Query;

/// <summary>What a field holds, which decides its operators and how its value is read.</summary>
public enum QueryFieldKind
{
    /// <summary>Text: contains, equals, starts with, empty.</summary>
    Text,

    /// <summary>A number: comparisons and ranges.</summary>
    Number,

    /// <summary>A date (<c>yyyy-MM-dd</c>): comparisons and ranges.</summary>
    Date,

    /// <summary>One of a fixed list of options: is one of, is none of.</summary>
    Choice,
}

/// <summary>A field the builder offers: its key (in the filter), title and kind, and the options of a choice.</summary>
public sealed record QueryField(string Key, string Title, QueryFieldKind Kind, IReadOnlyList<GridOption> Options);

/// <summary>The operators of each kind, their labels, and which take a value or a second value.</summary>
public static class QueryFieldOperators
{
    private static readonly FilterOperator[] TextOps = [FilterOperator.Contains, FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.StartsWith, FilterOperator.IsEmpty, FilterOperator.IsNotEmpty];
    private static readonly FilterOperator[] RangeOps = [FilterOperator.Equals, FilterOperator.NotEquals, FilterOperator.GreaterThan, FilterOperator.GreaterOrEqual, FilterOperator.LessThan, FilterOperator.LessOrEqual, FilterOperator.Between];
    private static readonly FilterOperator[] ChoiceOps = [FilterOperator.In, FilterOperator.NotIn, FilterOperator.Equals, FilterOperator.NotEquals];

    /// <summary>The operators a kind offers, in the order the menu lists them.</summary>
    public static IReadOnlyList<FilterOperator> For(QueryFieldKind kind) => kind switch
    {
        QueryFieldKind.Number or QueryFieldKind.Date => RangeOps,
        QueryFieldKind.Choice => ChoiceOps,
        _ => TextOps,
    };

    /// <summary>Whether an operator compares with a value (empty and not-empty do not).</summary>
    public static bool NeedsValue(FilterOperator op) => op is not (FilterOperator.IsEmpty or FilterOperator.IsNotEmpty);

    /// <summary>Whether an operator compares with a second value (the end of a range).</summary>
    public static bool NeedsTo(FilterOperator op) => op == FilterOperator.Between;

    /// <summary>The words of an operator as a person reads them ("is at least", "contains").</summary>
    public static string Label(FilterOperator op) => op switch
    {
        FilterOperator.Equals => "is",
        FilterOperator.NotEquals => "is not",
        FilterOperator.Contains => "contains",
        FilterOperator.StartsWith => "starts with",
        FilterOperator.IsEmpty => "is empty",
        FilterOperator.IsNotEmpty => "is not empty",
        FilterOperator.GreaterThan => "is greater than",
        FilterOperator.GreaterOrEqual => "is at least",
        FilterOperator.LessThan => "is less than",
        FilterOperator.LessOrEqual => "is at most",
        FilterOperator.Between => "is between",
        FilterOperator.In => "is one of",
        FilterOperator.NotIn => "is none of",
        _ => op.ToString(),
    };
}
