using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// List of records:
/// <code>
/// &lt;app-list header="Invoices" bordered&gt;
///   &lt;app-list-item title="Acme" description="INV-1042 · $12,400" avatar-name="Acme" href="/invoices/1042"&gt;
///     &lt;list-item-actions&gt;&lt;a href="…"&gt;View&lt;/a&gt;&lt;/list-item-actions&gt;
///   &lt;/app-list-item&gt;
/// &lt;/app-list&gt;
/// </code>
/// </summary>
[HtmlTargetElement("app-list")]
public class ListTagHelper : TagHelper
{
    public string? Header { get; set; }
    public string? Footer { get; set; }
    public bool Bordered { get; set; }
    public bool Split { get; set; } = true;

    /// <summary>small | default | large</summary>
    public string Size { get; set; } = "default";

    /// <summary>Text for the empty state when the list has no items.</summary>
    public string EmptyText { get; set; } = "No data";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var items = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-list", Split ? "ant-list-split" : null, Bordered ? "ant-list-bordered" : null, Size switch { "small" => "ant-list-sm", "large" => "ant-list-lg", _ => null });
        var body = string.IsNullOrWhiteSpace(items)
            ? $"""<div class="ant-list-empty-text">{EmptyTagHelper.Render(EmptyText, "ant-empty-small")}</div>"""
            : $"""<ul class="ant-list-items">{items}</ul>""";
        output.Content.SetHtmlContent(
            (Header is null ? "" : $"""<div class="ant-list-header">{Html.E(Header)}</div>""") +
            $"""<div class="ant-spin-nested-loading"><div class="ant-spin-container">{body}</div></div>""" +
            (Footer is null ? "" : $"""<div class="ant-list-footer">{Html.E(Footer)}</div>"""));
    }
}
