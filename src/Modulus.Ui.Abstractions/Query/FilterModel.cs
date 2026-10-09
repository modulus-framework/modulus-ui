using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions.Query;

/// <summary>How a group's items combine: all of them (AND) or any of them (OR).</summary>
public enum FilterJoin
{
    And,
    Or,
}

/// <summary>What a rule asks of its field. The operators a field offers depend on its kind (<see cref="QueryFieldOperators"/>).</summary>
public enum FilterOperator
{
    Equals,
    NotEquals,
    Contains,
    StartsWith,
    IsEmpty,
    IsNotEmpty,
    GreaterThan,
    GreaterOrEqual,
    LessThan,
    LessOrEqual,
    Between,
    In,
    NotIn,
}

/// <summary>The limits every filter is kept within, whatever a query string or a post sends.</summary>
public static class FilterLimits
{
    /// <summary>Rules a filter keeps.</summary>
    public const int MaxRules = 30;

    /// <summary>Levels of groups, the top group included.</summary>
    public const int MaxDepth = 3;

    /// <summary>Longest value kept.</summary>
    public const int MaxValueLength = 200;

    /// <summary>Longest filter text read (<see cref="FilterJson.FromText"/>).</summary>
    public const int MaxTextLength = 8000;
}

/// <summary>An item of a filter: a <see cref="FilterRule"/> or a nested <see cref="FilterGroup"/>.</summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(FilterRule), "rule")]
[JsonDerivedType(typeof(FilterGroup), "group")]
public abstract record FilterNode;

/// <summary>
/// One condition: <paramref name="Field"/> <paramref name="Operator"/> <paramref name="Value"/> (and <paramref name="To"/>
/// for a range). Values are text: numbers and dates are written invariant (<c>1200.5</c>, <c>2026-06-30</c>), and a
/// list joins its values with <c>|</c>.
/// </summary>
public sealed record FilterRule(string Field, FilterOperator Operator, string? Value = null, string? To = null) : FilterNode;

/// <summary>A group of items joined by AND or OR. A group with no items matches everything.</summary>
public sealed record FilterGroup(FilterJoin Join, IReadOnlyList<FilterNode> Items) : FilterNode
{
    /// <summary>A group that matches everything: the filter of a page with no conditions.</summary>
    public static FilterGroup Empty => new(FilterJoin.And, []);
}

/// <summary>A filter as text (JSON), for a query string, a hidden field or a saved view.</summary>
public static class FilterJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string ToText(FilterGroup group) => JsonSerializer.Serialize<FilterNode>(group, Options);

    /// <summary>The filter a text holds, or null when it is missing, too long or not a filter. Not checked against any fields (<see cref="QueryBuilder{T}"/> does that).</summary>
    public static FilterGroup? FromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length > FilterLimits.MaxTextLength) return null;
        try
        {
            return JsonSerializer.Deserialize<FilterNode>(text, Options) as FilterGroup;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
