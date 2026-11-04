using System.Globalization;
using Modulus.Ui.Abstractions.Chat;

namespace Modulus.Samples.Shared;

/// <summary>Sales analytics over the shipments: totals by city, and a monthly forecast from a straight-line trend.</summary>
public static class SalesAnalytics
{
    private sealed record CityTotal(string City, int Shipments, decimal Total);

    /// <summary>The cities with the largest totals, as text and as a table.</summary>
    public static (string Text, ChatTable Table) TopCities(int count)
    {
        var rows = Shipments.Query.GroupBy(s => s.City)
            .Select(g => new CityTotal(g.Key, g.Count(), g.Sum(s => s.Amount)))
            .OrderByDescending(x => x.Total)
            .Take(Math.Clamp(count, 1, 20))
            .ToList();
        var table = new ChatTable("Sales by city", ["City", "Shipments", "Total"],
            rows.Select(r => (IReadOnlyList<string>)new[] { r.City, r.Shipments.ToString("N0", CultureInfo.InvariantCulture), r.Total.ToString("N2", CultureInfo.InvariantCulture) }).ToList());
        var text = rows.Count == 0 ? "There are no shipments." : $"{rows[0].City} leads with {rows[0].Total.ToString("N2", CultureInfo.InvariantCulture)} across {rows[0].Shipments} shipments.";
        return (text, table);
    }

    /// <summary>
    /// The next month's total, from a straight line fitted to the last twelve monthly totals (least squares). The table shows the
    /// last six months and the forecast. The input is a city, or "all".
    /// </summary>
    public static (string Text, ChatTable Table) Forecast(string? city)
    {
        var shipments = Shipments.Query.ToList();
        var label = "all cities";
        if (!string.IsNullOrWhiteSpace(city) && !city.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            var match = Shipments.Cities.FirstOrDefault(c => c.Equals(city.Trim(), StringComparison.OrdinalIgnoreCase));
            if (match is null) return ($"There are no shipments for '{city}'.", new ChatTable("Forecast", ["Month", "Total"], []));
            shipments = shipments.Where(s => s.City == match).ToList();
            label = match;
        }

        var monthly = shipments.GroupBy(s => new DateOnly(s.Date.Year, s.Date.Month, 1))
            .OrderBy(g => g.Key)
            .Select(g => (Month: g.Key, Total: g.Sum(s => s.Amount)))
            .ToList();
        var window = monthly.TakeLast(12).ToList();
        if (window.Count < 2) return ("Not enough months of sales to forecast.", new ChatTable("Forecast", ["Month", "Total"], []));

        var (slope, intercept) = LeastSquares(window.Select(w => (double)w.Total).ToArray());
        var next = window[^1].Month.AddMonths(1);
        var forecast = (decimal)(intercept + slope * window.Count);
        var rows = window.TakeLast(6).Select(w => (IReadOnlyList<string>)new[] { w.Month.ToString("yyyy-MM"), w.Total.ToString("N2", CultureInfo.InvariantCulture) }).ToList();
        rows.Add(new[] { next.ToString("yyyy-MM") + " (forecast)", forecast.ToString("N2", CultureInfo.InvariantCulture) });
        var table = new ChatTable($"Monthly sales, {label}", ["Month", "Total"], rows);
        var text = $"Forecast for {next:yyyy-MM} ({label}): {forecast.ToString("N2", CultureInfo.InvariantCulture)}. The trend over the last {window.Count} months is {(slope >= 0 ? "+" : "")}{slope.ToString("N2", CultureInfo.InvariantCulture)} a month.";
        return (text, table);
    }

    private static (double Slope, double Intercept) LeastSquares(double[] y)
    {
        var n = y.Length;
        var meanX = (n - 1) / 2.0;
        var meanY = y.Average();
        var cov = 0.0;
        var varX = 0.0;
        for (var i = 0; i < n; i++)
        {
            cov += (i - meanX) * (y[i] - meanY);
            varX += (i - meanX) * (i - meanX);
        }
        var slope = varX == 0 ? 0 : cov / varX;
        return (slope, meanY - slope * meanX);
    }
}
