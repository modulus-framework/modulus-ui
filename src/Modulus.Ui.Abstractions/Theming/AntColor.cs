using System.Globalization;

namespace Modulus.Ui.Abstractions.Theming;

/// <summary>
/// Minimal RGBA colour with the handful of TinyColor operations antd's theme
/// generator relies on (lighten, darken, tint, alpha, HSV conversion).
/// </summary>
public readonly record struct AntColor(double R, double G, double B, double A = 1)
{
    public static AntColor White => new(255, 255, 255);

    public static AntColor Parse(string value)
    {
        var hex = value.Trim().TrimStart('#');
        if (hex.Length == 3)
            hex = string.Concat(hex.Select(c => new string(c, 2)));
        if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var rgb))
            throw new FormatException($"'{value}' is not a #rgb or #rrggbb colour.");
        return new AntColor((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);
    }

    public static bool TryParse(string? value, out AntColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        try { color = Parse(value); return true; }
        catch (FormatException) { return false; }
    }

    public AntColor Rounded() => new(Byte(R), Byte(G), Byte(B), A);

    public AntColor WithAlpha(double alpha) => this with { A = alpha };

    /// <summary>Mix towards white by <paramref name="amount"/> percent (TinyColor.tint).</summary>
    public AntColor Tint(double amount) => Mix(White, amount);

    /// <summary>Mix towards <paramref name="other"/> by <paramref name="amount"/> percent (TinyColor.mix).</summary>
    public AntColor Mix(AntColor other, double amount)
    {
        var p = amount / 100;
        return new AntColor(
            (other.R - R) * p + R,
            (other.G - G) * p + G,
            (other.B - B) * p + B,
            (other.A - A) * p + A);
    }

    public AntColor Lighten(double amount) => AdjustLightness(amount / 100);

    public AntColor Darken(double amount) => AdjustLightness(-amount / 100);

    private AntColor AdjustLightness(double delta)
    {
        var (h, s, l) = ToHsl();
        return FromHsl(h, s, Math.Clamp(l + delta, 0, 1), A);
    }

    public (double H, double S, double V) ToHsv()
    {
        double r = R / 255, g = G / 255, b = B / 255;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double d = max - min;
        double h = 0;
        if (d != 0)
        {
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h /= 6;
        }
        return (h * 360, max == 0 ? 0 : d / max, max);
    }

    public static AntColor FromHsv(double h, double s, double v)
    {
        h = h / 360 * 6;
        var i = Math.Floor(h);
        var f = h - i;
        double p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
        var (r, g, b) = ((int)i % 6) switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
        return new AntColor(r * 255, g * 255, b * 255);
    }

    private (double H, double S, double L) ToHsl()
    {
        double r = R / 255, g = G / 255, b = B / 255;
        double max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) / 2, h = 0, s = 0;
        if (max != min)
        {
            var d = max - min;
            s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            if (max == r) h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g) h = (b - r) / d + 2;
            else h = (r - g) / d + 4;
            h /= 6;
        }
        return (h, s, l);
    }

    private static AntColor FromHsl(double h, double s, double l, double a)
    {
        if (s == 0) return new AntColor(l * 255, l * 255, l * 255, a);
        static double Hue(double p, double q, double t)
        {
            if (t < 0) t += 1;
            if (t > 1) t -= 1;
            if (t < 1.0 / 6) return p + (q - p) * 6 * t;
            if (t < 1.0 / 2) return q;
            if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
            return p;
        }
        var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
        var p = 2 * l - q;
        return new AntColor(Hue(p, q, h + 1.0 / 3) * 255, Hue(p, q, h) * 255, Hue(p, q, h - 1.0 / 3) * 255, a);
    }

    public string ToHex() => $"#{Byte(R):x2}{Byte(G):x2}{Byte(B):x2}";

    /// <summary>Hex when opaque, <c>rgba()</c> otherwise — matches TinyColor.toRgbString/toHexString output.</summary>
    public override string ToString() =>
        A >= 1
            ? ToHex()
            : string.Create(CultureInfo.InvariantCulture, $"rgba({Byte(R)}, {Byte(G)}, {Byte(B)}, {Math.Round(A, 2)})");

    private static int Byte(double channel) => (int)Math.Round(Math.Clamp(channel, 0, 255), MidpointRounding.AwayFromZero);
}
