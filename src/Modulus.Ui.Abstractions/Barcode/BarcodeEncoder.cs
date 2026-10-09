using System.Net;
using System.Text;

namespace Modulus.Ui.Abstractions.Barcode;

/// <summary>The one-dimensional symbologies Modulus draws: Code 128 (any printable ASCII; digits in pairs when all digits) and EAN-13 (a product number).</summary>
public enum BarcodeKind { Code128, Ean13 }

/// <summary>
/// The barcode encoder: the bars and spaces of a value, as modules (<c>true</c> is a bar). Code 128 uses set B, or set C
/// (digit pairs) when the value is an even run of digits, with a mod-103 check character. EAN-13 takes 12 digits (the check
/// digit is added) or 13 (checked). Both come out with the standard quiet zone of 10 modules each side, which the renderer adds.
/// </summary>
public static class BarcodeEncoder
{
    /// <summary>Modules of empty space each side of a symbol.</summary>
    public const int QuietZone = 10;

    /// <summary>The longest Code 128 value.</summary>
    public const int MaxCode128Length = 80;

    // Code 128 bar and space widths, bar first, for symbol values 0 to 102 (generated from ZXing.Net's Code128Writer, which
    // checks each symbol is 11 modules and all 103 are distinct). The start codes and the stop code are below.
    private static readonly string[] Code128Patterns =
    [
        "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
        "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
        "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
        "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
        "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
        "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
        "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
        "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
        "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
        "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
        "114131", "311141", "411131",
    ];

    private const int StartB = 104, StartC = 105, Stop = 106;
    private const string StartBPattern = "211214", StartCPattern = "211232", StopPattern = "2331112";

    /// <summary>Why a value cannot be drawn as the kind, or null when it can.</summary>
    public static string? Problem(BarcodeKind kind, string? value)
    {
        value ??= "";
        switch (kind)
        {
            case BarcodeKind.Code128:
                if (value.Length == 0) return "Give the barcode a value.";
                if (value.Length > MaxCode128Length) return $"Use {MaxCode128Length} characters or fewer.";
                return value.Any(c => c < 32 || c > 126) ? "Code 128 takes printable ASCII characters only." : null;
            default:
                if (value.Length is not (12 or 13) || !value.All(char.IsAsciiDigit)) return "EAN-13 takes 12 digits, or 13 with the check digit.";
                return value.Length == 13 && CheckDigit(value[..12]) != value[12] - '0' ? "The EAN-13 check digit is wrong." : null;
        }
    }

    /// <summary>The value as it is printed under the bars: an EAN-13 with its check digit, a Code 128 value as it is.</summary>
    public static string Text(BarcodeKind kind, string value) =>
        kind == BarcodeKind.Ean13 && value.Length == 12 ? value + CheckDigit(value) : value;

    /// <summary>The modules of a value, bar (<c>true</c>) or space (<c>false</c>), without the quiet zone.</summary>
    /// <exception cref="ArgumentException">The value is not one the kind can draw (see <see cref="Problem"/>).</exception>
    public static IReadOnlyList<bool> Modules(BarcodeKind kind, string value)
    {
        if (Problem(kind, value) is { } problem) throw new ArgumentException(problem, nameof(value));
        return kind == BarcodeKind.Ean13 ? Ean13(Text(kind, value)) : Code128(value);
    }

    /// <summary>The check digit that completes twelve EAN-13 digits.</summary>
    public static int CheckDigit(string twelve)
    {
        var sum = 0;
        for (var i = 0; i < 12; i++) sum += (twelve[i] - '0') * (i % 2 == 0 ? 1 : 3);
        return (10 - sum % 10) % 10;
    }

    private static IReadOnlyList<bool> Code128(string value)
    {
        var digits = value.Length >= 2 && value.Length % 2 == 0 && value.All(char.IsAsciiDigit);
        var symbols = new List<int> { digits ? StartC : StartB };
        if (digits)
            for (var i = 0; i < value.Length; i += 2) symbols.Add((value[i] - '0') * 10 + (value[i + 1] - '0'));
        else
            foreach (var c in value) symbols.Add(c - 32);

        var sum = symbols[0];
        for (var i = 1; i < symbols.Count; i++) sum += symbols[i] * i;
        symbols.Add(sum % 103);
        symbols.Add(Stop);

        var modules = new List<bool>();
        for (var s = 0; s < symbols.Count; s++)
            Add(modules, s == symbols.Count - 1 ? StopPattern : Pattern(symbols[s]));
        return modules;
    }

    private static string Pattern(int symbol) => symbol switch
    {
        StartB => StartBPattern,
        StartC => StartCPattern,
        _ => Code128Patterns[symbol],
    };

    private static void Add(List<bool> modules, string widths)
    {
        var bar = true;
        foreach (var w in widths)
        {
            for (var i = 0; i < w - '0'; i++) modules.Add(bar);
            bar = !bar;
        }
    }

    private static readonly string[] Left =
    [
        "0001101", "0011001", "0010011", "0111101", "0100011", "0110001", "0101111", "0111011", "0110111", "0001011",
    ];

    private static readonly string[] Right =
    [
        "1110010", "1100110", "1101100", "1000010", "1011100", "1001110", "1010000", "1000100", "1001000", "1110100",
    ];

    // Which digits of the left half use the G pattern (the rest use L), by the first digit.
    private static readonly string[] Parity =
    [
        "LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG", "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL",
    ];

    private static readonly string[] Gcode =
    [
        "0100111", "0110011", "0011011", "0100001", "0011101", "0111001", "0000101", "0010001", "0001001", "0010111",
    ];

    private static IReadOnlyList<bool> Ean13(string thirteen)
    {
        var modules = new List<bool>();
        void Bits(string bits)
        {
            foreach (var b in bits) modules.Add(b == '1');
        }

        Bits("101");
        var parity = Parity[thirteen[0] - '0'];
        for (var i = 1; i <= 6; i++)
        {
            var d = thirteen[i] - '0';
            Bits(parity[i - 1] == 'L' ? Left[d] : Gcode[d]);
        }
        Bits("01010");
        for (var i = 7; i <= 12; i++) Bits(Right[thirteen[i] - '0']);
        Bits("101");
        return modules;
    }
}

/// <summary>
/// A barcode as inline SVG: one rectangle per run of bars, a quiet zone each side, and the value printed under the bars.
/// The Blazor <c>AppBarcode</c> and the MVC <c>app-barcode</c> both draw this.
/// </summary>
public static class BarcodeSvg
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>
    /// The SVG for a value. <paramref name="barHeight"/> and <paramref name="moduleWidth"/> are pixels. A value the kind cannot draw
    /// gets a message in its place (see <see cref="BarcodeEncoder.Problem"/>) rather than a symbol.
    /// </summary>
    public static string Render(BarcodeKind kind, string value, int barHeight = 80, int moduleWidth = 2, bool showText = true,
        string color = "#000", string background = "#fff", string? label = null, string? cssClass = null)
    {
        var css = "m-barcode" + (cssClass is { Length: > 0 } c ? " " + E(c) : "");
        if (BarcodeEncoder.Problem(kind, value) is { } problem)
            return $"""<span class="{css} m-barcode-error" role="alert">{E(problem)}</span>""";

        var modules = BarcodeEncoder.Modules(kind, value);
        var total = modules.Count + BarcodeEncoder.QuietZone * 2;
        var text = showText ? BarcodeEncoder.Text(kind, value) : null;
        var textHeight = text is null ? 0 : 18;
        var width = total * moduleWidth;
        var height = barHeight + textHeight;
        var aria = E(label ?? (kind == BarcodeKind.Ean13 ? "EAN-13 " : "Code 128 ") + BarcodeEncoder.Text(kind, value));

        var sb = new StringBuilder();
        sb.Append($"""<svg class="{css}" role="img" aria-label="{aria}" width="{width}" height="{height}" viewBox="0 0 {width} {height}" shape-rendering="crispEdges" xmlns="http://www.w3.org/2000/svg">""");
        if (background != "transparent") sb.Append($"""<rect width="{width}" height="{height}" fill="{E(background)}"/>""");
        var run = -1;
        for (var i = 0; i <= modules.Count; i++)
        {
            var bar = i < modules.Count && modules[i];
            if (bar && run < 0) run = i;
            else if (!bar && run >= 0)
            {
                sb.Append($"""<rect x="{(run + BarcodeEncoder.QuietZone) * moduleWidth}" y="0" width="{(i - run) * moduleWidth}" height="{barHeight}" fill="{E(color)}"/>""");
                run = -1;
            }
        }
        if (text is not null)
            sb.Append($"""<text x="{width / 2}" y="{barHeight + 14}" text-anchor="middle" font-family="monospace" font-size="13" fill="{E(color)}">{E(text)}</text>""");
        sb.Append("</svg>");
        return sb.ToString();
    }
}
