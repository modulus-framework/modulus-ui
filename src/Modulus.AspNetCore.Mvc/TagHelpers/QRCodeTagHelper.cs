using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Qr;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

// Ant Design 5 components that AntD Blazor 1.6.2 lacks: QRCode, ColorPicker, FloatButton and Tour. The Blazor
// versions live in Modulus.Blazor/Components/Extras; both emit the antd v5 DOM styled by modulus.css.
// ColorPicker and Tour run on modulusColorPicker / modulusTour in modulus-widgets.js; back-to-top and the tour
// geometry come from modulus-shell.js, which Blazor shares.


/// <summary>
/// QR code drawn as inline SVG on the server: <c>&lt;app-qr-code value="https://…" size="160" /&gt;</c>.
/// <c>status="expired"</c> shows a Refresh button over the code: it links to <c>refresh-href</c> when set (the server
/// issues a new code), otherwise it clears the overlay and raises <c>modulus-qr-refresh</c> with the value.
/// </summary>
[HtmlTargetElement("app-qr-code", TagStructure = TagStructure.WithoutEndTag)]
public class QRCodeTagHelper : TagHelper
{
    public string Value { get; set; } = "";
    public int Size { get; set; } = 160;
    public string Color { get; set; } = "#000";
    public string BgColor { get; set; } = "transparent";

    [HtmlAttributeName("error-level")]
    public QrErrorLevel ErrorLevel { get; set; } = QrErrorLevel.M;

    public string? Icon { get; set; }
    public int IconSize { get; set; } = 40;
    public bool Bordered { get; set; } = true;
    public QRCodeStatus Status { get; set; }
    public string? RefreshHref { get; set; }
    public string ExpiredText { get; set; } = "QR code expired";
    public string RefreshText { get; set; } = "Refresh";
    public string ScannedText { get; set; } = "Scanned";

    [HtmlAttributeName("aria-label")]
    public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (cls, style) = (output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null,
            output.Attributes.TryGetAttribute("style", out var s) ? s.Value?.ToString() : null);
        output.Attributes.RemoveAll("class");
        output.Attributes.RemoveAll("style");
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", QRCodeMarkup.CssClass(Bordered, cls));
        output.Attributes.SetAttribute("style", QRCodeMarkup.CssStyle(Size, BgColor, style));

        var html = new StringBuilder(QRCodeMarkup.Svg(Value, Size, Color, BgColor, ErrorLevel, Icon, IconSize, Bordered, AriaLabel));
        var clientRefresh = Status == QRCodeStatus.Expired && RefreshHref is null;
        if (clientRefresh) output.Attributes.SetAttribute("x-data", new HtmlString("{ expired: true }"));
        if (Status != QRCodeStatus.Active)
        {
            html.Append(clientRefresh ? "<div class=\"ant-qrcode-mask\" x-show=\"expired\">" : "<div class=\"ant-qrcode-mask\">");
            html.Append(Status switch
            {
                QRCodeStatus.Loading => """<div class="ant-spin ant-spin-spinning" role="status" aria-label="Loading"><span class="ant-spin-dot ant-spin-dot-spin"><i class="ant-spin-dot-item"></i><i class="ant-spin-dot-item"></i><i class="ant-spin-dot-item"></i><i class="ant-spin-dot-item"></i></span></div>""",
                QRCodeStatus.Expired => $"""<p class="ant-qrcode-expired">{Html.E(ExpiredText)}</p>""" +
                    (RefreshHref is null
                        ? $"""<button type="button" class="ant-btn ant-btn-link" @click="expired = false; $dispatch('modulus-qr-refresh', {Html.E(System.Text.Json.JsonSerializer.Serialize(Value))})">{AntIcons.Span("reload")}<span>{Html.E(RefreshText)}</span></button>"""
                        : $"""<a class="ant-btn ant-btn-link" href="{Html.E(RefreshHref)}">{AntIcons.Span("reload")}<span>{Html.E(RefreshText)}</span></a>"""),
                _ => $"""<p class="ant-qrcode-scanned">{Html.E(ScannedText)}</p>""",
            });
            html.Append("</div>");
        }
        output.Content.SetHtmlContent(html.ToString());
    }
}
