using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

[HtmlTargetElement("app-panel", ParentTag = "app-collapse")]
public class CollapsePanelTagHelper : TagHelper
{
    public string Key { get; set; } = "";
    public string Header { get; set; } = "";
    public bool Disabled { get; set; }

    /// <summary>Text shown on the right of the header.</summary>
    public string? Extra { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = (await output.GetChildContentAsync()).GetContent();
        var key = Html.E(Key.Length > 0 ? Key : Header);
        var on = context.Items.TryGetValue(typeof(CollapseTagHelper), out var open) && ((HashSet<string>)open).Contains(Key);
        var id = context.UniqueId;
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-collapse-item", on ? "ant-collapse-item-active" : null, Disabled ? "ant-collapse-item-disabled" : null);
        output.Attributes.SetAttribute(":class", "{ 'ant-collapse-item-active': isOpen('" + key + "') }");
        var toggle = Disabled ? "" : $""" @click="toggle('{key}')" @keydown.enter.prevent="toggle('{key}')" @keydown.space.prevent="toggle('{key}')" """;
        output.Content.SetHtmlContent(
            $"""<div class="ant-collapse-header" role="button" tabindex="{(Disabled ? -1 : 0)}" aria-controls="{id}" aria-expanded="{(on ? "true" : "false")}" :aria-expanded="isOpen('{key}')"{Html.Attr("aria-disabled", Disabled ? "true" : null)}{toggle.TrimEnd()}>""" +
            $"""<div class="ant-collapse-expand-icon">{AntIcons.Span("right", IconThemeType.Outline, "ant-collapse-arrow")}</div>""" +
            $"""<span class="ant-collapse-header-text">{Html.E(Header)}</span>""" +
            (Extra is null ? "" : $"""<div class="ant-collapse-extra">{Html.E(Extra)}</div>""") + "</div>" +
            $"""<div id="{id}" role="region" class="ant-collapse-content ant-collapse-content-{(on ? "active" : "inactive")}" x-show="isOpen('{key}')"{(on ? "" : " style=\"display:none\"")}><div class="ant-collapse-content-box">{content}</div></div>""");
    }
}
