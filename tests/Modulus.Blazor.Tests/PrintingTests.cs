using Modulus.Ui.Abstractions.Printing;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The printing library: the page rule, the printed table and the label sheets.</summary>
public class PrintingTests
{
    [Fact]
    public void PageRule_SetsThePaperOrientationAndMargin()
    {
        Assert.Equal("@page { size: A4; margin: 12mm; }", PrintCss.Page(new PrintOptions()));
        Assert.Equal("@page { size: letter landscape; margin: 0mm; }", PrintCss.Page(new PrintOptions(PaperSize.Letter, Landscape: true, MarginMm: 0)));
    }

    [Fact]
    public void PageRule_RefusesAMarginOutOfRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PrintCss.Page(new PrintOptions(MarginMm: 51)));
        Assert.Throws<ArgumentOutOfRangeException>(() => PrintCss.Page(new PrintOptions(MarginMm: -1)));
    }

    [Fact]
    public void Table_HasItsHeaderInAThead_AlignsNumbers_AndEncodesEveryCell()
    {
        var html = PrintTableHtml.Render(
            [new PrintColumn("Part"), new PrintColumn("Qty", Right: true)],
            [["<b>Bolt</b>", "4"], ["Nut"]]);

        Assert.Contains("<table class=\"m-print-table\"><thead><tr><th>Part</th><th class=\"n\">Qty</th></tr></thead>", html);
        Assert.Contains("<td>&lt;b&gt;Bolt&lt;/b&gt;</td><td class=\"n\">4</td>", html);
        Assert.Contains("<td>Nut</td><td class=\"n\"></td>", html);
        Assert.DoesNotContain("<tfoot>", html);
    }

    [Fact]
    public void Table_TotalRow_SitsInATfoot()
    {
        var html = PrintTableHtml.Render([new PrintColumn("Item"), new PrintColumn("Amount", Right: true)], [["A", "1.00"]], ["Total", "1.00"]);

        Assert.Contains("<tfoot><tr><th>Total</th><th class=\"n\">1.00</th></tr></tfoot>", html);
    }

    [Fact]
    public void Labels_FillASheetLeftToRight_AndStartANewSheetWhenItIsFull()
    {
        var items = Enumerable.Range(1, 22).Select(i => new LabelItem(["Bin", $"A-{i}"])).ToList();

        var html = LabelHtml.Render(items, LabelLayout.A4ThreeBySeven);

        Assert.Equal(2, LabelHtml.SheetCount(items.Count, LabelLayout.A4ThreeBySeven));
        Assert.Equal(2, CountOf(html, "class=\"m-label-sheet\""));
        Assert.Equal(22, CountOf(html, "class=\"m-label\""));
        Assert.Equal(22, CountOf(html, "class=\"m-label-title\""));
        Assert.Contains("A-21", html);
    }

    [Fact]
    public void Labels_CarryTheSheetGeometryAsVariables()
    {
        var html = LabelHtml.Render([new LabelItem(["Bin"])], LabelLayout.A4ThreeBySeven);

        Assert.Contains("--m-label-cols:3", html);
        Assert.Contains("--m-label-rows:7", html);
        Assert.Contains("--m-label-w:63.5mm", html);
        Assert.Contains("--m-label-h:38.1mm", html);
        Assert.Contains("--m-label-gap-x:2.5mm", html);
        Assert.Contains("--m-label-top:15mm", html);
    }

    [Fact]
    public void Labels_EncodeEveryLine_AndTheSecondAndLaterLinesAreNotTitles()
    {
        var html = LabelHtml.Render([new LabelItem(["<Shelf>", "Row & \"3\""])], LabelLayout.A4ThreeBySeven);

        Assert.Contains("<p class=\"m-label-title\">&lt;Shelf&gt;</p>", html);
        Assert.Contains("<p class=\"m-label-line\">Row &amp; &quot;3&quot;</p>", html);
    }

    [Fact]
    public void Labels_NoItems_NoSheets()
    {
        var html = LabelHtml.Render([], LabelLayout.A4ThreeBySeven);

        Assert.Equal(0, LabelHtml.SheetCount(0, LabelLayout.A4ThreeBySeven));
        Assert.DoesNotContain("m-label-sheet", html);
    }

    [Fact]
    public void Labels_RefuseANonsenseLayout()
    {
        var items = new[] { new LabelItem(["x"]) };

        Assert.Throws<ArgumentOutOfRangeException>(() => LabelHtml.Render(items, new LabelLayout(0, 7, 63.5, 38.1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => LabelHtml.Render(items, new LabelLayout(3, 7, 0, 38.1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => LabelHtml.Render(items, new LabelLayout(3, 7, 63.5, 38.1, GapXMm: -1)));
    }

    private static int CountOf(string text, string needle) => (text.Length - text.Replace(needle, "", StringComparison.Ordinal).Length) / needle.Length;
}
