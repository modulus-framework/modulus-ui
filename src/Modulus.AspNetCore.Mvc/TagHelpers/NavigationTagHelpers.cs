using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>Collects child items rendered by a parent tag helper (tabs, steps, panels, …).</summary>
internal sealed class ItemList<T> : List<T>;

/// <summary>
/// Breadcrumb: <c>&lt;app-breadcrumb&gt;&lt;app-breadcrumb-item href="/"&gt;Home&lt;/app-breadcrumb-item&gt;…&lt;/app-breadcrumb&gt;</c>.
/// The last item is the current page.
/// </summary>
[HtmlTargetElement("app-breadcrumb")]
public class BreadcrumbTagHelper : TagHelper
{
    public string Separator { get; set; } = "/";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var items = new ItemList<(string? Href, string? Icon, string Content)>();
        context.Items[typeof(BreadcrumbTagHelper)] = items;
        await output.GetChildContentAsync();

        output.TagName = "nav";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-breadcrumb");
        if (!output.Attributes.ContainsName("aria-label")) output.Attributes.SetAttribute("aria-label", "Breadcrumb");
        var sb = new StringBuilder("<ol>");
        for (var i = 0; i < items.Count; i++)
        {
            var (href, icon, content) = items[i];
            var last = i == items.Count - 1;
            var inner = (icon is null ? "" : AntIcons.Span(icon) + " ") + content;
            var link = href is null || last
                ? $"""<span class="ant-breadcrumb-link"{(last ? " aria-current=\"page\"" : "")}>{inner}</span>"""
                : $"""<span class="ant-breadcrumb-link"><a href="{Html.E(href)}">{inner}</a></span>""";
            sb.Append("<li>").Append(link);
            if (!last) sb.Append($"""<span class="ant-breadcrumb-separator" aria-hidden="true">{Html.E(Separator)}</span>""");
            sb.Append("</li>");
        }
        output.Content.SetHtmlContent(sb.Append("</ol>").ToString());
    }
}

[HtmlTargetElement("app-breadcrumb-item", ParentTag = "app-breadcrumb")]
public class BreadcrumbItemTagHelper : TagHelper
{
    public string? Href { get; set; }
    public string? Icon { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = (await output.GetChildContentAsync()).GetContent();
        if (context.Items.TryGetValue(typeof(BreadcrumbTagHelper), out var items))
            ((ItemList<(string?, string?, string)>)items).Add((Href, Icon, content));
        output.SuppressOutput();
    }
}

/// <summary>
/// Tabs, switched client-side with Alpine (all panes are rendered, so they work with or without htmx):
/// <code>
/// &lt;app-tabs active="orders"&gt;
///   &lt;app-tab key="overview" title="Overview"&gt;…&lt;/app-tab&gt;
///   &lt;app-tab key="orders" title="Orders" hx-get="/orders" hx-trigger="intersect once"&gt;…&lt;/app-tab&gt;
/// &lt;/app-tabs&gt;
/// </code>
/// </summary>
[HtmlTargetElement("app-tabs")]
public class TabsTagHelper : TagHelper
{
    /// <summary>Key of the initially active tab (defaults to the first).</summary>
    public string? Active { get; set; }

    /// <summary>line | card</summary>
    public string Type { get; set; } = "line";

    /// <summary>small | middle | large</summary>
    public string? Size { get; set; }

    /// <summary>Content on the right of the tab bar.</summary>
    public string? ExtraHtml { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var tabs = new ItemList<TabItem>();
        context.Items[typeof(TabsTagHelper)] = tabs;
        await output.GetChildContentAsync();

        var active = Active ?? tabs.FirstOrDefault()?.Key ?? "";
        var id = context.UniqueId;
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-tabs", "ant-tabs-top", Type == "card" ? "ant-tabs-card" : null, Size is null ? null : $"ant-tabs-{Size}", "m-tabs");
        output.Attributes.SetAttribute("x-data", $"modulusTabs('{Html.E(active)}')");

        var nav = new StringBuilder("""<div role="tablist" class="ant-tabs-nav" @keydown.arrow-right.prevent="move(1)" @keydown.arrow-left.prevent="move(-1)"><div class="ant-tabs-nav-wrap"><div class="ant-tabs-nav-list" x-ref="list">""");
        var panes = new StringBuilder("""<div class="ant-tabs-content-holder"><div class="ant-tabs-content ant-tabs-content-top">""");
        foreach (var tab in tabs)
        {
            var on = tab.Key == active;
            var key = Html.E(tab.Key);
            var tabClass = Html.Cx("ant-tabs-tab", on ? "ant-tabs-tab-active" : null, tab.Disabled ? "ant-tabs-tab-disabled" : null);
            var icon = tab.Icon is null ? "" : AntIcons.Span(tab.Icon);
            var tabActive = "{ 'ant-tabs-tab-active': active === '" + key + "' }";
            var paneActive = "{ 'ant-tabs-tabpane-active': active === '" + key + "' }";
            nav.Append($"""<div class="{tabClass}" data-key="{key}" :class="{tabActive}">""")
               .Append($"""<div role="tab" id="{id}-tab-{key}" aria-controls="{id}-panel-{key}" class="ant-tabs-tab-btn" aria-selected="{(on ? "true" : "false")}" :aria-selected="active === '{key}'" tabindex="{(on ? "0" : "-1")}" :tabindex="active === '{key}' ? 0 : -1" """)
               .Append(tab.Disabled ? "aria-disabled=\"true\">" : $"""@click="select('{key}')">""")
               .Append(icon).Append(icon.Length > 0 ? $"<span>{tab.Title}</span>" : tab.Title).Append("</div></div>");
            panes.Append($"""<div role="tabpanel" id="{id}-panel-{key}" aria-labelledby="{id}-tab-{key}" tabindex="0" class="ant-tabs-tabpane{(on ? " ant-tabs-tabpane-active" : "")}" :class="{paneActive}" x-show="active === '{key}'"{(on ? "" : " style=\"display:none\"")}{tab.Attributes}>{tab.Content}</div>""");
        }
        nav.Append("""<div class="ant-tabs-ink-bar ant-tabs-ink-bar-animated" x-ref="ink"></div></div></div>""");
        if (ExtraHtml is not null) nav.Append($"""<div class="ant-tabs-extra-content">{ExtraHtml}</div>""");
        nav.Append("</div>");
        output.Content.SetHtmlContent(nav.Append(panes).Append("</div></div>").ToString());
    }

    internal sealed record TabItem(string Key, string Title, string? Icon, bool Disabled, string Content, string Attributes);
}

[HtmlTargetElement("app-tab", ParentTag = "app-tabs")]
public class TabTagHelper : TagHelper
{
    public string Key { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Icon { get; set; }
    public bool Disabled { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = (await output.GetChildContentAsync()).GetContent();
        var attributes = output.TakeAttributes();
        if (context.Items.TryGetValue(typeof(TabsTagHelper), out var tabs))
            ((ItemList<TabsTagHelper.TabItem>)tabs).Add(new(Key.Length > 0 ? Key : Title, Html.E(Title), Icon, Disabled, content, attributes));
        output.SuppressOutput();
    }
}

/// <summary>
/// Steps: <c>&lt;app-steps current="1"&gt;&lt;app-step title="Account" description="Done" /&gt;…&lt;/app-steps&gt;</c>.
/// </summary>
[HtmlTargetElement("app-steps")]
public class StepsTagHelper : TagHelper
{
    /// <summary>Zero-based index of the current step.</summary>
    public int Current { get; set; }

    /// <summary>Status of the current step: process | error | finish | wait</summary>
    public string Status { get; set; } = "process";

    /// <summary>horizontal | vertical</summary>
    public string Direction { get; set; } = "horizontal";

    /// <summary>default | small</summary>
    public string Size { get; set; } = "default";

    /// <summary>Dotted progress style.</summary>
    public bool ProgressDot { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var steps = new ItemList<(string Title, string? Description, string? Icon, string? Status)>();
        context.Items[typeof(StepsTagHelper)] = steps;
        await output.GetChildContentAsync();

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-steps", $"ant-steps-{Direction}",
            Direction == "horizontal" && !ProgressDot ? "ant-steps-label-horizontal" : "ant-steps-label-vertical",
            Size == "small" ? "ant-steps-small" : null, ProgressDot ? "ant-steps-dot" : null);

        var sb = new StringBuilder();
        for (var i = 0; i < steps.Count; i++)
        {
            var (title, description, icon, own) = steps[i];
            var status = own ?? (i < Current ? "finish" : i == Current ? Status : "wait");
            var iconHtml = ProgressDot
                ? """<span class="ant-steps-icon"><span class="ant-steps-icon-dot"></span></span>"""
                : icon is not null ? $"""<span class="ant-steps-icon">{AntIcons.Span(icon)}</span>"""
                : status == "finish" ? $"""<span class="ant-steps-icon">{AntIcons.Span("check")}</span>"""
                : status == "error" ? $"""<span class="ant-steps-icon">{AntIcons.Span("close")}</span>"""
                : $"""<span class="ant-steps-icon">{i + 1}</span>""";
            sb.Append($"""<div class="{Html.Cx("ant-steps-item", $"ant-steps-item-{status}", i == Current ? "ant-steps-item-active" : null, icon is not null ? "ant-steps-item-custom" : null)}"{(i == Current ? " aria-current=\"step\"" : "")}>""")
              .Append("""<div class="ant-steps-item-container">""")
              .Append(i < steps.Count - 1 ? """<div class="ant-steps-item-tail"></div>""" : "")
              .Append($"""<div class="ant-steps-item-icon">{iconHtml}</div>""")
              .Append($"""<div class="ant-steps-item-content"><div class="ant-steps-item-title">{Html.E(title)}</div>""")
              .Append(description is null ? "" : $"""<div class="ant-steps-item-description">{Html.E(description)}</div>""")
              .Append("</div></div></div>");
        }
        output.Content.SetHtmlContent(sb.ToString());
    }
}

[HtmlTargetElement("app-step", ParentTag = "app-steps", TagStructure = TagStructure.WithoutEndTag)]
public class StepTagHelper : TagHelper
{
    public string Title { get; set; } = "";
    public string? Description { get; set; }
    public string? Icon { get; set; }

    /// <summary>Overrides the status derived from <c>current</c>: wait | process | finish | error</summary>
    public string? Status { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(StepsTagHelper), out var steps))
            ((ItemList<(string, string?, string?, string?)>)steps).Add((Title, Description, Icon, Status));
        output.SuppressOutput();
    }
}

/// <summary>
/// Dropdown menu. The first child is the trigger; items go in <c>&lt;dropdown-menu&gt;</c>:
/// <code>
/// &lt;app-dropdown&gt;
///   &lt;app-button&gt;Actions &lt;app-icon type="down" /&gt;&lt;/app-button&gt;
///   &lt;dropdown-menu&gt;
///     &lt;app-dropdown-item icon="edit" href="/x/edit"&gt;Edit&lt;/app-dropdown-item&gt;
///     &lt;app-dropdown-divider /&gt;
///     &lt;app-dropdown-item icon="delete" danger hx-delete="/x" hx-confirm="Delete?"&gt;Delete&lt;/app-dropdown-item&gt;
///   &lt;/dropdown-menu&gt;
/// &lt;/app-dropdown&gt;
/// </code>
/// </summary>
[HtmlTargetElement("app-dropdown")]
public class DropdownTagHelper : TagHelper
{
    /// <summary>bottomLeft | bottomRight</summary>
    public string Placement { get; set; } = "bottomLeft";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new Dictionary<string, string>();
        context.Items[typeof(DropdownTagHelper)] = slots;
        var trigger = (await output.GetChildContentAsync()).GetContent();
        slots.TryGetValue("dropdown-menu", out var menu);

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-dropdown");
        output.Attributes.SetAttribute("x-data", "dropdown");
        output.Attributes.SetAttribute("@click.outside", "close()");
        output.Attributes.SetAttribute("@keydown.escape", "close()");
        var side = Placement == "bottomRight" ? "m-dropdown-right" : "m-dropdown-left";
        output.Content.SetHtmlContent(
            $"""<div class="m-dropdown-trigger ant-dropdown-trigger" @click="toggle()" :class="open && 'ant-dropdown-open'" aria-haspopup="menu" :aria-expanded="open">{trigger}</div>""" +
            $"""<div class="ant-dropdown ant-dropdown-placement-{Html.E(Placement)} m-dropdown-panel {side}" x-show="open" x-transition.opacity x-cloak @click="close()">""" +
            $"""<ul class="ant-dropdown-menu ant-dropdown-menu-root ant-dropdown-menu-vertical ant-dropdown-menu-light" role="menu">{menu}</ul></div>""");
    }
}

[HtmlTargetElement("dropdown-menu", ParentTag = "app-dropdown")]
public class DropdownMenuTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(DropdownTagHelper), out var slots))
            ((Dictionary<string, string>)slots)["dropdown-menu"] = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}

/// <summary>Dropdown menu item. With <c>href</c> it is a link; any hx-* attributes go on the item.</summary>
[HtmlTargetElement("app-dropdown-item")]
public class DropdownItemTagHelper : TagHelper
{
    public string? Href { get; set; }
    public string? Icon { get; set; }
    public bool Danger { get; set; }
    public bool Disabled { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var text = (await output.GetChildContentAsync()).GetContent();
        var attributes = output.TakeAttributes();
        var icon = Icon is null ? "" : AntIcons.Span(Icon, IconThemeType.Outline, "ant-dropdown-menu-item-icon");
        var body = $"""{icon}<span class="ant-dropdown-menu-title-content">{text}</span>""";
        output.TagName = "li";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("role", "none");
        output.AddClasses("ant-dropdown-menu-item", Danger ? "ant-dropdown-menu-item-danger" : null, Disabled ? "ant-dropdown-menu-item-disabled" : null);
        output.Content.SetHtmlContent(Href is not null && !Disabled
            ? $"""<a role="menuitem" href="{Html.E(Href)}"{attributes}>{body}</a>"""
            : $"""<button type="button" role="menuitem"{(Disabled ? " disabled" : "")}{attributes}>{body}</button>""");
    }
}

[HtmlTargetElement("app-dropdown-divider", TagStructure = TagStructure.WithoutEndTag)]
public class DropdownDividerTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "li";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("role", "separator");
        output.AddClasses("ant-dropdown-menu-item-divider");
    }
}
