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
/// Button fixed to the bottom right: <c>&lt;app-float-button icon="question-circle" tooltip="Help" /&gt;</c>. A link
/// with <c>href</c>. Other attributes (<c>@click</c>, <c>hx-*</c>…) stay on the button.
/// </summary>
[HtmlTargetElement("app-float-button", TagStructure = TagStructure.WithoutEndTag)]
public class FloatButtonTagHelper : TagHelper
{
    public string? Icon { get; set; }
    public string? Description { get; set; }
    public string? Tooltip { get; set; }

    /// <summary>default | primary</summary>
    public string Type { get; set; } = "default";

    /// <summary>circle | square</summary>
    public string Shape { get; set; } = "circle";

    public string? Href { get; set; }
    public int? BadgeCount { get; set; }
    public bool BadgeDot { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagMode = TagMode.StartTagAndEndTag;
        if (Href is null)
        {
            output.TagName = "button";
            if (!output.Attributes.ContainsName("type")) output.Attributes.SetAttribute("type", "button");
        }
        else
        {
            output.TagName = "a";
            output.Attributes.SetAttribute("href", Href);
        }
        output.AddClasses(FloatButtons.Classes(Type, Shape));
        if (!output.Attributes.ContainsName("aria-label") && (Tooltip ?? Description) is { } label)
            output.Attributes.SetAttribute("aria-label", label);
        output.Content.SetHtmlContent(FloatButtons.Body(Icon, Description, Tooltip, BadgeCount, BadgeDot));
    }
}
