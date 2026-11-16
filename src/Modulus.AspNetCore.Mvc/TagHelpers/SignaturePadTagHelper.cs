using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Forms;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Signature pad: draw or type a name; posts a PNG data URL (check it with <see cref="SignatureImage.TryDecode"/>).
/// <code>&lt;app-signature-pad asp-for="Signature" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-signature-pad", TagStructure = TagStructure.WithoutEndTag)]
public class SignaturePadTagHelper : BoundControlTagHelper
{
    public int Height { get; set; } = 160;
    public string Ink { get; set; } = "#111827";
    public bool AllowTyping { get; set; } = true;
    public string Hint { get; set; } = "Sign here";
    public string ClearText { get; set; } = "Clear";
    public string TypePlaceholder { get; set; } = "Or type your name";

    [HtmlAttributeName("aria-label")] public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, _, raw) = Bound();
        output.TakeAttributes();
        var value = SignatureImage.IsValid(raw) ? raw : "";

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-signature", string.IsNullOrEmpty(value) ? null : "m-signature-signed", Disabled ? "m-signature-disabled" : null,
            EffectiveStatus is { } s ? $"m-signature-status-{s}" : null);
        output.AddStyle($"--m-signature-height:{Height.ToString(CultureInfo.InvariantCulture)}px");
        output.Attributes.SetAttribute("data-ink", Ink);
        output.Attributes.SetAttribute("data-m-signature", "");

        var sb = new StringBuilder();
        sb.Append($"""<div class="m-signature-pad"><canvas class="m-signature-canvas" role="img" aria-label="{Html.E(AriaLabel ?? "Signature")}"></canvas><span class="m-signature-hint" aria-hidden="true">{Html.E(Hint)}</span><span class="m-signature-line" aria-hidden="true"></span></div>""");
        if (!Disabled)
        {
            sb.Append("""<div class="m-signature-actions">""");
            if (AllowTyping) sb.Append($"""<input type="text" class="ant-input ant-input-sm m-signature-type" placeholder="{Html.E(TypePlaceholder)}" aria-label="{Html.E(TypePlaceholder)}" autocomplete="name" />""");
            sb.Append($"""<button type="button" class="ant-btn ant-btn-sm ant-btn-text" data-m-signature-clear>{AntIcons.Span("clear")}<span>{Html.E(ClearText)}</span></button></div>""");
        }
        sb.Append($"""<input type="hidden"{Html.Attr("name", name.Length > 0 ? name : null)} value="{Html.E(value)}" />""");
        output.Content.SetHtmlContent(sb.ToString());
    }
}
