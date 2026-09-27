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
        var step = NiceStep(max / targetCount);
        var ticks = new List<double>();
        for (var v = 0d; v < max + step * 0.999; v += step) ticks.Add(v);
        return ticks;
    }

    /// <summary>
    /// "Nice" ticks spanning <paramref name="min"/>…<paramref name="max"/>, always including zero, for
    /// axes that can go negative (waterfall, variance). Equals <see cref="NiceTicks"/> when min ≥ 0.
    /// </summary>
    public static IReadOnlyList<double> NiceRange(double min, double max, int targetCount = 5)
    {
        min = Math.Min(0, min);
        max = Math.Max(0, max);
        if (min == 0) return NiceTicks(max, targetCount);
        var step = NiceStep((max - min) / targetCount);
        var ticks = new List<double>();
        // Integer steps from zero so the zero tick is exact and never drifts.
        for (var k = (int)Math.Floor(min / step + 1e-9); k * step < max + step * 0.999; k++) ticks.Add(Math.Round(k * step, 10));
        return ticks;
    }

    private static double NiceStep(double rough)
    {
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(rough)));
        var residual = rough / magnitude;
        return (residual > 5 ? 10 : residual > 2 ? 5 : residual > 1 ? 2 : 1) * magnitude;
    }

    /// <summary>
    /// Squarified treemap (Bruls, Huizing, van Wijk): lays <paramref name="values"/> (sorted descending)
    /// into a <paramref name="width"/>×<paramref name="height"/> box, keeping tiles close to square.
    /// Returns one rectangle per value, in input order.
    /// </summary>
    public static IReadOnlyList<(double X, double Y, double W, double H)> Squarify(IReadOnlyList<double> values, double width, double height)
    {
        var result = new (double X, double Y, double W, double H)[values.Count];
        var total = values.Sum(v => Math.Max(0, v));
        if (total <= 0 || width <= 0 || height <= 0) return result;
        var scale = width * height / total;
        var areas = values.Select(v => Math.Max(0, v) * scale).ToArray();

        double x = 0, y = 0, w = width, h = height;
        var start = 0;
        while (start < areas.Length)
        {
            var side = Math.Min(w, h);
            var end = start + 1;
            var rowSum = areas[start];
            var worst = Worst(areas, start, end, rowSum, side);
            // Grow the row while it keeps the worst aspect ratio from getting worse.
            while (end < areas.Length)
            {
                var next = Worst(areas, start, end + 1, rowSum + areas[end], side);
                if (next > worst) break;
                worst = next;
                rowSum += areas[end];
                end++;
            }
            var thickness = rowSum / side;
            var offset = 0d;
            for (var i = start; i < end; i++)
            {
                var length = thickness == 0 ? 0 : areas[i] / thickness;
                result[i] = w >= h ? (x, y + offset, thickness, length) : (x + offset, y, length, thickness);
                offset += length;
            }
            if (w >= h) { x += thickness; w -= thickness; }
            else { y += thickness; h -= thickness; }
            start = end;
        }
        return result;

        static double Worst(double[] a, int from, int to, double sum, double side)
        {
            if (sum <= 0) return double.MaxValue;
            double max = 0, min = double.MaxValue;
            for (var i = from; i < to; i++) { max = Math.Max(max, a[i]); min = Math.Min(min, a[i]); }
            if (min <= 0) return double.MaxValue;
            var s2 = side * side;
            var sum2 = sum * sum;
            return Math.Max(s2 * max / sum2, sum2 / (s2 * min));
        }
    }

    /// <summary>Compact axis label: 1200 → 1.2k, 3400000 → 3.4M.</summary>
    public static string Compact(double value) => value switch
    {
        < 0 => "-" + Compact(-value),
        >= 1_000_000 => (value / 1_000_000).ToString("0.#", CultureInfo.InvariantCulture) + "M",
        >= 1_000 => (value / 1_000).ToString("0.#", CultureInfo.InvariantCulture) + "k",
        _ => value.ToString("0.##", CultureInfo.InvariantCulture),
    };

    /// <summary>Bar height as a percentage of <paramref name="max"/>, for inline <c>height:%</c> styles.</summary>
    public static string Percent(double value, double max) => F(max <= 0 ? 0 : Math.Clamp(value / max * 100, 0, 100));

    public static string F(double v) => Math.Round(v, 2).ToString(CultureInfo.InvariantCulture);
}
