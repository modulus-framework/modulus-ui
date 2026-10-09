using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Printing;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>app-label-sheet and app-print-table: the printing renderers, as tag helpers.</summary>
public class PrintTagHelperTests
{
    private static async Task<string> RenderAsync(TagHelper helper, string tag)
    {
        var output = new TagHelperOutput(tag, [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext(tag, [], new Dictionary<object, object>(), "p1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task LabelSheet_RendersTheLabelsOnTheLayout()
    {
        var items = new[] { new LabelItem(["Bin <1>", "Rack 2"]) };

        var html = await RenderAsync(new LabelSheetTagHelper { Items = items }, "app-label-sheet");

        Assert.Equal(LabelHtml.Render(items, LabelLayout.A4ThreeBySeven), html);
        Assert.Contains("Bin &lt;1&gt;", html);
    }

    [Fact]
    public async Task PrintTable_RendersTheHeaderAndTotal()
    {
        var html = await RenderAsync(new PrintTableTagHelper
        {
            Columns = [new PrintColumn("Part"), new PrintColumn("Qty", Right: true)],
            Rows = [["Bolt", "4"]],
            Total = ["Total", "4"],
        }, "app-print-table");

        Assert.Contains("<thead><tr><th>Part</th><th class=\"n\">Qty</th></tr></thead>", html);
        Assert.Contains("<tfoot><tr><th>Total</th><th class=\"n\">4</th></tr></tfoot>", html);
    }
}
