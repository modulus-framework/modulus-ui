namespace Modulus.Ui.Abstractions.Theming;

/// <summary>
/// The brand colour in dark mode. Ant Design's dark stylesheet hard-codes its primary blue (<c>#177ddc</c>) and the shades
/// made from it; <c>antd-dark-brand.css</c> repeats those declarations with each shade as a <c>--m-adp-&lt;hex&gt;</c> variable,
/// and <see cref="Variables"/> gives the variables their brand values.
/// </summary>
public static class AntDarkBrand
{
    /// <summary>The blue shades of the dark stylesheet that are swapped for the brand (lower-case hex, no <c>#</c>).</summary>
    public static readonly IReadOnlyList<string> Sources =
    [
        "06213a", "095cb5", "0e161f", "0e4980", "111b26", "111d2c", "11263c", "153450", "15395b",
        "16436e", "165996", "177ddc", "1890ff", "388ed3", "3c9ae8", "3c9be8", "4697e3",
    ];

    private static readonly AntColor Background = AntColor.Parse("#141414");
    private static readonly AntColor Reference = AntColor.Parse("#177ddc");

    /// <summary>The dark primary of a brand colour: antd's dark palette mixes it into the page background at 85%.</summary>
    public static AntColor DarkPrimary(AntColor primary) => Background.Mix(primary, 85).Rounded();

    /// <summary>
    /// One source shade moved to the brand: the hue turns by as much as the brand's dark primary differs from antd's,
    /// saturation scales the same way, and lightness keeps its place between black, the primary and white.
    /// </summary>
    public static AntColor Recolor(AntColor source, AntColor primary)
    {
        var (hs, ss, ls) = source.ToHsl();
        var (hr, sr, lr) = Reference.ToHsl();
        var (ht, st, lt) = DarkPrimary(primary).ToHsl();
        var h = ((ht + hs - hr) % 1 + 1) % 1;
        var s = sr == 0 ? st : Math.Clamp(ss * st / sr, 0, 1);
        var l = ls <= lr ? ls * lt / lr : lt + (ls - lr) * (1 - lt) / (1 - lr);
        return AntColor.FromHsl(h, s, Math.Clamp(l, 0, 1), 1).Rounded();
    }

    /// <summary>The <c>--m-adp-*</c> variables for a brand colour.</summary>
    public static IEnumerable<(string Name, string Value)> Variables(AntColor primary) =>
        Sources.Select(hex => ($"--m-adp-{hex}", Recolor(AntColor.Parse("#" + hex), primary).ToHex()));
}
