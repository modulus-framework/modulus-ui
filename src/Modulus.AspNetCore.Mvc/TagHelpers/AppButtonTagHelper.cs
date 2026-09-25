using Microsoft.AspNetCore.Html;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.TagHelpers;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// TagHelper for antd-styled Button: &lt;app-button type="primary" icon="plus"&gt;Add&lt;/app-button&gt;
/// </summary>
[HtmlTargetElement("app-button")]
public class AppButtonTagHelper : TagHelper
{
    /// <summary>
    /// Button type: "primary" | "default" | "dashed" | "text" | "link"
    /// </summary>
    [HtmlAttributeName("type")]
    public string Type { get; set; } = "default";

    /// <summary>
    /// Button size: "large" | "middle" | "small"
    /// </summary>
    [HtmlAttributeName("size")]
    public string Size { get; set; } = "middle";

    /// <summary>
    /// Icon name (antd icon, e.g. "plus", "delete", "search")
    /// </summary>
    [HtmlAttributeName("icon")]
    public string? Icon { get; set; }

    /// <summary>
    /// Danger/red color
    /// </summary>
    [HtmlAttributeName("danger")]
    public bool Danger { get; set; }

    /// <summary>
    /// Disabled state
    /// </summary>
    [HtmlAttributeName("disabled")]
    public bool Disabled { get; set; }

    /// <summary>
    /// Full-width button
    /// </summary>
    [HtmlAttributeName("block")]
    public bool Block { get; set; }

    /// <summary>
    /// HTML button type (button, submit, reset)
    /// </summary>
    [HtmlAttributeName("html-type")]
    public string HtmlType { get; set; } = "button";

    /// <summary>
    /// Href for link buttons
    /// </summary>
    [HtmlAttributeName("href")]
    public string? Href { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = Href is not null ? "a" : "button";
        output.TagMode = TagMode.StartTagAndEndTag;

        var classes = GetClasses();
        output.Attributes.SetAttribute("class", classes);

        if (Href is not null)
        {
            output.Attributes.SetAttribute("href", Href);
        }
        else
        {
            output.Attributes.SetAttribute("type", HtmlType);
        }

        if (Disabled)
        {
            output.Attributes.SetAttribute("disabled", "disabled");
        }

        // Add icon if specified
        if (!string.IsNullOrEmpty(Icon))
        {
            var iconHtml = $"""<i class="anticon anticon-{Icon}" style="margin-right: 8px;"></i>""";
            output.PreContent.AppendHtml(iconHtml);
        }
    }

    private string GetClasses()
    {
        var classList = new List<string> { "ant-btn" };

        // Type
        if (Type == "primary")
            classList.Add("ant-btn-primary");
        else if (Type == "dashed")
            classList.Add("ant-btn-dashed");
        else if (Type == "text")
            classList.Add("ant-btn-text");
        else if (Type == "link")
            classList.Add("ant-btn-link");

        // Size
        if (Size == "large")
            classList.Add("ant-btn-lg");
        else if (Size == "small")
            classList.Add("ant-btn-sm");

        // States
        if (Danger)
            classList.Add("ant-btn-dangerous");
        if (Disabled)
            classList.Add("ant-btn-disabled");
        if (Block)
            classList.Add("ant-btn-block");

        return string.Join(" ", classList);
    }
}
