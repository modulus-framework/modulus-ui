using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Data;
using Modulus.Ui.Abstractions.Query;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The visual filter: its model and JSON, the builder's checks and predicates, the form's operations and the table.</summary>
public class QueryBuilderTests
{
    private sealed record Row(int Id, string? Customer, string City, string Status, decimal Amount, int Items, DateOnly Date);

    private static readonly Row[] Rows =
    [
        new(1, "Acme", "Oslo", "Shipped", 120m, 2, new DateOnly(2026, 1, 5)),
        new(2, "Beta", "Oslo", "Pending", 40m, 5, new DateOnly(2026, 2, 10)),
        new(3, "Acme", "Paris", "Delivered", 300m, 1, new DateOnly(2026, 3, 1)),
        new(4, "Gamma", "Rome", "Shipped", 80m, 4, new DateOnly(2026, 6, 30)),
        new(5, null, "Oslo", "Pending", 10m, 9, new DateOnly(2025, 12, 31)),
    ];

    private static readonly GridOption[] Statuses = [new("Pending", "Pending"), new("Shipped", "Shipped"), new("Delivered", "Delivered")];

    private static QueryBuilder<Row> Builder() => QueryBuilder<Row>.Create()
        .Text("customer", "Customer", r => r.Customer)
        .Text("city", "City", r => r.City)
        .Choice("status", "Status", r => r.Status, Statuses)
        .Number("amount", "Amount", r => r.Amount)
        .Number("items", "Items", r => r.Items)
        .Date("date", "Date", r => r.Date);

    private static FilterRule Rule(string field, FilterOperator op, string? value = null, string? to = null) => new(field, op, value, to);

    private static FilterGroup All(params FilterNode[] items) => new(FilterJoin.And, items);

    private static int[] Ids(QueryBuilder<Row> builder, FilterGroup group) =>
        (builder.Compile(group) is { } where ? Rows.Where(where.Compile()) : Rows).Select(r => r.Id).ToArray();

    // Predicates -----------------------------------------------------------------------------------------------

    [Fact]
    public void Text_Operators_MatchTheRightRows()
    {
        var b = Builder();
        Assert.Equal([1, 3], Ids(b, All(Rule("customer", FilterOperator.Equals, "Acme"))));
        Assert.Equal([2, 4, 5], Ids(b, All(Rule("customer", FilterOperator.NotEquals, "Acme")))); // a missing customer is not Acme
        Assert.Equal([1, 3], Ids(b, All(Rule("customer", FilterOperator.Contains, "cm"))));
        Assert.Equal([1, 3], Ids(b, All(Rule("customer", FilterOperator.StartsWith, "A"))));
        Assert.Equal([5], Ids(b, All(Rule("customer", FilterOperator.IsEmpty))));
        Assert.Equal([1, 2, 3, 4], Ids(b, All(Rule("customer", FilterOperator.IsNotEmpty))));
    }

    [Fact]
    public void Choice_Operators_UseTheOptionValues()
    {
        var b = Builder();
        Assert.Equal([1, 4], Ids(b, All(Rule("status", FilterOperator.In, "Shipped"))));
        Assert.Equal([1, 3, 4], Ids(b, All(Rule("status", FilterOperator.In, "Shipped|Delivered"))));
        Assert.Equal([2, 3, 5], Ids(b, All(Rule("status", FilterOperator.NotIn, "Shipped"))));
        Assert.Equal([2, 5], Ids(b, All(Rule("status", FilterOperator.Equals, "Pending"))));
    }

    [Fact]
    public void Numbers_AndDates_CompareAndRange()
    {
        var b = Builder();
        Assert.Equal([1, 3], Ids(b, All(Rule("amount", FilterOperator.GreaterOrEqual, "120"))));
        Assert.Equal([1, 4], Ids(b, All(Rule("amount", FilterOperator.Between, "50", "150"))));
        Assert.Equal([2, 5], Ids(b, All(Rule("amount", FilterOperator.LessThan, "50"))));
        Assert.Equal([2, 5], Ids(b, All(Rule("items", FilterOperator.GreaterThan, "4")))); // a whole-number field
        Assert.Equal([1, 2, 3], Ids(b, All(Rule("date", FilterOperator.Between, "2026-01-01", "2026-03-31"))));
        Assert.Equal([4], Ids(b, All(Rule("date", FilterOperator.Equals, "2026-06-30"))));
    }

    [Fact]
    public void Groups_CombineByTheirJoin_AndNest()
    {
        var b = Builder();
        var or = new FilterGroup(FilterJoin.Or, [Rule("amount", FilterOperator.GreaterOrEqual, "300"), Rule("customer", FilterOperator.Equals, "Gamma")]);
        Assert.Equal([3, 4], Ids(b, new FilterGroup(FilterJoin.Or, or.Items)));

        // Oslo, and either under 50 or Delivered: rows 2 and 5 (row 3 is Paris).
        var nested = All(Rule("city", FilterOperator.Equals, "Oslo"), new FilterGroup(FilterJoin.Or, [Rule("amount", FilterOperator.LessThan, "50"), Rule("status", FilterOperator.Equals, "Delivered")]));
        Assert.Equal([2, 5], Ids(b, nested));
    }

    [Fact]
    public void NoRules_MeansNoPredicate_AndEveryRow()
    {
        var b = Builder();
        Assert.Null(b.Compile(null));
        Assert.Null(b.Compile(FilterGroup.Empty));
        Assert.Null(b.Compile(All(new FilterGroup(FilterJoin.Or, [])))); // an empty group adds nothing
        Assert.Equal(Rows.Length, Ids(b, FilterGroup.Empty).Length);
    }

    // Checks ---------------------------------------------------------------------------------------------------

    [Fact]
    public void Normalize_DropsWhatTheBuilderCannotAnswer()
    {
        var b = Builder();
        var clean = b.Normalize(All(
            Rule("nope", FilterOperator.Equals, "x"),                    // unknown field
            Rule("amount", FilterOperator.Contains, "1"),                // an operator a number doesn't offer
            Rule("amount", FilterOperator.GreaterThan, "abc"),           // not a number
            Rule("date", FilterOperator.Equals, "2026-13-45"),           // not a date
            Rule("status", FilterOperator.In, "Bogus"),                  // no option matches
            Rule("status", FilterOperator.In, "Pending|Bogus"),          // the unknown one is dropped, the rest kept
            Rule("customer", FilterOperator.Contains, new string('x', FilterLimits.MaxValueLength + 1)), // too long
            Rule("customer", FilterOperator.Contains, "   "),             // blank
            Rule("customer", FilterOperator.IsEmpty, "ignored")));       // no value needed: the value goes

        var kept = Assert.IsType<FilterRule[]>(clean.Items.Cast<FilterRule>().ToArray());
        Assert.Equal([("status", "Pending"), ("customer", (string?)null)], kept.Select(r => (r.Field, r.Value)));
        Assert.Equal(FilterOperator.IsEmpty, kept[1].Operator);
    }

    [Fact]
    public void Normalize_KeepsTwoLevelsOfGroups_AndCapsTheRules()
    {
        var b = Builder();
        var deep = All(new FilterGroup(FilterJoin.Or, [new FilterGroup(FilterJoin.And, [new FilterGroup(FilterJoin.Or, [Rule("city", FilterOperator.Equals, "Oslo")])])]));
        var clean = b.Normalize(deep);
        var level1 = Assert.IsType<FilterGroup>(Assert.Single(clean.Items));
        var level2 = Assert.IsType<FilterGroup>(Assert.Single(level1.Items));
        Assert.Empty(level2.Items); // the third level is past FilterLimits.MaxDepth

        var many = All(Enumerable.Range(0, FilterLimits.MaxRules + 5).Select(_ => (FilterNode)Rule("city", FilterOperator.Equals, "Oslo")).ToArray());
        Assert.Equal(FilterLimits.MaxRules, b.Normalize(many).Items.Count);
    }

    [Fact]
    public void Describe_ReadsLikeASentence()
    {
        var b = Builder();
        var group = All(
            Rule("customer", FilterOperator.Contains, "Acme"),
            new FilterGroup(FilterJoin.Or, [Rule("amount", FilterOperator.GreaterOrEqual, "100"), Rule("status", FilterOperator.In, "Delivered")]),
            Rule("date", FilterOperator.Between, "2026-01-01", "2026-03-31"));

        Assert.Equal("Customer contains Acme and (Amount is at least 100 or Status is one of Delivered) and Date is between 2026-01-01 and 2026-03-31", b.Describe(group));
        Assert.Equal("Customer is empty", b.Describe(All(Rule("customer", FilterOperator.IsEmpty))));
        Assert.Equal("All rows", b.Describe(FilterGroup.Empty));
    }

    [Fact]
    public void Builder_RefusesDuplicateAndLongKeys()
    {
        var b = Builder();
        Assert.Throws<ArgumentException>(() => b.Text("customer", "Again", r => r.Customer));
        Assert.Throws<ArgumentException>(() => b.Text(new string('k', 61), "Long", r => r.Customer));
    }

    [Fact]
    public void Restrict_DropsTheRulesOfHiddenFields()
    {
        var restricted = Builder().Restrict(k => k != "amount");
        Assert.DoesNotContain(restricted.Schema, f => f.Key == "amount");
        Assert.Null(restricted.Compile(All(Rule("amount", FilterOperator.GreaterThan, "1"))));
    }

    // JSON -----------------------------------------------------------------------------------------------------

    [Fact]
    public void FilterJson_RoundTrips_AndRejectsWhatIsNotAFilter()
    {
        var group = new FilterGroup(FilterJoin.Or, [Rule("amount", FilterOperator.Between, "1", "9"), new FilterGroup(FilterJoin.And, [])]);
        var text = FilterJson.ToText(group);
        Assert.Contains("\"kind\":\"rule\"", text);
        Assert.Contains("\"between\"", text);
        Assert.Equal(text, FilterJson.ToText(FilterJson.FromText(text)!)); // the records hold lists, so compare the text

        Assert.Null(FilterJson.FromText(null));
        Assert.Null(FilterJson.FromText("not json"));
        Assert.Null(FilterJson.FromText("{\"kind\":\"rule\",\"field\":\"x\",\"operator\":\"equals\"}")); // a rule is not a whole filter
        Assert.Null(FilterJson.FromText(new string('x', FilterLimits.MaxTextLength + 1)));
    }

    // Form -----------------------------------------------------------------------------------------------------

    [Fact]
    public void Form_ReadsTheNamesTheRendererWrites()
    {
        var query = new List<KeyValuePair<string, string?>>
        {
            new("f.join", "Or"),
            new("f.0.kind", "rule"), new("f.0.field", "amount"), new("f.0.op", "GreaterOrEqual"), new("f.0.value", "100"),
            new("f.1.kind", "group"), new("f.1.join", "And"),
            new("f.1.0.kind", "rule"), new("f.1.0.field", "status"), new("f.1.0.op", "In"), new("f.1.0.value", "Pending"), new("f.1.0.value", "Delivered"),
            new("f.1.1.kind", "rule"), new("f.1.1.field", "customer"), new("f.1.1.op", "Contains"), new("f.1.1.value", "Acme"),
            new("other", "ignored"), new("f.9.op", "NotAnOperator"), new("f.0.bogus", "ignored"),
        };

        var group = QueryBuilderForm.Read(query);

        Assert.Equal(FilterJoin.Or, group.Join);
        var first = Assert.IsType<FilterRule>(group.Items[0]);
        Assert.Equal(("amount", FilterOperator.GreaterOrEqual, "100"), (first.Field, first.Operator, first.Value));
        var nested = Assert.IsType<FilterGroup>(group.Items[1]);
        Assert.Equal(FilterJoin.And, nested.Join);
        Assert.Equal("Pending|Delivered", Assert.IsType<FilterRule>(nested.Items[0]).Value); // a multi-select's values
        Assert.Equal(2, group.Items.Count); // the node with an unknown operator is left out
    }

    [Fact]
    public void Form_EditsAddRemoveAndClear_WithinTheLimits()
    {
        var schema = Builder().Schema;
        var model = FilterGroup.Empty;

        model = QueryBuilderForm.Edit(schema, model, "add-rule:");
        var added = Assert.IsType<FilterRule>(Assert.Single(model.Items));
        Assert.Equal(("customer", FilterOperator.Contains), (added.Field, added.Operator));

        model = QueryBuilderForm.Edit(schema, model, "add-group:");
        model = QueryBuilderForm.Edit(schema, model, "add-rule:1");
        Assert.Single(Assert.IsType<FilterGroup>(model.Items[1]).Items);

        // A group inside a group inside the top group is the deepest level; a group under it is refused.
        model = QueryBuilderForm.Edit(schema, model, "add-group:1");
        model = QueryBuilderForm.Edit(schema, model, "add-group:1.1");
        Assert.Empty(Assert.IsType<FilterGroup>(Assert.IsType<FilterGroup>(model.Items[1]).Items[1]).Items);
        Assert.Equal(model, QueryBuilderForm.Edit(schema, model, "add-group:1.1.0"));

        model = QueryBuilderForm.Edit(schema, model, "remove:1");
        Assert.Single(model.Items);
        Assert.Equal(model, QueryBuilderForm.Edit(schema, model, "remove:"));          // the top group can't be removed
        Assert.Equal(model, QueryBuilderForm.Edit(schema, model, "add-rule:x"));       // not a path
        Assert.Equal(model, QueryBuilderForm.Edit(schema, model, "add-rule:7"));       // not a group

        Assert.Equal(FilterGroup.Empty, QueryBuilderForm.Edit(schema, model, "clear"));
    }

    [Fact]
    public void Form_StopsAddingRulesAtTheLimit()
    {
        var schema = Builder().Schema;
        var model = FilterGroup.Empty;
        for (var i = 0; i < FilterLimits.MaxRules + 3; i++) model = QueryBuilderForm.Edit(schema, model, "add-rule:");
        Assert.Equal(FilterLimits.MaxRules, model.Items.Count);
    }

    [Fact]
    public void Form_ReplacesANodeAtItsPath()
    {
        var model = All(Rule("city", FilterOperator.Equals, "Oslo"));
        var edited = QueryBuilderForm.Replace(model, "0", Rule("city", FilterOperator.Equals, "Rome"));
        Assert.Equal("Rome", Assert.IsType<FilterRule>(edited.Items[0]).Value);
        Assert.Equal(model, QueryBuilderForm.Replace(model, "5", Rule("city", FilterOperator.Equals, "x")));
    }

    // Rendering ------------------------------------------------------------------------------------------------

    [Fact]
    public void Render_NamesTheFields_AndEscapesValues()
    {
        var model = All(Rule("customer", FilterOperator.Contains, "<b>x</b>"), new FilterGroup(FilterJoin.Or, []));
        var html = QueryBuilderHtml.Render(Builder().Schema, model, action: "/tables/query");

        Assert.Contains("action=\"/tables/query\"", html);
        Assert.Contains("name=\"f.0.field\"", html);
        Assert.Contains("name=\"f.0.value\"", html);
        Assert.Contains("name=\"f.op\" value=\"add-rule:\"", html);
        Assert.Contains("name=\"f.op\" value=\"remove:0\"", html);
        Assert.Contains("name=\"f.1.join\"", html);
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", html);
        Assert.DoesNotContain("<b>x</b>", html);
    }

    // The table ------------------------------------------------------------------------------------------------

    [Fact]
    public void TableQuery_FiltersTheRowsByTheRequestsWhere()
    {
        var query = TableQuery<Row>.By(r => r.Id).Filters(Builder());
        var where = FilterJson.ToText(All(Rule("city", FilterOperator.Equals, "Oslo"), Rule("amount", FilterOperator.GreaterThan, "30")));

        var rows = query.Filter(Rows.AsQueryable(), new ProTableRequest { Where = where }).Select(r => r.Id).ToArray();
        Assert.Equal([1, 2], rows);

        // Without a filter (or with a text that isn't one) every row comes back.
        Assert.Equal(Rows.Length, query.Filter(Rows.AsQueryable(), new ProTableRequest { Where = "junk" }).Count());
        Assert.Equal(Rows.Length, TableQuery<Row>.By(r => r.Id).Filter(Rows.AsQueryable(), new ProTableRequest { Where = where }).Count());

        // A hidden field's rule is dropped, the rest still applies.
        var restricted = query.Restrict(k => k != "amount");
        Assert.Equal([1, 2, 5], restricted.Filter(Rows.AsQueryable(), new ProTableRequest { Where = where }).Select(r => r.Id).ToArray());
    }
}
