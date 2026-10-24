using System.Text.RegularExpressions;
using Bunit;
using Modulus.Blazor.Components.Charts;
using Modulus.Ui.Abstractions.Charts;
using Xunit;

namespace Modulus.Blazor.Tests;

public class BusinessChartGeometryTests
{
    [Theory]
    [InlineData(-40, 90, new double[] { -50, 0, 50, 100 })]
    [InlineData(-900, -100, new double[] { -1000, -800, -600, -400, -200, 0 })]
    [InlineData(10, 87, new double[] { 0, 20, 40, 60, 80, 100 })]
    public void NiceRange_SpansBothSidesAndKeepsZero(double min, double max, double[] expected) =>
        Assert.Equal(expected, ChartGeometry.NiceRange(min, max));

    [Theory]
    [InlineData(-1200, "-1.2k")]
    [InlineData(-3_400_000, "-3.4M")]
    public void Compact_KeepsTheSignOfNegatives(double value, string expected) =>
        Assert.Equal(expected, ChartGeometry.Compact(value));

    [Fact]
    public void Squarify_TilesTheWholeBoxWithoutOverlap()
    {
        double[] values = [6, 6, 4, 3, 2, 2, 1];
        var rects = ChartGeometry.Squarify(values, 6, 4);

        Assert.Equal(values.Length, rects.Count);
        Assert.Equal(24, rects.Sum(r => r.W * r.H), 6);
        for (var i = 0; i < rects.Count; i++)
        {
            Assert.Equal(values[i], rects[i].W * rects[i].H, 6); // area ∝ value (total 24 in a 6×4 box)
            Assert.InRange(rects[i].X + rects[i].W, 0, 6 + 1e-9);
            Assert.InRange(rects[i].Y + rects[i].H, 0, 4 + 1e-9);
            for (var j = i + 1; j < rects.Count; j++)
            {
                var (a, b) = (rects[i], rects[j]);
                var overlap = Math.Max(0, Math.Min(a.X + a.W, b.X + b.W) - Math.Max(a.X, b.X)) * Math.Max(0, Math.Min(a.Y + a.H, b.Y + b.H) - Math.Max(a.Y, b.Y));
                Assert.True(overlap < 1e-9, $"tiles {i} and {j} overlap");
            }
        }
    }
}

public class BusinessChartHtmlTests
{
    private static readonly ChartOptions Money = new() { Prefix = "$", Title = "Test" };

    private static List<string> Rows(string html) =>
        Regex.Matches(html, "<tr><th scope=\"row\">(.*?)</tr>").Select(m => Regex.Replace(m.Groups[1].Value, "<.*?>", "|")).ToList();

    [Fact]
    public void Waterfall_TotalsCarryTheRunningSumAndChangesTheirSign()
    {
        var html = ChartHtml.Waterfall(
        [
            new("Revenue", 1000), new("COGS", -600), new("Gross profit", 0, IsTotal: true), new("Opex", -250), new("Net", 0, IsTotal: true),
        ], Money);

        var rows = Rows(html);
        Assert.Equal("Revenue||+$1,000||$1,000|", rows[0]);
        Assert.Equal("COGS||−$600||$400|", rows[1]);
        Assert.Equal("Gross profit||||$400|", rows[2]);
        Assert.Equal("Net||||$150|", rows[4]);
        Assert.Equal(2, Regex.Matches(html, "m-wf-bar\" style=\"[^\"]*--c:var\\(--m-div-neg\\)").Count);
        Assert.Equal(2, Regex.Matches(html, "m-wf-bar\" style=\"[^\"]*--c:var\\(--m-chart-neutral\\)").Count);
        Assert.Equal(4, Regex.Matches(html, "class=\"m-wf-link\"").Count); // a connector between each pair of bars
    }

    [Fact]
    public void Waterfall_GoingNegativeAddsTicksBelowZero()
    {
        var html = ChartHtml.Waterfall([new("Start", 100), new("Loss", -300)], Money);

        Assert.Contains(">-$200</span>", html);
        Assert.Contains("m-xy-grid m-xy-base", html);
    }

    [Fact]
    public void RankedBars_SortsAndFoldsTheTailIntoOther()
    {
        var html = ChartHtml.RankedBars([new("C", 10), new("A", 50), new("B", 30), new("D", 6), new("E", 4)], Money, limit: 3, showShare: true);

        var rows = Rows(html);
        Assert.Equal(["A||$50||50%|", "B||$30||30%|", "Other||$20||20%|"], rows);
        Assert.Contains("m-rc m-rc-4", html);
        Assert.Contains("--c:var(--m-chart-neutral)", html);
    }

    [Fact]
    public void Funnel_ReportsConversionFromThePreviousAndFirstStage()
    {
        var html = ChartHtml.Funnel([new("Leads", 1000), new("Quoted", 400), new("Won", 100)]);

        var rows = Rows(html);
        Assert.Equal("Quoted||400||40%||40%|", rows[1]);
        Assert.Equal("Won||100||25%||10%|", rows[2]);
        Assert.Contains("600 lost", html);
        Assert.Contains("color-mix(in oklab, var(--m-ord-hi) 100%, var(--m-ord-lo))", html); // first stage darkest
    }

    [Fact]
    public void Variance_ColoursBySignAndFavorability()
    {
        ChartSlice[] items = [new("Production", 120), new("Marketing", -80)];

        var cost = ChartHtml.Variance(items, Money, positiveIsFavorable: false);
        var revenue = ChartHtml.Variance(items, Money);

        Assert.Contains("left:50%;width:50%;--c:var(--m-div-neg)", cost);
        Assert.Contains("right:50%;width:33.33%;--c:var(--m-div-pos)", cost);
        Assert.Contains("left:50%;width:50%;--c:var(--m-div-pos)", revenue);
        Assert.Equal("Production||+$120||Unfavorable|", Rows(cost)[0]);
    }

    [Fact]
    public void Pareto_ClassifiesByCumulativeShareOnOnePercentAxis()
    {
        var html = ChartHtml.Pareto([new("x", 10), new("a", 70), new("b", 15), new("c", 5)], Money);

        Assert.Contains("<span>Class A</span>", html);
        Assert.Contains("<span>Class B</span>", html);
        Assert.Contains("<span>Class C</span>", html);
        Assert.Contains(">A &#183; 80%</span>", html);
        Assert.Contains(">100%</span>", html); // the axis is percent, not dollars
        var rows = Rows(html);
        Assert.Equal("a||70.0%||70.0%|", rows[0]);
        Assert.Equal("c||5.0%||100.0%|", rows[3]);
    }

    [Fact]
    public void Combo_DrawsLinesOnTheColumnsAxis()
    {
        var html = ChartHtml.Combo(["Q1", "Q2"], [new("Actual", [80, 120])], [new("Budget", [100, 100])], Money);

        Assert.Contains("<span class=\"m-key m-key-rect\" style=\"--c:var(--m-series-1)\"></span>Actual", html);
        Assert.Contains("<span class=\"m-key m-key-line\" style=\"--c:var(--m-series-2)\"></span>Budget", html);
        Assert.Contains("class=\"m-xy-line\" d=\"M250,333.33L750,333.33\"", html); // 100 of a 0–150 axis
        Assert.Equal("Q2||$120||$100|", Rows(html)[1]);
    }

    [Fact]
    public void StackedBars_NormalizesAndUsesTheOrdinalRamp()
    {
        var html = ChartHtml.StackedBars(["Acme"], [new("Current", [75]), new("Overdue", [25])], Money, normalize: true, ordinal: true);

        Assert.Contains("class=\"m-rc-stack\" style=\"width:100%\"", html);
        Assert.Contains("$75 (75%)", html);
        Assert.Contains("var(--m-ord-hi) 0%", html);
        Assert.Contains("var(--m-ord-hi) 100%", html);
    }

    [Fact]
    public void Treemap_FoldsPastEightTiles()
    {
        var items = Enumerable.Range(1, 10).Select(i => new ChartSlice($"P{i}", 11 - i)).ToList();

        var html = ChartHtml.Treemap(items);

        Assert.Equal(8, Regex.Matches(html, "class=\"m-tm-tile").Count);
        Assert.Contains("Other", html);
        Assert.Equal("Other||6||11%|", Rows(html)[7]); // 3 + 2 + 1 of 55
    }

    [Fact]
    public void Gauge_DrawsTheFillAndTargetAndNamesThemForScreenReaders()
    {
        var html = ChartHtml.Gauge(90, 100, 95, new ChartOptions { Suffix = "%", Title = "OTIF" }, "Last 90 days");

        Assert.Contains("aria-label=\"OTIF: 90% of 100%, target 95%\"", html);
        Assert.Contains("stroke-dasharray=\"90 200\"", html);
        Assert.Contains("class=\"m-gauge-target\"", html);
    }

    [Fact]
    public void Bullet_ReportsAttainment()
    {
        var html = ChartHtml.Bullet([new BulletItem("Sales", 90, 120, [60, 100, 140])]);

        Assert.Contains("75% of target", html);
        Assert.Equal(3, Regex.Matches(html, "class=\"m-bullet-band ").Count);
        Assert.Contains("class=\"m-bullet-target\" style=\"left:85.71%\"", html);
    }

    [Fact]
    public void Scatter_SizesBubblesByAreaAndListsEveryPoint()
    {
        var html = ChartHtml.Scatter(
            [new("Big", 10, 20, 100, 0), new("Small", 30, 40, 25, 1)],
            new ChartOptions { Title = "Portfolio" },
            new ScatterOptions { Groups = ["A", "B"], SizeTitle = "Units", Quadrants = true });

        Assert.Contains("--d:40px", html); // largest bubble
        Assert.Contains("--d:24px", html); // quarter the value, half the diameter span
        Assert.Contains("m-sc-mean-x", html);
        Assert.Contains("Bubble size: Units", html);
        Assert.Equal(2, Rows(html).Count);
    }

    [Fact]
    public void EmptyInputs_RenderTheEmptyState()
    {
        Assert.Contains("m-chart-empty", ChartHtml.RankedBars([]));
        Assert.Contains("m-chart-empty", ChartHtml.Waterfall([]));
        Assert.Contains("m-chart-empty", ChartHtml.Funnel([]));
        Assert.Contains("m-chart-empty", ChartHtml.Treemap([]));
        Assert.Contains("m-chart-empty", ChartHtml.Pareto([]));
        Assert.Contains("m-chart-empty", ChartHtml.Scatter([]));
    }
}

public class BusinessChartComponentTests : AsyncBunitContext
{
    [Fact]
    public void WaterfallChart_RendersTheSharedMarkup()
    {
        WaterfallStep[] steps = [new("Revenue", 500), new("Costs", -200), new("Profit", 0, IsTotal: true)];

        var cut = Render<WaterfallChart>(p => p.Add(c => c.Steps, steps).Add(c => c.Prefix, "$").Add(c => c.Title, "Bridge"));

        Assert.Equal(3, cut.FindAll(".m-wf-bar").Count);
        Assert.Equal(3, cut.FindAll("table tbody tr").Count);
        cut.MarkupMatches(ChartHtml.Waterfall(steps, new ChartOptions { Prefix = "$", Title = "Bridge" }));
    }

    [Fact]
    public void VarianceChart_PassesFavorability()
    {
        var cut = Render<VarianceChart>(p => p.Add(c => c.Items, [new ChartSlice("IT", 10)]).Add(c => c.PositiveIsFavorable, false));

        Assert.Contains("Unfavorable", cut.Find("table").TextContent);
    }
}
