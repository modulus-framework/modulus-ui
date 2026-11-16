using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>Multi-line input: <c>&lt;app-textarea asp-for="Notes" rows="4" maxlength="200" show-count /&gt;</c>.</summary>
[HtmlTargetElement("app-textarea", TagStructure = TagStructure.WithoutEndTag)]
public class TextAreaTagHelper : BoundControlTagHelper
{
    public int Rows { get; set; } = 3;

    /// <summary>Shows "n / maxlength" under the field.</summary>
    public bool ShowCount { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var value = Bound().Value ?? "";
        var max = output.Attributes.TryGetAttribute("maxlength", out var m) ? m.Value?.ToString() : null;
        var attrs = ControlAttributes(output, includeValue: false);
        var cls = Html.Cx("ant-input", SizeClass("ant-input"), StatusClass("ant-input"), Disabled ? "ant-input-disabled" : null);
        if (!ShowCount)
        {
            var (outerClass, outerStyle) = TakeClassAndStyle(output);
            output.TagName = null;
            output.Content.SetHtmlContent($"""<textarea class="{Html.Cx(cls, outerClass)}"{Html.Attr("style", outerStyle)} rows="{Rows}"{attrs}>{Html.E(value)}</textarea>""");
            return;
        }
        // Same structure as Blazor's <TextArea ShowCount>: the textarea sits in an affix wrapper with the
        // height antd's autosize computes (rows × line height + padding), and inside a form item the counter
        // hangs into the help row instead of adding height.
        var suffix = max is null ? "" : $" / {max}";
        var height = (Rows * 22.001 + 14).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-input-textarea", "ant-input-textarea-show-count", context.Items.ContainsKey(FormItemTagHelper.ContextKey) ? "ant-input-textarea-in-form-item" : null);
        output.Attributes.SetAttribute("x-data", $"{{ n: {value.Length} }}");
        output.Attributes.SetAttribute("data-count", $"{value.Length}{suffix}");
        output.Attributes.SetAttribute(":data-count", $"n + '{suffix}'");
        output.Content.SetHtmlContent($"""<span class="ant-input-affix-wrapper"><textarea class="{cls}" style="height:{height}px;overflow-y:auto;overflow-x:hidden" rows="{Rows}" @input="n = $el.value.length"{attrs}>{Html.E(value)}</textarea></span><span class="ant-input-textarea-suffix"></span>""");
    }
}
