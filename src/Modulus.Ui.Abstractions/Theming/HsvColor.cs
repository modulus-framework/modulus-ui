using System.Globalization;

namespace Modulus.Ui.Abstractions.Theming;

/// <summary>
/// A colour as hue (0–360), saturation, value and alpha (0–1): the model of the colour picker panel.
/// Shared by the Blazor <c>AppColorPicker</c>, the MVC <c>app-color-picker</c> and modulusColorPicker in
/// modulus-widgets.js, which mirrors <see cref="ToHex"/> and <see cref="TryParse"/>.
/// </summary>
public readonly record struct HsvColor(double H, double S, double V, double A = 1)
{
    public static HsvColor FromRgb(int r, int g, int b, double a = 1)
    {
        double rf = r / 255.0, gf = g / 255.0, bf = b / 255.0;
        var max = Math.Max(rf, Math.Max(gf, bf));
        var min = Math.Min(rf, Math.Min(gf, bf));
        var d = max - min;
        double h = 0;
        if (d > 0)
        {
            if (max == rf) h = (gf - bf) / d % 6;
            else if (max == gf) h = (bf - rf) / d + 2;
            else h = (rf - gf) / d + 4;
            h *= 60;
            if (h < 0) h += 360;
        }
        return new HsvColor(h, max == 0 ? 0 : d / max, max, a);
    }

    public (int R, int G, int B) ToRgb()
    {
        var c = V * S;
        var x = c * (1 - Math.Abs(H / 60 % 2 - 1));
        var m = V - c;
        var (r, g, b) = (H % 360) switch
        {
            < 60 => (c, x, 0d),
            < 120 => (x, c, 0d),
            < 180 => (0d, c, x),
            < 240 => (0d, x, c),
            < 300 => (x, 0d, c),
            _ => (c, 0d, x),
        };
        return (Byte(r + m), Byte(g + m), Byte(b + m));
    }

    /// <summary>#rrggbb, or #rrggbbaa when the colour is translucent.</summary>
    public string ToHex()
    {
        var (r, g, b) = ToRgb();
        var hex = $"#{r:x2}{g:x2}{b:x2}";
        return A >= 1 ? hex : hex + Byte(A).ToString("x2", CultureInfo.InvariantCulture);
    }

    /// <summary>rgb(r, g, b) or rgba(r, g, b, a).</summary>
    public string ToRgbString()
    {
        var (r, g, b) = ToRgb();
        return A >= 1 ? $"rgb({r}, {g}, {b})" : $"rgba({r}, {g}, {b}, {Math.Round(A, 2).ToString(CultureInfo.InvariantCulture)})";
    }

    /// <summary>The same hue at full saturation and value: the panel's background.</summary>
    public string HueHex() => (this with { S = 1, V = 1, A = 1 }).ToHex();

    public string Format(string? format) => format == "rgb" ? ToRgbString() : ToHex();

    /// <summary>Parses #rgb, #rgba, #rrggbb, #rrggbbaa, rgb() and rgba().</summary>
    public static bool TryParse(string? value, out HsvColor color)
    {
        color = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        var s = value.Trim();
        if (s.StartsWith('#')) s = s[1..];
        else if (s.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
        {
            var open = s.IndexOf('(');
            var close = s.IndexOf(')');
            if (open < 0 || close < open) return false;
            var parts = s[(open + 1)..close].Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length is < 3 or > 4) return false;
            if (!int.TryParse(parts[0], out var r) || !int.TryParse(parts[1], out var g) || !int.TryParse(parts[2], out var b)) return false;
            var a = 1d;
            if (parts.Length == 4 && !double.TryParse(parts[3], NumberStyles.Float, CultureInfo.InvariantCulture, out a)) return false;
            if (r is < 0 or > 255 || g is < 0 or > 255 || b is < 0 or > 255) return false;
            color = FromRgb(r, g, b, Math.Clamp(a, 0, 1));
            return true;
        }

        if (s.Length is 3 or 4) s = string.Concat(s.Select(ch => new string(ch, 2)));
        if (s.Length is not (6 or 8) || !s.All(Uri.IsHexDigit)) return false;
        int Hex(int i) => int.Parse(s.AsSpan(i, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        color = FromRgb(Hex(0), Hex(2), Hex(4), s.Length == 8 ? Hex(6) / 255.0 : 1);
        return true;
    }

    public static HsvColor Parse(string? value, string fallback = "#1677ff") =>
        TryParse(value, out var c) ? c : TryParse(fallback, out c) ? c : new HsvColor(215, 0.91, 1);

    private static int Byte(double v) => (int)Math.Round(Math.Clamp(v, 0, 1) * 255);
}

/// <summary>A labelled row of swatches in the colour picker.</summary>
public sealed record ColorPreset(string Label, IReadOnlyList<string> Colors)
{
    /// <summary>Ant Design's primary palette, 6th shade of each hue.</summary>
    public static readonly ColorPreset Recommended = new("Recommended",
    [
        "#f5222d", "#fa541c", "#fa8c16", "#faad14", "#fadb14", "#a0d911", "#52c41a", "#13c2c2",
        "#1677ff", "#2f54eb", "#722ed1", "#eb2f96", "#000000", "#595959", "#bfbfbf", "#ffffff",
    ]);
}
