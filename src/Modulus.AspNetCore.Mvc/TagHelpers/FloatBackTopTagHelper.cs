using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Qr;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Back-to-top float button, shown once the page (or <c>target</c>) has scrolled <c>visibility-height</c> px:
/// <c>&lt;app-float-back-top /&gt;</c>. Behaviour comes from modulus-shell.js.
/// </summary>
[HtmlTargetElement("app-float-back-top", TagStructure = TagStructure.WithoutEndTag)]
public class FloatBackTopTagHelper : TagHelper
{
    public int VisibilityHeight { get; set; } = 400;

    /// <summary>CSS selector of the scrolling element; the page when unset.</summary>
    public string? Target { get; set; }

    public string Type { get; set; } = "default";
    public string Shape { get; set; } = "circle";
    public string? Description { get; set; }
    public string? Tooltip { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "button";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", "button");
        output.AddClasses(FloatButtons.Classes(Type, Shape), "m-float-back-top");
        output.Attributes.SetAttribute("data-m-back-top", VisibilityHeight.ToString(CultureInfo.InvariantCulture));
        if (Target is not null) output.Attributes.SetAttribute("data-m-target", Target);
        if (!output.Attributes.ContainsName("aria-label")) output.Attributes.SetAttribute("aria-label", Tooltip ?? "Back to top");
        output.Content.SetHtmlContent(FloatButtons.Body("vertical-align-top", Description, Tooltip, null, false));
    }
}
