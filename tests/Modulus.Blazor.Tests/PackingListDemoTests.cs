using Modulus.Samples.Shared;
using QuestPDF.Infrastructure;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The packing list template: the fill of each field, the item rows repeated per line, the totals, and the errors for a field the order lacks.</summary>
public class PackingListDemoTests
{
    static PackingListDemoTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly PackingOrder Order = PackingListDemo.Orders[0];

    [Fact]
    public void TheHeaderFields_TakeTheOrdersValues()
    {
        var rows = PackingListDemo.Fill(Order).Rows;

        Assert.Equal("H&M", rows[1][1]);
        Assert.Equal("PO-2025-001", rows[2][1]);
        Assert.Equal("ST-00123", rows[3][1]);
        Assert.Equal(52, rows[2][8]);
    }

    [Fact]
    public void TheItemRow_RepeatsOncePerLine()
    {
        var rows = PackingListDemo.Fill(Order).Rows;

        var items = rows.Skip(7).Take(Order.Lines.Count).ToList();
        Assert.Equal(Order.Lines.Count, items.Count);
        Assert.Equal(1, items[0][0]);
        Assert.Equal(Order.Lines[2].Size, items[2][3]);
        Assert.Equal(Order.Lines[5].Quantity, items[5][4]);
    }

    [Fact]
    public void TheTotals_AreSummedFromTheLines()
    {
        var total = PackingListDemo.Fill(Order).Rows.Last();

        Assert.Equal("Total", total[0]);
        Assert.Equal(Order.Lines.Sum(l => l.Quantity), total[4]);
        Assert.Equal(Order.Lines.Sum(l => l.GrossWeight).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture), total[6]);
    }

    [Fact]
    public void AFilledTemplate_HasNoUnmappedFields()
    {
        Assert.Empty(PackingListDemo.Fill(Order).Errors);
    }

    [Fact]
    public void AnUnknownField_IsReportedAndLeftInTheSheet()
    {
        var template = PackingListDemo.TemplateRows;
        var rowsWithUnknown = template.Select(r => r.Select(c => c.Replace("{{PO.Number}}", "{{PO.Missing}}")).ToArray()).ToArray();
        var fill = PackingListDemo.FillWith(Order, rowsWithUnknown);

        Assert.Contains("Unmapped field {{PO.Missing}}", fill.Errors);
        Assert.Contains("{{PO.Missing}}", fill.Rows[2][1]?.ToString());
    }

    [Fact]
    public void TheExcelAndPdfDownloads_AreMadeForAKnownOrder()
    {
        var excel = PackingListDemo.Download("PO-2025-001", "xlsx", "hm");
        var pdf = PackingListDemo.Download("PO-2025-001", "pdf", "hm");

        Assert.NotNull(excel);
        Assert.EndsWith(".xlsx", excel!.Value.FileName);
        Assert.Equal("%PDF-", System.Text.Encoding.Latin1.GetString(pdf!.Value.Bytes, 0, 5));
        Assert.Null(PackingListDemo.Download("PO-0000", "xlsx", "hm"));
    }

    [Fact]
    public void EachTemplate_HasItsOwnHeadings_AndTheSameRowStructure()
    {
        foreach (var template in PackingListDemo.Templates)
        {
            var rows = PackingListDemo.Fill(Order, template.Key).Rows;

            // Five header rows, a blank row and the column headings, then one row per order line, then the total row.
            Assert.Equal(8 + Order.Lines.Count, rows.Count);
            Assert.Equal("Total", rows[^1][0]);
        }
        Assert.Contains("UPC", PackingListDemo.Fill(PackingListDemo.Orders[2], "walmart").Rows[6].Select(c => c?.ToString()));
        Assert.DoesNotContain("Remarks", PackingListDemo.Fill(Order, "zara").Rows[6].Select(c => c?.ToString()));
        Assert.Contains("Remarks", PackingListDemo.Fill(Order, "basic").Rows[6].Select(c => c?.ToString()));
    }

    [Fact]
    public void TheWalmartUpcs_FillTheirItemRows()
    {
        var walmart = PackingListDemo.Orders[2];
        var rows = PackingListDemo.Fill(walmart, "walmart").Rows;

        Assert.Equal(walmart.Lines[0].Upc, rows[7][1]);
        Assert.Equal(walmart.Lines[2].Upc, rows[9][1]);
        Assert.Empty(PackingListDemo.Fill(walmart, "walmart").Errors);
    }
}
