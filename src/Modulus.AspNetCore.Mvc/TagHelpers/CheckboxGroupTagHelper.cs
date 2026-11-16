using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Checkbox group bound to a collection: <c>&lt;app-checkbox-group asp-for="Channels" items="…" /&gt;</c>.
/// </summary>
[HtmlTargetElement("app-checkbox-group", TagStructure = TagStructure.WithoutEndTag)]
public class CheckboxGroupTagHelper : BoundControlTagHelper
{
    public IEnumerable<SelectListItem> Items { get; set; } = [];

    /// <summary>Stacks the boxes one under another. The default lays them out in a row.</summary>
    public bool Vertical { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, _, _) = Bound();
        var selected = For?.Model is IEnumerable list and not string
            ? list.Cast<object?>().Select(Html.Format).ToHashSet()
            : [];
        var extra = output.TakeAttributes();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-checkbox-group");
        if (Vertical) output.AddClasses("m-choice-vertical");
        output.Attributes.SetAttribute("role", "group");
        var sb = new StringBuilder();
        foreach (var item in Items)
        {
            var on = item.Selected || selected.Contains(item.Value) || selected.Contains(item.Text);
            var off = item.Disabled || Disabled;
            sb.Append($"""<label class="{Html.Cx("ant-checkbox-wrapper", "ant-checkbox-group-item", on ? "ant-checkbox-wrapper-checked" : null, off ? "ant-checkbox-wrapper-disabled" : null)}"><span class="{Html.Cx("ant-checkbox", on ? "ant-checkbox-checked" : null, off ? "ant-checkbox-disabled" : null)}"><input type="checkbox" class="ant-checkbox-input" name="{Html.E(name)}" value="{Html.E(item.Value)}"{(on ? " checked" : "")}{Html.Attr("disabled", off)}{extra} /><span class="ant-checkbox-inner"></span></span><span>{Html.E(item.Text)}</span></label>""");
        }
        output.Content.SetHtmlContent(sb.ToString());
    }
}
