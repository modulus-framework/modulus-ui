using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Ant Design button. Renders &lt;a&gt; when <c>href</c> is set, otherwise &lt;button&gt;.
/// All other attributes (hx-*, x-*, @click, form…) pass through.
/// <code>&lt;app-button type="primary" icon="plus" hx-get="/customers/create"&gt;New&lt;/app-button&gt;</code>
/// </summary>
[HtmlTargetElement("app-button")]
public class ButtonTagHelper : TagHelper
{
    /// <summary>primary | default | dashed | text | link</summary>
    public string Type { get; set; } = "default";

    /// <summary>small | middle | large</summary>
    public string Size { get; set; } = "middle";

    public bool Danger { get; set; }
    public bool Block { get; set; }
    public bool Ghost { get; set; }

    /// <summary>circle | round</summary>
    public string? Shape { get; set; }

    /// <summary>Ant Design icon name shown before the label.</summary>
    public string? Icon { get; set; }

    public string? Href { get; set; }

    /// <summary>button | submit | reset (ignored for links).</summary>
    public string HtmlType { get; set; } = "button";

    public bool Disabled { get; set; }

    /// <summary>Shows a spinner and blocks clicks. Buttons that issue htmx requests get the same state automatically while the request runs.</summary>
    public bool Loading { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = (await output.GetChildContentAsync()).GetContent();
        var iconOnly = string.IsNullOrWhiteSpace(content) && Icon is not null;

        output.TagMode = TagMode.StartTagAndEndTag;
        if (Href is not null)
        {
            output.TagName = "a";
            output.Attributes.SetAttribute("href", Href);
        }
        else
        {
            output.TagName = "button";
            output.Attributes.SetAttribute("type", HtmlType);
        }
        if (Disabled) output.Attributes.SetAttribute("disabled", "disabled");

        output.AddClasses(
            "ant-btn",
            $"ant-btn-{Type}",
            Size switch { "small" => "ant-btn-sm", "large" => "ant-btn-lg", _ => null },
            Danger ? "ant-btn-dangerous" : null,
            Block ? "ant-btn-block" : null,
            Ghost ? "ant-btn-background-ghost" : null,
            Shape is null ? null : $"ant-btn-{Shape}",
            iconOnly ? "ant-btn-icon-only" : null,
            Loading ? "ant-btn-loading" : null);
        if (Loading) output.Attributes.SetAttribute("aria-busy", "true");

        var icon = Loading
            ? $"""<span class="ant-btn-loading-icon">{AntIcons.Span("loading", AntDesign.IconThemeType.Outline, "anticon-spin")}</span>"""
            : Icon is null ? "" : AntIcons.Span(Icon);
        var label = string.IsNullOrWhiteSpace(content) ? "" : $"<span>{content.Trim()}</span>";
        // A space between icon and label, as AntDesign Blazor renders it, so buttons measure the same.
        output.Content.SetHtmlContent(icon.Length > 0 && label.Length > 0 ? icon + " " + label : icon + label);
    }
}
