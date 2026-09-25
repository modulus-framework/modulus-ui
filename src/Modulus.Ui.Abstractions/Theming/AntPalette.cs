namespace Modulus.Ui.Abstractions.Theming;

/// <summary>
/// Port of <c>@ant-design/colors</c> <c>generate()</c>: derives the 10-step
/// Ant Design palette (index 5 is the seed) from a single brand colour.
/// Steps are rounded to whole RGB channels, as the JS version returns hex strings.
/// </summary>
public static class AntPalette
{
    private const int HueStep = 2;
    private const double SaturationStep = 0.16;
    private const double SaturationStep2 = 0.05;
    private const double BrightnessStep1 = 0.05;
    private const double BrightnessStep2 = 0.15;
    private const int LightColorCount = 5;
    private const int DarkColorCount = 4;

    public static IReadOnlyList<AntColor> Generate(AntColor seed)
    {
        var hsv = seed.ToHsv();
        var colors = new List<AntColor>(10);
        for (var i = LightColorCount; i > 0; i--)
            colors.Add(AntColor.FromHsv(Hue(hsv, i, true), Saturation(hsv, i, true), Value(hsv, i, true)).Rounded());
        colors.Add(seed);
        for (var i = 1; i <= DarkColorCount; i++)
            colors.Add(AntColor.FromHsv(Hue(hsv, i, false), Saturation(hsv, i, false), Value(hsv, i, false)).Rounded());
        return colors;
    }

    private static double Hue((double H, double S, double V) hsv, int i, bool light)
    {
        var h = Math.Round(hsv.H);
        double hue = h >= 60 && h <= 240
            ? light ? h - HueStep * i : h + HueStep * i
            : light ? h + HueStep * i : h - HueStep * i;
        if (hue < 0) hue += 360;
        else if (hue >= 360) hue -= 360;
        return hue;
    }

    private static double Saturation((double H, double S, double V) hsv, int i, bool light)
    {
        if (hsv.H == 0 && hsv.S == 0) return hsv.S;
        double s = light
            ? hsv.S - SaturationStep * i
            : i == DarkColorCount ? hsv.S + SaturationStep : hsv.S + SaturationStep2 * i;
        if (s > 1) s = 1;
        if (light && i == LightColorCount && s > 0.1) s = 0.1;
        if (s < 0.06) s = 0.06;
        return Math.Round(s, 2, MidpointRounding.AwayFromZero);
    }

    private static double Value((double H, double S, double V) hsv, int i, bool light)
    {
        var v = light ? hsv.V + BrightnessStep1 * i : hsv.V - BrightnessStep2 * i;
        return Math.Round(Math.Min(v, 1), 2, MidpointRounding.AwayFromZero);
    }
}
