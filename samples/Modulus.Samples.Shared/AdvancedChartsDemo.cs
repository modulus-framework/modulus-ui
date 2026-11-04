using Modulus.Ui.Abstractions.Charts;

namespace Modulus.Samples.Shared;

/// <summary>Demo data for the radar, candlestick, Sankey and org charts; the same in Blazor, MVC and Razor Pages.</summary>
public static class AdvancedChartsDemo
{
    public static IReadOnlyList<string> SupplierAxes { get; } = ["Quality", "Delivery", "Price", "Service", "Compliance", "Innovation"];

    public static IReadOnlyList<ChartSeries> SupplierScores { get; } =
    [
        new("Acme Metals", [4.6, 4.2, 3.1, 4.0, 4.8, 3.2]),
        new("Borealis Plastics", [3.4, 4.7, 4.5, 3.6, 3.9, 4.4]),
    ];

    /// <summary>Thirty trading days of a made-up commodity price, from a fixed seed so the page does not change on reload.</summary>
    public static IReadOnlyList<Candle> Prices { get; } = BuildPrices();

    public static IReadOnlyList<SankeyLink> MaterialFlow { get; } =
    [
        new("Steel mill", "Plant Hamburg", 420), new("Aluminium", "Plant Hamburg", 160), new("Steel mill", "Plant Gdansk", 260), new("Resin", "Plant Gdansk", 140),
        new("Plant Hamburg", "Finished goods", 520), new("Plant Hamburg", "Scrap", 60), new("Plant Gdansk", "Finished goods", 360), new("Plant Gdansk", "Scrap", 40),
        new("Finished goods", "Retail", 600), new("Finished goods", "Wholesale", 280),
    ];

    public static IReadOnlyList<OrgNode> Company { get; } =
    [
        new("ceo", null, "Serena Park", "Chief Executive", "Board"),
        new("cfo", "ceo", "Marcus Webb", "Finance", "FIN"),
        new("coo", "ceo", "Lena Fischer", "Operations", "OPS"),
        new("cto", "ceo", "Ravi Menon", "Technology", "TECH"),
        new("ctrl", "cfo", "Ines Duarte", "Controlling"),
        new("ap", "cfo", "Tom Hale", "Accounts payable"),
        new("plant1", "coo", "Jonas Berg", "Plant Hamburg"),
        new("plant2", "coo", "Agnieszka Nowak", "Plant Gdansk"),
        new("log", "coo", "Hugo Reyes", "Logistics"),
        new("dev", "cto", "Mei Tanaka", "Platform"),
        new("sec", "cto", "Omar Haddad", "Security"),
    ];

    private static IReadOnlyList<Candle> BuildPrices()
    {
        var rng = new Random(2024);
        var list = new List<Candle>();
        var price = 84.0;
        var day = new DateTime(2026, 9, 1);
        while (list.Count < 30)
        {
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) { day = day.AddDays(1); continue; }
            var open = price;
            var close = Math.Round(open + (rng.NextDouble() - 0.47) * 3.2, 2);
            var high = Math.Round(Math.Max(open, close) + rng.NextDouble() * 1.4, 2);
            var low = Math.Round(Math.Min(open, close) - rng.NextDouble() * 1.4, 2);
            list.Add(new Candle(day.ToString("d MMM"), Math.Round(open, 2), high, low, close, Math.Round(8000 + rng.NextDouble() * 9000)));
            price = close;
            day = day.AddDays(1);
        }
        return list;
    }
}
