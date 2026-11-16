using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Qr;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// A stack of float buttons: <c>&lt;app-float-button-group trigger="click"&gt;&lt;app-float-button …/&gt;&lt;/app-float-button-group&gt;</c>.
/// With <c>trigger</c> (click | hover) the children fold behind a toggle button.
/// </summary>
[HtmlTargetElement("app-float-button-group")]
public class FloatButtonGroupTagHelper : TagHelper
{
    public string? Trigger { get; set; }
    public bool Open { get; set; }
    public string Shape { get; set; } = "circle";
    public string Type { get; set; } = "default";
    public string Icon { get; set; } = "plus";
    public string CloseIcon { get; set; } = "close";
    public string? Description { get; set; }
    public string? Tooltip { get; set; }

    [HtmlAttributeName("aria-label")]
    public string? AriaLabel { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var body = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        var square = Shape == "square";
        output.AddClasses("ant-float-btn-group", square ? "ant-float-btn-group-square" : "ant-float-btn-group-circle",
            square && Trigger is null ? "ant-float-btn-group-square-shadow" : null);
        if (Trigger is null)
        {
            output.Content.SetHtmlContent(body);
            return;
        }

        output.Attributes.SetAttribute("x-data", new HtmlString($"{{ open: {(Open ? "true" : "false")} }}"));
        output.Attributes.SetAttribute(":class", "open && 'ant-float-btn-group-open'");
        if (Trigger == "hover")
        {
            output.Attributes.SetAttribute("@mouseenter", "open = true");
            output.Attributes.SetAttribute("@mouseleave", "open = false");
        }
        var label = AriaLabel ?? Tooltip ?? "Actions";
        output.Content.SetHtmlContent(
            $"""<div class="ant-float-btn-group-wrap" x-show="open"{(Open ? "" : " x-cloak")}>{body}</div>""" +
            $"""<button type="button" class="{FloatButtons.Classes(Type, Shape)}" aria-label="{Html.E(label)}" :aria-expanded="open" @click="open = !open">""" +
            """<div class="ant-float-btn-body"><div class="ant-float-btn-content">""" +
            $"""<div class="ant-float-btn-icon"><span x-show="!open">{AntIcons.Span(Icon)}</span><span x-show="open" x-cloak>{AntIcons.Span(CloseIcon)}</span></div>""" +
            (Description is null ? "" : $"""<div class="ant-float-btn-description" x-show="!open">{Html.E(Description)}</div>""") +
            "</div></div>" +
            (Tooltip is null ? "" : $"""<span class="m-float-btn-tip" aria-hidden="true">{Html.E(Tooltip)}</span>""") + "</button>");
    }
}
