using Modulus.Ui.Abstractions.Query;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The builder's markup: the join shown between rules, the empty state, and the choice dropdown's checkboxes.</summary>
public class QueryBuilderMarkupTests
{
    private static readonly IReadOnlyList<QueryField> Schema =
    [
        new("customer", "Customer", QueryFieldKind.Text, []),
        new("status", "Status", QueryFieldKind.Choice, [new("Pending", "Pending"), new("Shipped", "Shipped"), new("Delivered", "Delivered")]),
    ];

    [Fact]
    public void ARuleAfterTheFirst_ShowsTheJoinInItsConnectorColumn()
    {
        var filter = new FilterGroup(FilterJoin.Or, [new FilterRule("customer", FilterOperator.Contains, "Ada"), new FilterRule("customer", FilterOperator.Contains, "Lee")]);

        var html = QueryBuilderHtml.Render(Schema, filter);

        Assert.Contains("<span class=\"m-qb-connector\" aria-hidden=\"true\"></span>", html);
        Assert.Contains("<span class=\"m-qb-connector\" aria-hidden=\"true\">or</span>", html);
    }

    [Fact]
    public void AnEmptyTopGroup_SaysToAddARule()
    {
        var html = QueryBuilderHtml.Render(Schema, FilterGroup.Empty);

        Assert.Contains("No conditions yet. Add a rule to narrow the list.", html);
        Assert.DoesNotContain("m-qb-rule", html);
    }

    [Fact]
    public void AChoiceRule_IsADropdownOfCheckboxes_EachPostingItsValue()
    {
        var filter = new FilterGroup(FilterJoin.And, [new FilterRule("status", FilterOperator.In, "Pending|Shipped")]);

        var html = QueryBuilderHtml.Render(Schema, filter);

        Assert.Contains("<details class=\"m-dropdown m-qb-multi\">", html);
        Assert.Contains("Pending, Shipped", html);
        Assert.Contains("name=\"f.0.value\" value=\"Pending\" checked", html);
        Assert.Contains("name=\"f.0.value\" value=\"Shipped\" checked", html);
        Assert.Contains("name=\"f.0.value\" value=\"Delivered\" />", html);
        Assert.DoesNotContain("multiple", html);
    }

    [Fact]
    public void AChoiceRuleWithNothingPicked_SaysToChooseValues()
    {
        var filter = new FilterGroup(FilterJoin.And, [new FilterRule("status", FilterOperator.In, null)]);

        var html = QueryBuilderHtml.Render(Schema, filter);

        Assert.Contains("Choose values", html);
        Assert.DoesNotContain("m-qb-multi-count", html);
    }
}
