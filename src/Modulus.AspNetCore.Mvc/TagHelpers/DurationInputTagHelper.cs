using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Forms;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Duration typed as timesheets take it ("1:30", "1.5", "90m", "1h 30m"), shown as h:mm, posted as whole minutes.
/// <code>&lt;app-duration-input asp-for="Minutes" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-duration-input", TagStructure = TagStructure.WithoutEndTag)]
public class DurationInputTagHelper : BoundControlTagHelper
{
    /// <summary>Text after the value; "h" by default.</summary>
    public string Suffix { get; set; } = "h";

    /// <summary>Longest duration in minutes (24 h by default).</summary>
    public int Max { get; set; } = 24 * 60;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, raw) = Bound();
        var extra = output.TakeAttributes();
        int? value = int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? Math.Min(m, Max) : null;
        var placeholder = extra.Contains(" placeholder=", StringComparison.Ordinal) ? "" : " placeholder=\"0:00\"";

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-input-affix-wrapper", SizeClass("ant-input-affix-wrapper"), StatusClass("ant-input-affix-wrapper"),
            Disabled ? "ant-input-affix-wrapper-disabled" : null, "m-field m-duration");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusDuration({Widgets.Config(new { value, max = Max })})"));
        output.Content.SetHtmlContent(
            $"""<span class="ant-input-prefix">{AntIcons.Span("clock-circle")}</span>""" +
            $"""<input type="text" inputmode="decimal" autocomplete="off" class="ant-input m-num"{Html.Attr("id", id.Length > 0 ? id : null)}{placeholder} title="1:30, 1.5, 90m or 1h 30m" value="{Html.E(DurationText.Format(value))}" x-ref="input" @change="commit()"{Html.Attr("disabled", Disabled)}{BusinessFields.Invalid(EffectiveStatus)}{extra} />""" +
            $"""<span class="ant-input-suffix">{Html.E(Suffix)}</span>""" +
            BusinessFields.Hidden(name, value?.ToString(CultureInfo.InvariantCulture), "value", "raw"));
    }
}
