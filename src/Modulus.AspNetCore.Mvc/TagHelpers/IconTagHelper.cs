using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Ant Design icon: <c>&lt;app-icon type="dashboard" /&gt;</c>, <c>&lt;app-icon type="check-circle" theme="fill" /&gt;</c>.
/// Same SVG as Blazor's &lt;Icon&gt;.
/// </summary>
[HtmlTargetElement("app-icon", TagStructure = TagStructure.WithoutEndTag)]
public class IconTagHelper : TagHelper
{
    public string Type { get; set; } = "";

    public IconThemeType Theme { get; set; } = IconThemeType.Outline;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("role", "img");
        if (!output.Attributes.ContainsName("aria-label")) output.Attributes.SetAttribute("aria-hidden", "true");
        output.AddClasses("anticon", $"anticon-{Type}");
        output.Content.SetHtmlContent(AntIcons.Svg(Type, Theme) ?? "");
    }
}
