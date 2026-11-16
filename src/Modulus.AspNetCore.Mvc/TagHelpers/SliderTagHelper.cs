using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>Slider over a native range input: <c>&lt;app-slider asp-for="Discount" min="0" max="100" show-value /&gt;</c>.</summary>
[HtmlTargetElement("app-slider", TagStructure = TagStructure.WithoutEndTag)]
public class SliderTagHelper : BoundControlTagHelper
{
    public decimal Min { get; set; }
    public decimal Max { get; set; } = 100;
    public decimal Step { get; set; } = 1;

    /// <summary>Shows the current value next to the slider.</summary>
    public bool ShowValue { get; set; }

    /// <summary>Format for the displayed value, e.g. "{0}%".</summary>
    public string ValueFormat { get; set; } = "{0}";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var raw = decimal.TryParse(Bound().Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : Min;
        var pct = Max > Min ? (raw - Min) / (Max - Min) * 100 : 0;
        var attrs = ControlAttributes(output, includeValue: false);
        var inv = CultureInfo.InvariantCulture;
        var format = Html.E(ValueFormat).Replace("{0}", "' + v + '", StringComparison.Ordinal);
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-slider", Disabled ? "ant-slider-disabled" : null);
        output.Attributes.SetAttribute("x-data", $"{{ v: {raw.ToString(inv)} }}");
        output.AddStyle($"--m-slider:{pct.ToString("0.##", inv)}%");
        var (min, max) = (Min.ToString(inv), Max.ToString(inv));
        output.Attributes.SetAttribute(":style", Max > Min ? "`--m-slider:${(v - " + min + ") / (" + max + " - " + min + ") * 100}%`" : "'--m-slider:0%'");
        output.Content.SetHtmlContent(
            $"""<input type="range" class="m-slider-input" min="{Min.ToString(inv)}" max="{Max.ToString(inv)}" step="{Step.ToString(inv)}" value="{raw.ToString(inv)}" x-model.number="v"{attrs} />""" +
            (ShowValue ? $"""<span class="m-slider-value" x-text="'{format}'">{Html.E(string.Format(inv, ValueFormat, raw))}</span>""" : ""));
    }
}
