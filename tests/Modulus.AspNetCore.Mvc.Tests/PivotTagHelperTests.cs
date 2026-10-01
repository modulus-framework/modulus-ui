using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Pivot;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>&lt;app-pivot-grid&gt;: the same markup as the Blazor AppPivotGrid (PivotHtml).</summary>
public class PivotTagHelperTests
{
    private sealed record Row(string City, int Qty, decimal Amount);

    private static readonly PivotQuery<Row> Pivot = PivotQuery<Row>.Create()
        .Dimension("city", "City", r => r.City)
        .Measure("qty", "Quantity", r => r.Qty)
        .Measure("amount", "Amount", r => r.Amount)
        .Default(rows: ["city"], columns: [], measures: ["qty", "amount"]);

    private static readonly Row[] Data = [new("Oslo", 2, 10.5m), new("Oslo", 1, 4m), new("Rome", 5, 20m)];

    private static async Task<string> RenderAsync(TagHelper helper)
    {
        var output = new TagHelperOutput("app-pivot-grid", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext("app-pivot-grid", [], new Dictionary<object, object>(), "x"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private static string Href(PivotState s) => "/p?" + s.ToQueryString();

    [Fact]
    public async Task PivotGrid_IsTheSharedMarkup()
    {
        var result = Pivot.Run(Data.AsQueryable(), Pivot.DefaultState);
        string? Cell(PivotCell c) => "/list?city=" + c.Row.Key;

        var html = await RenderAsync(new PivotGridTagHelper { Result = result, Href = Href, CellHref = Cell, DownloadHref = "/p/download", Id = "pv", Label = "Sales" });

        Assert.Equal(PivotHtml.Render(result, Href, Cell, "/p/download", "pv", "Sales"), html);
        Assert.Contains("<a href=\"/list?city=Oslo\">3</a>", html);
        Assert.Contains($"<a href=\"/list?city=Oslo\">{result.Format(14.5m, result.Measures[1])}</a>", html);
    }

    [Fact]
    public async Task PivotGrid_WithoutAResult_RendersNothing()
    {
        Assert.Equal("", await RenderAsync(new PivotGridTagHelper()));
    }

    [Fact]
    public void Link_KeepsThePagesOwnQueryValues_AndReplacesThePivots()
    {
        var link = PivotState.Link("/p", PivotState.ReadQuery("?status=Packed&rows=old&rx=*&q=a+b"), Pivot.DefaultState);

        Assert.Equal("/p?status=Packed&q=a%20b&rows=city&cols=&measures=qty%2Camount&rx=%2A", link);
    }
}
