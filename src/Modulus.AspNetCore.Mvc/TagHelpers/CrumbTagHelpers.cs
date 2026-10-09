using System.Net;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Navigation;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// The breadcrumb trail (<see cref="CrumbHtml"/>), the same markup as the Blazor <c>AppCrumbs</c>. Each level is an
/// <c>app-crumb</c>; the last one is the current page:
/// <code>&lt;app-crumbs&gt;&lt;app-crumb href="/" icon="home"&gt;Home&lt;/app-crumb&gt;&lt;app-crumb&gt;Orders&lt;/app-crumb&gt;&lt;/app-crumbs&gt;</code>
/// </summary>
[HtmlTargetElement("app-crumbs")]
public class CrumbsTagHelper : TagHelper
{
    /// <summary>The most levels shown inline; a longer trail folds the middle into a "…" menu.</summary>
    public int MaxVisible { get; set; } = 4;

    /// <summary>Accessible name of the trail.</summary>
    public string Label { get; set; } = "You are here";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var crumbs = new ItemList<Crumb>();
        context.Items[typeof(CrumbsTagHelper)] = crumbs;
        await output.GetChildContentAsync();

        output.TagName = null;
        output.Content.SetHtmlContent(CrumbHtml.Trail(crumbs, MaxVisible, Label));
    }
}

/// <summary>
/// One level of a breadcrumb trail: its text, the address it leads to (none for the current page), and an icon. Inside
/// <c>app-crumbs</c> it is part of that trail; inside <c>app-page-header</c> it is the page's own trail above the title.
/// </summary>
[HtmlTargetElement("app-crumb", ParentTag = "app-crumbs")]
[HtmlTargetElement("app-crumb", ParentTag = "app-page-header")]
public class CrumbTagHelper : TagHelper
{
    public string? Href { get; set; }
    public string? Icon { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        // The child text is encoded for HTML; the trail encodes its labels again, so the text is decoded first.
        var label = WebUtility.HtmlDecode((await output.GetChildContentAsync()).GetContent()).Trim();
        if (context.Items.TryGetValue(typeof(CrumbsTagHelper), out var items) || context.Items.TryGetValue(typeof(PageHeaderTagHelper), out items))
            ((ItemList<Crumb>)items).Add(new Crumb(label, Href, Icon));
        output.SuppressOutput();
    }
}
