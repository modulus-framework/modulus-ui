using System.Globalization;
using System.Text;

namespace Modulus.Ui.Abstractions.Charts;

public readonly record struct ChartPoint(string Label, double Value);

/// <summary>
/// Geometry for the dependency-free Modulus charts. Blazor components and MVC tag helpers
/// both render from this, so the two flavors draw identical charts. Bars are HTML (see
/// modulus.css .m-bars); only sparkline paths are SVG. Hover tooltips are pure CSS, so
/// they work without JavaScript in every flavor.
/// </summary>
public static class ChartGeometry
{
    /// <summary>
    /// Line and closed-area paths for a sparkline in a <paramref name="width"/>×<paramref name="height"/> box.
    /// Point i sits at the centre of slot i, so equal-width HTML hover cells line up with the data.
    /// </summary>
    public static (string Line, string Area) Sparkline(IReadOnlyList<double> values, double width, double height, double padTop = 2)
    {
        if (values.Count == 0) return ("", "");
        var max = values.Max();
        var min = Math.Min(0, values.Min());
        var range = max - min == 0 ? 1 : max - min;
        var slot = width / values.Count;

        var line = new StringBuilder();
        for (var i = 0; i < values.Count; i++)
        {
            var x = (i + 0.5) * slot;
            var y = padTop + (height - padTop) * (1 - (values[i] - min) / range);
            line.Append(i == 0 ? 'M' : 'L').Append(F(x)).Append(',').Append(F(y));
        }
        var area = $"{line}L{F((values.Count - 0.5) * slot)},{F(height)}L{F(slot / 2)},{F(height)}Z";
        return (line.ToString(), area);
    }

    /// <summary>"Nice" axis ticks (1/2/5 × 10^n) from zero to at least <paramref name="max"/>.</summary>
    public static IReadOnlyList<double> NiceTicks(double max, int targetCount = 5)
    {
        if (max <= 0) return [0];
        var rough = max / targetCount;
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var residual = rough / magnitude;
        var step = (residual > 5 ? 10 : residual > 2 ? 5 : residual > 1 ? 2 : 1) * magnitude;
        var ticks = new List<double>();
        for (var v = 0d; v < max + step * 0.999; v += step) ticks.Add(v);
        return ticks;
    }

    /// <summary>Compact axis label: 1200 → 1.2k, 3400000 → 3.4M.</summary>
    public static string Compact(double value) => value switch
    {
        >= 1_000_000 => (value / 1_000_000).ToString("0.#", CultureInfo.InvariantCulture) + "M",
        >= 1_000 => (value / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "k",
        _ => value.ToString("0.##", CultureInfo.InvariantCulture),
    };

    /// <summary>Bar height as a percentage of <paramref name="max"/>, for inline <c>height:%</c> styles.</summary>
    public static string Percent(double value, double max) => F(max <= 0 ? 0 : Math.Clamp(value / max * 100, 0, 100));

    public static string F(double v) => Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);
}
