using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Popover with a title and rich content. Opens on hover/focus, or on click with <c>trigger="click"</c>:
/// <code>
/// &lt;app-popover title="Seats" trigger="click"&gt;
///   &lt;app-button&gt;Details&lt;/app-button&gt;
///   &lt;popover-content&gt;18 of 25 seats used.&lt;/popover-content&gt;
/// &lt;/app-popover&gt;
/// </code>
/// </summary>
[HtmlTargetElement("app-popover")]
public class PopoverTagHelper : TagHelper
{
    public string? Title { get; set; }

    /// <summary>Plain-text content (or use &lt;popover-content&gt;).</summary>
    public string? Content { get; set; }

    /// <summary>hover | click</summary>
    public string Trigger { get; set; } = "hover";

    /// <summary>top | bottom | left | right</summary>
    public string Placement { get; set; } = "top";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new Dictionary<string, string>();
        context.Items[typeof(PopoverTagHelper)] = slots;
        var trigger = (await output.GetChildContentAsync()).GetContent();
        var body = slots.TryGetValue("popover-content", out var c) ? c : Html.E(Content);
        var click = Trigger == "click";
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-tooltip-host", click ? "m-popover-click" : null);
        if (click)
        {
            output.Attributes.SetAttribute("x-data", "{ open: false }");
            output.Attributes.SetAttribute(":class", "open && 'm-open'");
            output.Attributes.SetAttribute("@click.outside", "open = false");
            output.Attributes.SetAttribute("@keydown.escape", "open = false");
            trigger = $"""<span class="m-popover-trigger" @click="open = !open" :aria-expanded="open" aria-haspopup="dialog">{trigger}</span>""";
        }
        output.Content.SetHtmlContent(trigger +
            $"""<span class="ant-popover ant-popover-placement-{Html.E(Placement)} m-tooltip m-popover m-pop-{Html.E(Placement)}" role="{(click ? "dialog" : "tooltip")}"><span class="ant-popover-content"><span class="ant-popover-arrow"><span class="ant-popover-arrow-content"></span></span><span class="ant-popover-inner" role="presentation">""" +
            (Title is null ? "" : $"""<span class="ant-popover-title">{Html.E(Title)}</span>""") +
            $"""<span class="ant-popover-inner-content">{body}</span></span></span></span>""");
    }
}
