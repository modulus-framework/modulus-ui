using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Tooltip on hover and keyboard focus (CSS only): <c>&lt;app-tooltip title="Copy link"&gt;&lt;app-button icon="link" /&gt;&lt;/app-tooltip&gt;</c>.
/// </summary>
[HtmlTargetElement("app-tooltip")]
public class TooltipTagHelper : TagHelper
{
    public string Title { get; set; } = "";

    /// <summary>top | bottom | left | right</summary>
    public string Placement { get; set; } = "top";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-tooltip-host");
        output.PostContent.SetHtmlContent(
            $"""<span class="ant-tooltip ant-tooltip-placement-{Html.E(Placement)} m-tooltip m-pop-{Html.E(Placement)}" role="tooltip"><span class="ant-tooltip-content"><span class="ant-tooltip-arrow"><span class="ant-tooltip-arrow-content"></span></span><span class="ant-tooltip-inner">{Html.E(Title)}</span></span></span>""");
    }
}
