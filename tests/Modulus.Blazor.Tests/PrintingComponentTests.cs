using Bunit;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions.Printing;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The Blazor LabelSheet and PrintTable draw the library's markup, the same the MVC tag helpers draw.</summary>
public class PrintingComponentTests : AsyncBunitContext
{
    [Fact]
    public void LabelSheet_DrawsTheSameLabelsAsTheRenderer()
    {
        var items = Enumerable.Range(1, 22).Select(i => new LabelItem(["Bin", $"A-{i}"])).ToList();

        var cut = Render<LabelSheet>(p => p.Add(x => x.Items, items).Add(x => x.Layout, LabelLayout.A4ThreeBySeven));

        Assert.Equal(22, cut.FindAll(".m-label").Count);
        Assert.Equal(2, cut.FindAll(".m-label-sheet").Count);
        Assert.Contains(LabelHtml.Render(items, LabelLayout.A4ThreeBySeven), cut.Markup);
    }

    [Fact]
    public void PrintTable_HasARepeatingHeader_AndTheTotal()
    {
        var columns = new[] { new PrintColumn("Part"), new PrintColumn("Qty", Right: true) };
        var rows = new IReadOnlyList<string>[] { ["A", "1"], ["B", "2"] };

        var cut = Render<PrintTable>(p => p.Add(x => x.Columns, columns).Add(x => x.Rows, rows).Add(x => x.Total, ["Total", "3"]));

        Assert.Single(cut.FindAll("thead"));
        Assert.Equal(2, cut.FindAll("tbody tr").Count);
        Assert.Contains("<th class=\"n\">3</th>", cut.Markup);
        Assert.Contains(PrintTableHtml.Render(columns, rows, ["Total", "3"]), cut.Markup);
    }
}
