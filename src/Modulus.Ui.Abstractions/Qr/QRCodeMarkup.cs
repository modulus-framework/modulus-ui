using System.Globalization;
using System.Net;

namespace Modulus.Ui.Abstractions.Qr;

/// <summary>The overlay shown over a QR code (Ant Design's <c>status</c>).</summary>
public enum QRCodeStatus
{
    Active,
    Expired,
    Loading,
    Scanned,
}

/// <summary>
/// The wrapper classes, style and SVG of the QR code component, shared by the Blazor <c>AppQRCode</c> and the MVC
/// <c>&lt;app-qr-code&gt;</c> so both draw the same box.
/// </summary>
public static class QRCodeMarkup
{
    /// <summary>Padding (12px each side) plus the 1px border of a bordered code.</summary>
    public const int BorderedInset = 26;

    public static string CssClass(bool bordered, string? extra = null) =>
        "ant-qrcode m-qrcode" + (bordered ? "" : " ant-qrcode-borderless") + (string.IsNullOrWhiteSpace(extra) ? "" : " " + extra);

    public static string CssStyle(int size, string background, string? extra = null)
    {
        var style = string.Create(CultureInfo.InvariantCulture, $"width:{size}px;height:{size}px;background-color:{background}");
        return string.IsNullOrWhiteSpace(extra) ? style : style + ";" + extra.TrimEnd(';');
    }

    /// <summary>
    /// The code as SVG sized to fit inside the wrapper. An empty value draws nothing; a value too long for any QR
    /// version draws a short message instead of throwing.
    /// </summary>
    public static string Svg(string? value, int size, string color, string background, QrErrorLevel level,
        string? icon, int iconSize, bool bordered, string? label)
    {
        if (string.IsNullOrEmpty(value)) return "";
        var inner = Math.Max(bordered ? size - BorderedInset : size, 21);
        try
        {
            var code = QrCode.Encode(value, level);
            return QrSvg.Render(code, color, background, inner, 0, icon, iconSize, label ?? value);
        }
        catch (ArgumentException)
        {
            return $"<span class=\"m-qrcode-error\" role=\"alert\">{WebUtility.HtmlEncode("Text too long for a QR code")}</span>";
        }
    }
}
