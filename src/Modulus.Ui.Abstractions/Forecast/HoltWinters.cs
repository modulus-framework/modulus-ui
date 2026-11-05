namespace Modulus.Ui.Abstractions.Forecast;

/// <summary>The next value of a series, and how far off the model was on the last few points.</summary>
/// <param name="Next">The forecast for the next period.</param>
/// <param name="ErrorRate">The mean absolute percentage error of one-step forecasts over the last six points (0 when they cannot be measured).</param>
/// <param name="Seasonal">True when the series was long enough for a seasonal model (two full seasons); otherwise a trend-only model.</param>
public sealed record SeriesForecast(double Next, double ErrorRate, bool Seasonal);

/// <summary>
/// Holt-Winters exponential smoothing (additive): a level, a trend and, with two full seasons of data, a season. The smoothing
/// constants are fixed (alpha 0.3, beta 0.05, gamma 0.3): they suit monthly sales. The error rate is measured by forecasting each of the last
/// six points from the points before it, so the accuracy is shown rather than assumed.
/// </summary>
public static class HoltWinters
{
    public const double Alpha = 0.3;
    public const double Beta = 0.05;
    public const double Gamma = 0.3;

    /// <summary>The series needs at least this many points.</summary>
    public const int MinimumPoints = 3;

    public static SeriesForecast Next(IReadOnlyList<double> series, int season = 12)
    {
        var n = series.Count;
        if (n < MinimumPoints) throw new ArgumentException($"A forecast needs at least {MinimumPoints} points.", nameof(series));
        var seasonal = n >= 2 * season;
        var m = seasonal ? season : 1;

        double level, trend;
        var s = new double[m];
        if (seasonal)
        {
            level = series.Take(m).Average();
            trend = (series.Skip(m).Take(m).Average() - level) / m;
            // The first season's level is at its centre, so each point is compared with the level that point would have on the trend.
            for (var i = 0; i < m; i++) s[i] = series[i] - (level + trend * (i - (m - 1) / 2.0));
        }
        else
        {
            level = series[0];
            trend = series[1] - series[0];
        }

        var errors = new List<double>();
        for (var t = 1; t < n; t++)
        {
            var slot = t % m;
            var seasonalPrevious = seasonal ? s[slot] : 0;
            var predicted = level + trend + seasonalPrevious;
            if (t >= n - 6 && series[t] != 0) errors.Add(Math.Abs(series[t] - predicted) / Math.Abs(series[t]));

            var newLevel = Alpha * (series[t] - seasonalPrevious) + (1 - Alpha) * (level + trend);
            trend = Beta * (newLevel - level) + (1 - Beta) * trend;
            if (seasonal) s[slot] = Gamma * (series[t] - newLevel) + (1 - Gamma) * seasonalPrevious;
            level = newLevel;
        }

        var next = level + trend + (seasonal ? s[n % m] : 0);
        return new SeriesForecast(next, errors.Count == 0 ? 0 : errors.Average(), seasonal);
    }
}
