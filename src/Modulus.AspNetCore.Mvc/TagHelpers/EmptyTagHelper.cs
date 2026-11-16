using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>Empty state: <c>&lt;app-empty description="No invoices yet"&gt;&lt;app-button type="primary"&gt;Create&lt;/app-button&gt;&lt;/app-empty&gt;</c>.</summary>
[HtmlTargetElement("app-empty")]
public class EmptyTagHelper : TagHelper
{
    public string Description { get; set; } = "No data";

    /// <summary>Custom image URL instead of the built-in illustration.</summary>
    public string? Image { get; set; }

    internal const string SimpleSvg =
        """<svg class="ant-empty-img-simple" width="64" height="41" viewBox="0 0 64 41" xmlns="http://www.w3.org/2000/svg" aria-hidden="true"><g transform="translate(0 1)" fill="none" fill-rule="evenodd"><ellipse class="ant-empty-img-simple-ellipse" cx="32" cy="33" rx="32" ry="7"></ellipse><g class="ant-empty-img-simple-g" fill-rule="nonzero"><path d="M55 12.76L44.854 1.258C44.367.474 43.656 0 42.907 0H21.093c-.749 0-1.46.474-1.947 1.257L9 12.761V22h46v-9.24z"></path><path d="M41.613 15.931c0-1.605.994-2.93 2.227-2.931H55v18.137C55 33.26 53.68 35 52.05 35h-40.1C10.32 35 9 33.259 9 31.137V13h11.16c1.233 0 2.227 1.323 2.227 2.928v.022c0 1.605 1.005 2.901 2.237 2.901h14.752c1.232 0 2.237-1.308 2.237-2.913v-.007z" class="ant-empty-img-simple-path"></path></g></g></svg>""";

    internal static string Render(string description, string? extraClass = null, string? image = null, string? footer = null) =>
        $"""<div class="{Html.Cx("ant-empty", "ant-empty-normal", extraClass)}"><div class="ant-empty-image">{(image is null ? SimpleSvg : $"<img src=\"{Html.E(image)}\" alt=\"\" />")}</div><p class="ant-empty-description">{Html.E(description)}</p>{(string.IsNullOrWhiteSpace(footer) ? "" : $"<div class=\"ant-empty-footer\">{footer}</div>")}</div>""";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var footer = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-empty", "ant-empty-normal");
        output.Content.SetHtmlContent(
            $"""<div class="ant-empty-image">{(Image is null ? SimpleSvg : $"<img src=\"{Html.E(Image)}\" alt=\"\" />")}</div><p class="ant-empty-description">{Html.E(Description)}</p>""" +
            (string.IsNullOrWhiteSpace(footer) ? "" : $"""<div class="ant-empty-footer">{footer}</div>"""));
    }
}
