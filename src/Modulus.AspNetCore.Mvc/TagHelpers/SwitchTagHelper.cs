using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Toggle switch that posts <c>true</c>/<c>false</c> (binds to bool):
/// <c>&lt;app-switch asp-for="Alerts" checked-text="On" unchecked-text="Off" /&gt;</c>.
/// It raises a bubbling <c>change</c> event, so <c>hx-post="…" hx-trigger="change" hx-include="find input"</c> saves instantly.
/// </summary>
[HtmlTargetElement("app-switch", TagStructure = TagStructure.WithoutEndTag)]
public class SwitchTagHelper : BoundControlTagHelper
{
    public bool Checked { get; set; }
    public string? CheckedText { get; set; }
    public string? UncheckedText { get; set; }

    /// <summary>Accessible name when no visible label points at the switch.</summary>
    public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, _) = Bound();
        var on = For?.Model is true || Checked;
        output.TagName = "button";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", "button");
        output.Attributes.SetAttribute("role", "switch");
        if (id.Length > 0 && !output.Attributes.ContainsName("id")) output.Attributes.SetAttribute("id", id);
        if (AriaLabel is not null) output.Attributes.SetAttribute("aria-label", AriaLabel);
        if (Disabled) output.Attributes.SetAttribute("disabled", "disabled");
        output.Attributes.SetAttribute("aria-checked", on ? "true" : "false");
        output.Attributes.SetAttribute("x-data", $"{{ on: {(on ? "true" : "false")} }}");
        output.Attributes.SetAttribute(":aria-checked", "on");
        output.Attributes.SetAttribute(":class", "on && 'ant-switch-checked'");
        output.Attributes.SetAttribute("@click", "on = !on; $nextTick(() => $el.dispatchEvent(new Event('change', { bubbles: true })))");
        output.AddClasses("ant-switch", Size == "small" ? "ant-switch-small" : null, on ? "ant-switch-checked" : null, Disabled ? "ant-switch-disabled" : null);
        var inner = CheckedText is null && UncheckedText is null ? "" :
            $"""<span x-show="on"{(on ? "" : " style=\"display:none\"")}>{Html.E(CheckedText)}</span><span x-show="!on"{(on ? " style=\"display:none\"" : "")}>{Html.E(UncheckedText)}</span>""";
        output.Content.SetHtmlContent(
            $"""<div class="ant-switch-handle"></div><span class="ant-switch-inner">{inner}</span>""" +
            (name.Length == 0 ? "" : $"""<input type="hidden" name="{Html.E(name)}" value="{(on ? "true" : "false")}" :value="on" />"""));
    }
}
