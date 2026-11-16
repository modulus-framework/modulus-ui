using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>Number input with stepper handles: <c>&lt;app-input-number asp-for="Seats" min="1" max="500" /&gt;</c>.</summary>
[HtmlTargetElement("app-input-number", TagStructure = TagStructure.WithoutEndTag)]
public class InputNumberTagHelper : BoundControlTagHelper
{
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public decimal Step { get; set; } = 1;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var attrs = ControlAttributes(output);
        const string bump = "$refs.input.dispatchEvent(new Event('input', { bubbles: true })); $refs.input.dispatchEvent(new Event('change', { bubbles: true }))";
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-input-number", SizeClass("ant-input-number"), StatusClass("ant-input-number"), Disabled ? "ant-input-number-disabled" : null);
        output.Attributes.SetAttribute("x-data", "");
        output.Content.SetHtmlContent(
            """<div class="ant-input-number-handler-wrap" aria-hidden="true">""" +
            $"""<span unselectable="on" role="button" tabindex="-1" aria-label="Increase value" class="ant-input-number-handler ant-input-number-handler-up" @mousedown.prevent @click="$refs.input.stepUp(); {bump}">{AntIcons.Span("up", IconThemeType.Outline, "ant-input-number-handler-up-inner")}</span>""" +
            $"""<span unselectable="on" role="button" tabindex="-1" aria-label="Decrease value" class="ant-input-number-handler ant-input-number-handler-down" @mousedown.prevent @click="$refs.input.stepDown(); {bump}">{AntIcons.Span("down", IconThemeType.Outline, "ant-input-number-handler-down-inner")}</span>""" +
            "</div>" +
            $"""<div class="ant-input-number-input-wrap"><input type="number" role="spinbutton" inputmode="decimal" autocomplete="off" class="ant-input-number-input" x-ref="input"{Html.Attr("min", Min?.ToString(CultureInfo.InvariantCulture))}{Html.Attr("max", Max?.ToString(CultureInfo.InvariantCulture))} step="{Step.ToString(CultureInfo.InvariantCulture)}"{attrs} /></div>""");
    }
}
