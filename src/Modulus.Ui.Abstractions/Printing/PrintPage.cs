using System.Globalization;

namespace Modulus.Ui.Abstractions.Printing;

/// <summary>The paper a print view is set for (its <c>@page size</c>).</summary>
public enum PaperSize
{
    /// <summary>210 × 297 mm.</summary>
    A4,

    /// <summary>8.5 × 11 in.</summary>
    Letter,
}

/// <summary>How a print view's pages are set: the paper, its orientation and the margin all round (in millimetres, 0 to 50).</summary>
public sealed record PrintOptions(PaperSize Paper = PaperSize.A4, bool Landscape = false, int MarginMm = 12);

/// <summary>The print CSS a print view puts in its page: the <c>@page</c> rule for its paper.</summary>
public static class PrintCss
{
    /// <summary>The <c>@page</c> rule, e.g. <c>@page { size: A4 landscape; margin: 12mm; }</c>.</summary>
    public static string Page(PrintOptions options)
    {
        if (options.MarginMm is < 0 or > 50) throw new ArgumentOutOfRangeException(nameof(options), "A page margin is 0 to 50 mm.");
        var size = options.Paper == PaperSize.A4 ? "A4" : "letter";
        var orientation = options.Landscape ? " landscape" : "";
        var margin = options.MarginMm.ToString(CultureInfo.InvariantCulture) + "mm";
        return $"@page {{ size: {size}{orientation}; margin: {margin}; }}";
    }
}
