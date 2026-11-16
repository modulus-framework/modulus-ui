using Modulus.AspNetCore.Fluid;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Charts;

/// <summary>
/// The chart pages of the Fluid sample: the eight charts of the MVC and Razor Pages templates (line, column, donut, heatmap, radar,
/// candlestick, sankey, org chart), each drawn as SVG by <see cref="ChartHtml"/> on the server. The page passes the SVG as
/// <see cref="LiquidHtml"/>: it is markup the application made, so Liquid prints it unescaped. The data is the sample data of the
/// MVC templates; replace it with a query or an API call.
/// </summary>
internal static class ChartPages
{
    public static void Map(WebApplication app, string templates)
    {
        app.MapGet("/charts", (ModulusLiquid liquid) =>
            Page(liquid, templates, "charts/index", "Charts", new Dictionary<string, object?>
            {
                ["charts"] = Charts.Select(c => new Dictionary<string, object?> { ["slug"] = c.Slug, ["title"] = c.Title }).ToList(),
            }));

        foreach (var chart in Charts)
        {
            var chartValue = chart;
            app.MapGet("/charts/" + chart.Slug, (ModulusLiquid liquid) =>
                Page(liquid, templates, "charts/chart", chartValue.Title, new Dictionary<string, object?>
                {
                    ["chart_title"] = chartValue.Title,
                    ["chart"] = new LiquidHtml(chartValue.Render()),
                }));
        }
    }

    private static IResult Page(ModulusLiquid liquid, string templates, string page, string title, Dictionary<string, object?> values)
    {
        values["title"] = title;
        var html = liquid.RenderPage(
            liquid.Load(Path.Combine(templates, page + ".liquid")),
            liquid.Load(Path.Combine(templates, "_layout.liquid")),
            values);
        return Results.Content(html, "text/html; charset=utf-8");
    }

    private sealed record Chart(string Slug, string Title, Func<string> Render);

    private static readonly IReadOnlyList<Chart> Charts =
    [
        new("line", "Line chart", () => ChartHtml.Line(
            ["Jan", "Feb", "Mar", "Apr", "May", "Jun"],
            [new ChartSeries("Revenue", [12, 19, 14, 22, 27, 31]), new ChartSeries("Costs", [9, 11, 12, 15, 16, 18])])),

        new("column", "Column chart", () => ChartHtml.Columns(
            ["Q1", "Q2", "Q3", "Q4"],
            [new ChartSeries("North", [40, 52, 47, 61]), new ChartSeries("South", [33, 38, 45, 49])])),

        new("donut", "Donut chart", () => ChartHtml.Donut(
            [new ChartSlice("Search", 540), new ChartSlice("Direct", 320), new ChartSlice("Referral", 180), new ChartSlice("Social", 110)])),

        new("heatmap", "Heatmap", () => ChartHtml.Heatmap(
            ["Mon", "Tue", "Wed", "Thu", "Fri"],
            ["9", "11", "13", "15", "17"],
            [[4, 8, 6, 9, 3], [5, 9, 7, 8, 4], [3, 7, 9, 6, 5], [6, 8, 8, 7, 2], [2, 5, 6, 4, 1]])),

        new("radar", "Radar chart", () => ChartHtml.Radar(
            ["Quality", "Delivery", "Price", "Service", "Compliance"],
            [new ChartSeries("Supplier A", [4.5, 4.0, 3.2, 4.1, 4.7]), new ChartSeries("Supplier B", [3.6, 4.6, 4.4, 3.5, 3.9])])),

        new("candlestick", "Candlestick chart", () => ChartHtml.Candlestick(
            [new Candle("Mon", 80, 83, 79, 82, 9000), new Candle("Tue", 82, 84, 80, 81, 11000),
             new Candle("Wed", 81, 82, 77, 78, 15000), new Candle("Thu", 78, 81, 77, 80.5, 8000),
             new Candle("Fri", 80.5, 85, 80, 84, 12000)])),

        new("sankey", "Sankey chart", () => ChartHtml.Sankey(
            [new SankeyLink("Supplier A", "Plant", 60), new SankeyLink("Supplier B", "Plant", 40),
             new SankeyLink("Plant", "Customers", 85), new SankeyLink("Plant", "Scrap", 15)])),

        new("org", "Org chart", () => ChartHtml.OrgChart(
            [new OrgNode("1", null, "Ada Lovelace", "CEO"), new OrgNode("2", "1", "Grace Hopper", "Finance"),
             new OrgNode("3", "1", "Alan Turing", "Technology"), new OrgNode("4", "3", "Linus T.", "Platform")])),
    ];
}
