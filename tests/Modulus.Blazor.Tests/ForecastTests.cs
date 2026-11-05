using Modulus.Ui.Abstractions.Forecast;
using Xunit;

namespace Modulus.Blazor.Tests;

public class HoltWintersTests
{
    private static readonly double[] Season = [10, 20, 5, -5, 0, 15, 25, 10, -10, 5, 0, 8];

    [Fact]
    public void ASeasonalTrend_IsForecastNearlyExactly()
    {
        // Three years of a rising line with a repeating season.
        var series = Enumerable.Range(0, 36).Select(t => 100.0 + 2.0 * t + Season[t % 12]).ToList();
        var forecast = HoltWinters.Next(series);

        var expected = 100.0 + 2.0 * 36 + Season[36 % 12];
        Assert.True(forecast.Seasonal);
        Assert.True(Math.Abs(forecast.Next - expected) / expected < 0.02, $"forecast {forecast.Next:0.0}, expected about {expected}");
        Assert.True(forecast.ErrorRate < 0.05);
    }

    [Fact]
    public void AFlatSeries_IsForecastFlat()
    {
        var forecast = HoltWinters.Next(Enumerable.Repeat(50.0, 10).ToList());
        Assert.False(forecast.Seasonal);
        Assert.Equal(50.0, forecast.Next, 6);
        Assert.Equal(0, forecast.ErrorRate, 6);
    }

    [Fact]
    public void AShortSeries_UsesTheTrendOnly()
    {
        var forecast = HoltWinters.Next([10, 12, 14, 16, 18, 20]);
        Assert.False(forecast.Seasonal);
        Assert.True(forecast.Next > 20);
    }

    [Fact]
    public void TooFewPoints_IsRefused()
    {
        Assert.Throws<ArgumentException>(() => HoltWinters.Next([1, 2]));
    }
}
