using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Query;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>app-query-builder: the builder's GET form, with the filter it was given.</summary>
public class QueryBuilderTagHelperTests
{
    private sealed record Row(string City, decimal Amount);

    private static async Task<string> RenderAsync(QueryBuilderTagHelper helper)
    {
        var output = new TagHelperOutput("app-query-builder", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext("app-query-builder", [], new Dictionary<object, object>(), "qb1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task RendersTheFormWithItsFilter_AndTheFieldNames()
    {
        var schema = QueryBuilder<Row>.Create()
            .Text("city", "City", r => r.City)
            .Number("amount", "Amount", r => r.Amount)
            .Schema;
        var filter = new FilterGroup(FilterJoin.And, [new FilterRule("city", FilterOperator.Equals, "Oslo")]);

        var html = await RenderAsync(new QueryBuilderTagHelper { Schema = schema, Model = filter, Action = "/tables/query" });

        Assert.StartsWith("<form class=\"m-qb\" method=\"get\" action=\"/tables/query\"", html);
        Assert.Contains("name=\"f.0.value\" value=\"Oslo\"", html);
        Assert.Contains("name=\"f.op\" value=\"add-rule:\"", html);
        Assert.DoesNotContain("<app-query-builder", html);
    }

    [Fact]
    public async Task WithoutAFilter_ShowsAnEmptyBuilder()
    {
        var html = await RenderAsync(new QueryBuilderTagHelper { Schema = QueryBuilder<Row>.Create().Number("amount", "Amount", r => r.Amount).Schema });

        Assert.DoesNotContain("m-qb-rule", html);
        Assert.Contains("Apply filters", html);
    }
}
