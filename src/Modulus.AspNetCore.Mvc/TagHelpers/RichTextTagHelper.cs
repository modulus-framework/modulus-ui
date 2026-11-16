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
/// Rich text (notes, terms and conditions): a formatting toolbar over an editable area; posts HTML. The initial value
/// is cleaned with <see cref="RichText.Sanitize(string?)"/>; clean what is posted on the server as well.
/// <code>&lt;app-rich-text asp-for="Notes" placeholder="Terms and conditions" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-rich-text", TagStructure = TagStructure.WithoutEndTag)]
public class RichTextTagHelper : BoundControlTagHelper
{
    public string? Placeholder { get; set; }

    /// <summary>Minimum editor height in pixels.</summary>
    public int Height { get; set; } = 140;

    [HtmlAttributeName("aria-label")] public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, raw) = Bound();
        output.TakeAttributes();
        var html = RichText.Sanitize(raw);

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-richtext", html.Length == 0 ? "m-richtext-empty" : null, Disabled ? "m-richtext-disabled" : null,
            EffectiveStatus is { } s ? $"m-richtext-status-{s}" : null);

        var sb = new StringBuilder();
        if (!Disabled)
        {
            sb.Append("""<div class="m-richtext-toolbar" role="toolbar" aria-label="Formatting">""");
            foreach (var group in RichTextToolbar.Groups)
            {
                sb.Append("""<span class="m-richtext-group">""");
                foreach (var b in group)
                {
                    sb.Append($"""<button type="button" class="m-richtext-btn" data-cmd="{Html.E(b.Command)}"{Html.Attr("data-arg", b.Argument)}{(b.Toggle ? " data-state=\"\"" : "")} aria-label="{Html.E(b.Label)}" title="{Html.E(b.Title)}"{(b.Toggle ? " aria-pressed=\"false\"" : "")}{(b.NeedsTable ? " disabled" : "")} tabindex="-1">""");
                    sb.Append(b.Icon is not null ? AntIcons.Span(b.Icon) : $"""<span class="m-richtext-btn-text">{Html.E(b.Text)}</span>""");
                    sb.Append("</button>");
                }
                sb.Append("</span>");
            }
            sb.Append("</div>");
        }
        sb.Append($"""<div class="ant-input m-richtext-editor"{Html.Attr("id", id.Length > 0 ? id : null)} contenteditable="{(Disabled ? "false" : "true")}" role="textbox" aria-multiline="true" aria-label="{Html.E(AriaLabel ?? "Rich text")}"{BusinessFields.Invalid(EffectiveStatus)}{Html.Attr("data-placeholder", Placeholder)} style="min-height:{Height.ToString(CultureInfo.InvariantCulture)}px">{html}</div>""");
        sb.Append($"""<input type="hidden"{Html.Attr("name", name.Length > 0 ? name : null)} value="{Html.E(html)}" />""");
        output.Content.SetHtmlContent(sb.ToString());
    }
}
