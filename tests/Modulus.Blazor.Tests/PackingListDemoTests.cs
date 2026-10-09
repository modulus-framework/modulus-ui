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

    [Fact]
    public void AChosenColumnOrder_ReordersTheTableAndKeepsTheHeaderBlock()
    {
        var chosen = new[] { "Remarks", "Style", "#", "Color", "Size", "Quantity", "Carton No.", "G.W. (KGS)", "N.W. (KGS)" };

        var rows = PackingListDemo.Fill(Order, "hm", chosen).Rows;

        Assert.Equal("Remarks", rows[6][0]);
        Assert.Equal("#", rows[6][2]);
        Assert.Equal("ST-00123", rows[7][1]);
        Assert.Equal("PACKING LIST", rows[0][0]);
        Assert.Equal("Buyer:", rows[1][0]);
    }

    [Fact]
    public void AnUnknownOrPartialColumnOrder_KeepsTheRestAtTheEnd()
    {
        var headings = PackingListDemo.Headings("hm");

        var rows = PackingListDemo.Fill(Order, "hm", ["Size", "Nonsense", "Size"]).Rows;

        Assert.Equal("Size", rows[6][0]);
        Assert.Equal(headings.Count, rows[6].Count);
        Assert.Equal("#", rows[6][1]);
    }

    [Fact]
    public void TheColumnOrder_ReachesTheDownload()
    {
        var excel = PackingListDemo.Download("PO-2025-001", "xlsx", "hm", ["Remarks", "Style"]);

        Assert.NotNull(excel);
        Assert.NotEmpty(excel!.Value.Bytes);
    }


    private static PackingOrder Custom(params PackingLine[] lines) =>
        new("PO-T", "Test", new DateTime(2025, 1, 1), new DateTime(2025, 2, 1), "ST-1", "Test", "Red", "SS26", 6, lines, OrderQuantity: 300);

    private static PackingLine Line(string carton, int qty = 100, decimal gross = 10m, decimal net = 9m) =>
        new("ST-1", "Red", "M", qty, carton, gross, net);

    [Fact]
    public void TheSamplePurchaseOrders_PassTheChecks_ExceptTheShortOne()
    {
        var all = PackingListDemo.Orders.ToDictionary(o => o.Number, o => PackingListDemo.Checks(o));

        Assert.Empty(all["PO-2025-001"]);
        Assert.Empty(all["PO-2025-007"]);
        var short12 = Assert.Single(all["PO-2025-004"]);
        Assert.Equal("warning", short12.Level);
        Assert.Equal("Shipped 660 of 672 pieces: 12 short.", short12.Message);
    }

    [Fact]
    public void ACartonInTwoLines_IsAnError()
    {
        var checks = PackingListDemo.Checks(Custom(Line("1-3"), Line("3-4"), Line("5-6")));

        var error = Assert.Single(checks, c => c.Level == "error");
        Assert.Equal("Line 2: carton 3 is also in another line.", error.Message);
    }

    [Fact]
    public void ACartonGap_And_AWrongLastCarton_AreWarnings()
    {
        var checks = PackingListDemo.Checks(Custom(Line("1-2"), Line("4-5")));

        Assert.Contains(checks, c => c.Level == "warning" && c.Message == "Carton 3 is not in any line.");
        Assert.Contains(checks, c => c.Level == "warning" && c.Message == "The carton ranges end at carton 5, but the order has 6 cartons.");
    }

    [Fact]
    public void AnUnreadableCartonRange_IsAnError()
    {
        var checks = PackingListDemo.Checks(Custom(Line("1-2"), Line("x-3")));

        Assert.Contains(checks, c => c.Level == "error" && c.Message.StartsWith("Line 2: carton no. \"x-3\""));
    }

    [Fact]
    public void GrossBelowNet_IsAnError_AndAMissingWeight_IsAWarning()
    {
        var checks = PackingListDemo.Checks(Custom(Line("1-3", gross: 8m, net: 9m), Line("4-6", gross: 10m, net: 0m)));

        Assert.Contains(checks, c => c.Level == "error" && c.Message == "Line 1 (M, carton 1-3): gross weight is below net weight.");
        Assert.Contains(checks, c => c.Level == "warning" && c.Message == "Line 2 (M, carton 4-6): a weight is missing.");
    }

    [Fact]
    public void AnOverShipment_IsAWarning()
    {
        var checks = PackingListDemo.Checks(Custom(Line("1-3", qty: 200), Line("4-6", qty: 200)));

        Assert.Contains(checks, c => c.Level == "warning" && c.Message == "Shipped 400 of 300 pieces: 100 over.");
    }

    [Fact]
    public void TheShippingFields_FillFromTheOrder()
    {
        var rows = PackingListDemo.Fill(PackingListDemo.Orders[0], "hm", null).Rows;
        var fill = PackingListDemo.FillWith(PackingListDemo.Orders[0], new[] { new[] { "{{Invoice.Number}}", "{{Container.Number}}", "{{Terms}}" } });

        Assert.Equal(["INV-2025-0412", "MSKU4821350", "FOB"], fill.Rows[0].Select(c => c?.ToString()).ToArray());
        Assert.NotEmpty(rows);
    }
}
