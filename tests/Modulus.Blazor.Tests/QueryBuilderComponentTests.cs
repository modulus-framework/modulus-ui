using System.Text.RegularExpressions;
using Bunit;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Query;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The Blazor QueryBuilder: the markup matches the MVC renderer's, and its controls report the filter they make.</summary>
public class QueryBuilderComponentTests : BunitContext
{
    private sealed record Row(string Customer, string Status, decimal Amount);

    private static readonly GridOption[] Statuses = [new("Pending", "Pending"), new("Delivered", "Delivered")];

    private static readonly IReadOnlyList<QueryField> Schema = QueryBuilder<Row>.Create()
        .Text("customer", "Customer", r => r.Customer)
        .Choice("status", "Status", r => r.Status, Statuses)
        .Number("amount", "Amount", r => r.Amount)
        .Schema;

    private static readonly FilterGroup Model = new(FilterJoin.Or,
    [
        new FilterRule("amount", FilterOperator.Between, "10", "50"),
        new FilterGroup(FilterJoin.And, [new FilterRule("status", FilterOperator.In, "Pending|Delivered")]),
    ]);

    // Counts elements whose class list holds the class as a whole token (m-qb is not counted for m-qb-group).
    private static int Count(string markup, string cls) => Regex.Matches(markup, $"(?<=class=\"[^\"]*)(?<![\\w-]){Regex.Escape(cls)}(?![\\w-])").Count;

    [Fact]
    public void Markup_HasTheClassesOfTheMvcRenderer()
    {
        var cut = Render<QueryBuilder>(p => p.Add(x => x.Schema, Schema).Add(x => x.Value, Model));
        var html = QueryBuilderHtml.Render(Schema, Model);

        foreach (var cls in new[] { "m-qb", "m-qb-group", "m-qb-head", "m-qb-items", "m-qb-actions", "m-qb-footer", "m-qb-rule", "m-qb-value", "m-qb-to", "m-qb-remove", "m-qb-label" })
            Assert.True(Count(html, cls) == Count(cut.Markup, cls), $"{cls}: mvc {Count(html, cls)} blazor {Count(cut.Markup, cls)}");
    }

    [Fact]
    public void Buttons_AddAndRemove_ReportTheFilterTheyMake()
    {
        FilterGroup? changed = null;
        var cut = Render<QueryBuilder>(p => p.Add(x => x.Schema, Schema).Add(x => x.Value, FilterGroup.Empty)
            .Add(x => x.ValueChanged, v => changed = v));

        cut.Find(".m-qb-actions button").Click(); // + Rule on the top group
        var rule = Assert.IsType<FilterRule>(Assert.Single(changed!.Items));
        Assert.Equal(("customer", FilterOperator.Contains), (rule.Field, rule.Operator));
        Assert.Single(cut.FindAll(".m-qb-rule"));

        cut.Find(".m-qb-rule .m-qb-remove").Click();
        Assert.Empty(changed!.Items);
    }

    [Fact]
    public void Controls_ChangeTheRule_ThroughTheSameEditsAsTheForm()
    {
        FilterGroup? changed = null;
        var cut = Render<QueryBuilder>(p => p.Add(x => x.Schema, Schema).Add(x => x.Value, Model)
            .Add(x => x.ValueChanged, v => changed = v));

        // The first rule is amount between 10 and 50: change its field to status, which starts on its first operator.
        cut.Find(".m-qb-rule select[aria-label='Field']").Change("status");
        var moved = Assert.IsType<FilterRule>(Assert.IsType<FilterGroup>(changed).Items[0]);
        Assert.Equal(("status", FilterOperator.In), (moved.Field, moved.Operator));
        Assert.Null(moved.To);

        cut.Find(".m-qb-rule select[aria-label='Field']").Change("amount");
        cut.Find(".m-qb-rule select[aria-label='Operator']").Change("GreaterThan");
        var compared = Assert.IsType<FilterRule>(Assert.IsType<FilterGroup>(changed).Items[0]);
        Assert.Equal((FilterOperator.GreaterThan, (string?)null, (string?)null), (compared.Operator, compared.Value, compared.To)); // a new field starts with no value

        cut.Find(".m-qb-rule input[aria-label='Value']").Change("25");
        Assert.Equal("25", Assert.IsType<FilterRule>(Assert.IsType<FilterGroup>(changed).Items[0]).Value);
    }

    [Fact]
    public void Join_AndNestedGroups_AreEditable()
    {
        FilterGroup? changed = null;
        var cut = Render<QueryBuilder>(p => p.Add(x => x.Schema, Schema).Add(x => x.Value, Model)
            .Add(x => x.ValueChanged, v => changed = v));

        cut.Find(".m-qb-head select[aria-label='Match all or any of these']").Change("And");
        Assert.Equal(FilterJoin.And, changed!.Join);

        // The nested group's own join (the second select of the markup) takes its change.
        cut.FindAll(".m-qb-head select[aria-label='Match all or any of these']")[1].Change("Or");
        Assert.Equal(FilterJoin.Or, Assert.IsType<FilterGroup>(changed!.Items[1]).Join);
    }
}
