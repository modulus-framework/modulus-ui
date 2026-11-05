using System.Globalization;
using Modulus.Ui.Abstractions.Chat;
using Modulus.Ui.Abstractions.Forecast;

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
    /// The next month's total, from Holt-Winters smoothing of every month of sales (a trend and a yearly season, once two seasons are
    /// in). The text gives the error rate measured on the last six months. The table shows the last six months and the forecast.
    /// The input is a city, or "all".
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
        if (monthly.Count < HoltWinters.MinimumPoints) return ("Not enough months of sales to forecast.", new ChatTable("Forecast", ["Month", "Total"], []));

        var forecast = HoltWinters.Next(monthly.Select(m => (double)m.Total).ToList());
        var next = monthly[^1].Month.AddMonths(1);
        var value = (decimal)forecast.Next;
        var rows = monthly.TakeLast(6).Select(w => (IReadOnlyList<string>)new[] { w.Month.ToString("yyyy-MM"), w.Total.ToString("N2", CultureInfo.InvariantCulture) }).ToList();
        rows.Add(new[] { next.ToString("yyyy-MM") + " (forecast)", value.ToString("N2", CultureInfo.InvariantCulture) });
        var table = new ChatTable($"Monthly sales, {label}", ["Month", "Total"], rows);
        var method = forecast.Seasonal ? "trend and yearly season" : "trend only (not two full years yet)";
        var text = $"Forecast for {next:yyyy-MM} ({label}): {value.ToString("N2", CultureInfo.InvariantCulture)}, from a {method} model. " +
            $"Its error on the last six months: {forecast.ErrorRate:P1}.";
        return (text, table);
    }
}
