using Bunit;
using Modulus.Blazor.Components.Charts;
using Modulus.Ui.Abstractions.Charts;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>Radar, candlestick, Sankey and org charts: the shared renderer, and the Blazor components that wrap it.</summary>
public class AdvancedChartTests : BunitContext
{
    // ---- Radar ----

    [Fact]
    public void Radar_DrawsOnePolygonPerSeries_APointPerValue_AndATable()
    {
        var html = ChartHtml.Radar(["Quality", "Cost", "Delivery", "Service"], [new("Acme", [4, 3, 5, 2]), new("Beta", [2, 5, 3, 4])], new() { Title = "Suppliers" }, max: 5);

        Assert.Equal(2, CountOf(html, "m-radar-area"));
        Assert.Equal(8, CountOf(html, "m-radar-pt m-hov"));
        Assert.Equal(4, CountOf(html, "m-radar-spoke"));
        Assert.Contains("<th scope=\"col\">Acme</th>", html);
        Assert.Contains("Delivery · Acme", html);
    }

    [Fact]
    public void Radar_NeedsThreeAxes_AndTreatsMissingValuesAsZero()
    {
        Assert.Contains("No data", ChartHtml.Radar(["a", "b"], [new("s", [1, 2])]));
        var html = ChartHtml.Radar(["a", "b", "c"], [new("s", [3])]);
        Assert.Equal(3, CountOf(html, "m-radar-pt m-hov"));
    }

    // ---- Candlestick ----

    [Fact]
    public void Candlestick_MarksUpAndDownPeriods_AndShowsVolume()
    {
        Candle[] candles = [new("Mon", 10, 12, 9, 11.5, 1000), new("Tue", 11.5, 12, 8, 9, 2500)];

        var html = ChartHtml.Candlestick(candles, new() { Title = "ACME", Prefix = "$", ValueFormat = "N2" });

        Assert.Equal(2, CountOf(html, "m-candle-slot m-hov"));
        Assert.Equal(2, CountOf(html, "m-candle-vbar"));
        Assert.Contains("--c:var(--m-div-pos)", html);
        Assert.Contains("--c:var(--m-div-neg)", html);
        Assert.Contains("Mon · up +15.0%", html);
        Assert.Contains("Tue · down −21.7%", html);
        Assert.Contains("Open $10.00 · High $12.00", html);
    }

    [Fact]
    public void Candlestick_WithoutVolume_HasNoVolumeStrip_AndHandlesAFlatPeriod()
    {
        var html = ChartHtml.Candlestick([new("Only", 5, 5, 5, 5)]);

        Assert.DoesNotContain("m-candle-vol", html);
        Assert.Contains("m-candle-body", html);
    }

    // ---- Sankey ----

    [Fact]
    public void Sankey_LaysNodesInColumns_AndScalesFlowsTogether()
    {
        SankeyLink[] links = [new("Supplier A", "Plant", 60), new("Supplier B", "Plant", 40), new("Plant", "Customer X", 70), new("Plant", "Customer Y", 30)];

        var html = ChartHtml.Sankey(links, new() { Title = "Material flow", Height = 300 });

        Assert.Equal(5, CountOf(html, "m-sk-node m-hov"));
        Assert.Equal(4, CountOf(html, "m-sk-link"));
        Assert.Contains("Supplier A → Plant: 60", html);
        Assert.Contains("In 100 · Out 100", html); // the plant
        // Two columns of suppliers/customers use one scale, so a 60 flow is thicker than a 40 flow.
        Assert.Contains("stroke-width:", html);
    }

    [Fact]
    public void Sankey_DropsLoopsAndNonPositiveFlows()
    {
        SankeyLink[] links = [new("A", "B", 10), new("B", "C", 10), new("C", "B", 5), new("A", "A", 3), new("A", "C", 0), new("A", "D", -4)];

        var html = ChartHtml.Sankey(links);

        Assert.Equal(2, CountOf(html, "class=\"m-sk-link\"")); // A→B and B→C; the loop back, the self link and the zero and negative flows are gone
        Assert.DoesNotContain("A → A", html);
        Assert.DoesNotContain("A → D", html);
        Assert.DoesNotContain("A → C", html);
    }

    [Fact]
    public void Sankey_OnlyInvalidLinks_IsEmpty() =>
        Assert.Contains("No data", ChartHtml.Sankey([new("A", "A", 5), new("B", "C", 0)]));

    // ---- Org chart ----

    [Fact]
    public void OrgChart_NestsReportsUnderTheirManager_AndCountsDirectReports()
    {
        OrgNode[] nodes =
        [
            new("ceo", null, "Ada Lovelace", "CEO", "Board"),
            new("cfo", "ceo", "Grace Hopper", "CFO"),
            new("cto", "ceo", "Alan Turing", "CTO"),
            new("dev", "cto", "Linus", "Engineer"),
        ];

        var html = ChartHtml.OrgChart(nodes, new() { Title = "Company" });

        Assert.Equal(4, CountOf(html, "m-org-node"));
        Assert.Equal(2, CountOf(html, "<ul>")); // under the CEO and under the CTO
        Assert.Contains("<span class=\"m-org-count\" title=\"2 direct\">2</span>", html);
        Assert.Contains("<span class=\"m-org-badge\">Board</span>", html);
        Assert.True(html.IndexOf("Alan Turing", StringComparison.Ordinal) < html.IndexOf("Linus", StringComparison.Ordinal));
    }

    [Fact]
    public void OrgChart_UnknownParentsAreRoots_AndLoopsDoNotHang()
    {
        var forest = ChartHtml.OrgChart([new("a", "missing", "A"), new("b", null, "B")]);
        Assert.Equal(2, CountOf(forest, "m-org-node"));

        var loop = ChartHtml.OrgChart([new("a", "b", "A"), new("b", "a", "B")]);
        Assert.Contains("m-org-node", loop);
        Assert.Equal(2, CountOf(loop, "m-org-title")); // both drawn once, neither repeated
    }

    [Fact]
    public void OrgChart_EscapesText_AndOnlyLinksSafeUrls()
    {
        var html = ChartHtml.OrgChart(
        [
            new("1", null, "<img src=x onerror=alert(1)>", "<b>x</b>", null, "javascript:alert(1)"),
            new("2", "1", "Safe", null, null, "/people/2"),
            new("3", "1", "Data", null, null, "data:text/html,boom"),
            new("4", "1", "External", null, null, "https://example.com/p"),
            new("5", "1", "Protocol-relative", null, null, "//evil.example/p"),
        ]);

        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<b>x</b>", html);
        Assert.DoesNotContain("javascript:", html);
        Assert.DoesNotContain("data:text", html);
        Assert.DoesNotContain("//evil.example", html);
        Assert.Contains("href=\"/people/2\"", html);
        Assert.Contains("href=\"https://example.com/p\"", html);
    }

    // ---- Blazor components ----

    [Fact]
    public void Components_RenderTheSharedMarkup()
    {
        var radar = Render<RadarChart>(p => p.Add(c => c.Axes, ["a", "b", "c"]).Add(c => c.Series, [new ChartSeries("s", [1, 2, 3])]));
        Assert.Contains("m-radar-area", radar.Markup);

        var candles = Render<CandlestickChart>(p => p.Add(c => c.Candles, [new Candle("d", 1, 2, 0.5, 1.5)]));
        Assert.Contains("m-candle-body", candles.Markup);

        var sankey = Render<SankeyChart>(p => p.Add(c => c.Flows, [new SankeyLink("A", "B", 3)]));
        Assert.Contains("m-sk-link", sankey.Markup);

        var org = Render<OrgChart>(p => p.Add(c => c.Nodes, [new OrgNode("1", null, "Root")]));
        Assert.Contains("m-org-node", org.Markup);
    }

    private static int CountOf(string html, string needle)
    {
        var count = 0;
        for (var i = html.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = html.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) count++;
        return count;
    }
}
