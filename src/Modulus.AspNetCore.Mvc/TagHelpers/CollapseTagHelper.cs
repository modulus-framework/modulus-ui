using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Expandable panels:
/// <code>
/// &lt;app-collapse active="billing" accordion&gt;
///   &lt;app-panel key="billing" header="How is billing calculated?"&gt;…&lt;/app-panel&gt;
/// &lt;/app-collapse&gt;
/// </code>
/// </summary>
[HtmlTargetElement("app-collapse")]
public class CollapseTagHelper : TagHelper
{
    /// <summary>Comma-separated keys of the initially open panels.</summary>
    public string? Active { get; set; }

    /// <summary>Only one panel open at a time.</summary>
    public bool Accordion { get; set; }

    public bool Bordered { get; set; } = true;

    /// <summary>Transparent, borderless panels.</summary>
    public bool Ghost { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var open = (Active ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        context.Items[typeof(CollapseTagHelper)] = open;
        var body = (await output.GetChildContentAsync()).GetContent();
        var keys = string.Join(", ", open.Select(k => $"'{Html.E(k)}'"));
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-collapse", "ant-collapse-icon-position-left", Bordered && !Ghost ? null : "ant-collapse-borderless", Ghost ? "ant-collapse-ghost" : null, "m-collapse");
        output.Attributes.SetAttribute("x-data", $"modulusCollapse([{keys}], {(Accordion ? "true" : "false")})");
        output.Content.SetHtmlContent(body);
    }
}
