using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Segmented control over radio inputs: <c>&lt;app-segmented asp-for="View" items="new[] { "List", "Board" }" /&gt;</c>.
/// </summary>
[HtmlTargetElement("app-segmented", TagStructure = TagStructure.WithoutEndTag)]
public class SegmentedTagHelper : BoundControlTagHelper
{
    public IEnumerable<string> Items { get; set; } = [];

    /// <summary>Stretch to the container width.</summary>
    public bool Block { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, _, current) = Bound();
        var extra = output.TakeAttributes();
        var groupName = name.Length > 0 ? name : context.UniqueId;
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-segmented", Block ? "ant-segmented-block" : null, SizeClass("ant-segmented"), Disabled ? "ant-segmented-disabled" : null);
        output.Attributes.SetAttribute("role", "radiogroup");
        var sb = new StringBuilder("""<div class="ant-segmented-group">""");
        foreach (var item in Items)
        {
            var on = item == current;
            sb.Append($"""<label class="ant-segmented-item{(on ? " ant-segmented-item-selected" : "")}"><input class="ant-segmented-item-input" type="radio" name="{Html.E(groupName)}" value="{Html.E(item)}"{(on ? " checked" : "")}{Html.Attr("disabled", Disabled)}{extra} /><div class="ant-segmented-item-label" title="{Html.E(item)}">{Html.E(item)}</div></label>""");
        }
        output.Content.SetHtmlContent(sb.Append("</div>").ToString());
    }
}
