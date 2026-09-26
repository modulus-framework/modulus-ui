using System.Globalization;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

// Page frame and scroll helpers: Layout (header, sider, content, footer), Splitter, Affix, BackTop and the
// dropdown button. Each emits the DOM AntD Blazor renders; Splitter, Affix and BackTop get their behaviour from
// small Alpine components in modulus-widgets.js.

internal sealed class LayoutContext
{
    public bool HasSider { get; set; }
}

/// <summary>Page frame: <c>&lt;app-layout&gt;</c> with <c>app-layout-header</c>, <c>-sider</c>, <c>-content</c>, <c>-footer</c>.</summary>
[HtmlTargetElement("app-layout")]
public class LayoutTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var layout = new LayoutContext();
        context.Items[typeof(LayoutContext)] = layout;
        var body = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "section";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-layout", layout.HasSider ? "ant-layout-has-sider" : null);
        output.Content.SetHtmlContent(body);
    }
}

[HtmlTargetElement("app-layout-header")]
public class LayoutHeaderTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "header";
        output.AddClasses("ant-layout-header", "ant-header");
    }
}

[HtmlTargetElement("app-layout-content")]
public class LayoutContentTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "main";
        output.AddClasses("ant-layout-content");
    }
}

[HtmlTargetElement("app-layout-footer")]
public class LayoutFooterTagHelper : TagHelper
{
    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "footer";
        output.AddClasses("ant-layout-footer");
    }
}

[HtmlTargetElement("app-layout-sider")]
public class LayoutSiderTagHelper : TagHelper
{
    public int Width { get; set; } = 200;

    /// <summary>light | dark</summary>
    public string Theme { get; set; } = "dark";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(LayoutContext), out var layout)) ((LayoutContext)layout).HasSider = true;
        var body = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "aside";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-layout-sider", $"ant-layout-sider-{Html.E(Theme)}");
        output.AddStyle($"flex: 0 0 {Width}px;max-width: {Width}px;min-width: {Width}px;width: {Width}px;");
        output.Content.SetHtmlContent($"""<div class="ant-layout-sider-children">{body}</div>""");
    }
}

internal sealed class SplitterContext
{
    public List<(string Body, string? Size, string? Min, string? Max)> Panels { get; } = [];
}

/// <summary>
/// Two resizable panes: <c>&lt;app-splitter layout="horizontal|vertical"&gt;</c> with two
/// <c>&lt;app-splitter-panel size="40%" min="20%" max="70%"&gt;</c> children. Drag the bar (or use the arrow keys on it).
/// </summary>
[HtmlTargetElement("app-splitter")]
public class SplitterTagHelper : TagHelper
{
    /// <summary>horizontal (side by side) | vertical (stacked)</summary>
    public string Layout { get; set; } = "horizontal";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var splitter = new SplitterContext();
        context.Items[typeof(SplitterContext)] = splitter;
        await output.GetChildContentAsync();
        var vertical = Layout == "vertical";
        var dim = vertical ? "height" : "width";
        var first = splitter.Panels.ElementAtOrDefault(0);
        var second = splitter.Panels.ElementAtOrDefault(1);
        var start = Percent(first.Size) ?? 50;
        var min = Percent(first.Min) ?? 0;
        var max = Percent(first.Max) ?? 100;

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        // AntD Blazor names the orientation of the bar, not of the layout: a horizontal splitter has a vertical bar.
        output.AddClasses("split-container", vertical ? "splitter-orientation-horizontal" : "splitter-orientation-vertical", "ant-splitter", vertical ? "ant-splitter-vertical" : "ant-splitter-horizontal");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusSplitter({Widgets.Config(new { vertical, size = start, min, max })})"));
        var inv = CultureInfo.InvariantCulture;
        var minStyle = first.Min is null ? "" : $"min-{dim}:{Html.E(first.Min)};";
        output.Content.SetHtmlContent(
            $"""<div class="pane-of-split-container ant-splitter-panel" style="{minStyle}{dim}:{start.ToString("0.##", inv)}%;" :style="first">{first.Body}</div>""" +
            $"""<div class="ant-splitter-bar" role="separator" tabindex="0" aria-orientation="{(vertical ? "horizontal" : "vertical")}" aria-valuemin="{min.ToString("0", inv)}" aria-valuemax="{max.ToString("0", inv)}" aria-valuenow="{start.ToString("0", inv)}" :aria-valuenow="Math.round(size)" @pointerdown="start($event)" @keydown.arrow-left.prevent="nudge(-5)" @keydown.arrow-up.prevent="nudge(-5)" @keydown.arrow-right.prevent="nudge(5)" @keydown.arrow-down.prevent="nudge(5)"><div class="ant-splitter-bar-dragger spliter-bar "></div></div>""" +
            $"""<div class="pane-of-split-container ant-splitter-panel" style="{dim}:{(100 - start).ToString("0.##", inv)}%;" :style="second">{second.Body}</div>""");
    }

    private static decimal? Percent(string? value) =>
        value is not null && decimal.TryParse(value.TrimEnd('%'), NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : null;
}

[HtmlTargetElement("app-splitter-panel", ParentTag = "app-splitter")]
public class SplitterPanelTagHelper : TagHelper
{
    /// <summary>Initial size of the first panel, in percent (e.g. <c>40%</c>).</summary>
    public string? Size { get; set; }
    public string? Min { get; set; }
    public string? Max { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var body = (await output.GetChildContentAsync()).GetContent();
        if (context.Items.TryGetValue(typeof(SplitterContext), out var s)) ((SplitterContext)s).Panels.Add((body, Size, Min, Max));
        output.SuppressOutput();
    }
}

/// <summary>Keeps its content in view once the page scrolls past it: <c>&lt;app-affix offset-top="120"&gt;</c>.</summary>
[HtmlTargetElement("app-affix")]
public class AffixTagHelper : TagHelper
{
    public int OffsetTop { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var body = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusAffix({OffsetTop})"));
        output.Attributes.SetAttribute(":style", "placeholder");
        output.Content.SetHtmlContent($"""<div class="" :class="fixed && 'ant-affix'" :style="style" x-ref="inner">{body}</div>""");
    }
}

/// <summary>Floating "back to top" button, shown once the page has scrolled: <c>&lt;app-back-top /&gt;</c>.</summary>
[HtmlTargetElement("app-back-top", TagStructure = TagStructure.WithoutEndTag)]
public class BackTopTagHelper : TagHelper
{
    public int VisibilityHeight { get; set; } = 400;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-back-top");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusBackTop({VisibilityHeight})"));
        output.Content.SetHtmlContent(
            $"""<div><button type="button" class="ant-back-top-content m-back-top-button" x-show="visible" x-transition.opacity x-cloak @click="top()" aria-label="Back to top"><div class="ant-back-top-icon">{AntIcons.Span("vertical-align-top")}</div></button></div>""");
    }
}

/// <summary>
/// A button with a menu of alternatives: <c>&lt;app-dropdown-button text="Save"&gt;&lt;app-dropdown-item&gt;…&lt;/app-dropdown-button&gt;</c>.
/// Unbound attributes (<c>hx-post</c>, <c>name</c>, <c>value</c>, <c>type</c> …) go on the main button.
/// </summary>
[HtmlTargetElement("app-dropdown-button")]
public class DropdownButtonTagHelper : TagHelper
{
    public string Text { get; set; } = "";

    /// <summary>default | primary</summary>
    [HtmlAttributeName("button-type")]
    public string ButtonType { get; set; } = "default";

    public string Icon { get; set; } = "ellipsis";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var menu = (await output.GetChildContentAsync()).GetContent();
        var extra = output.TakeAttributes();
        if (!extra.Contains(" type=", StringComparison.Ordinal)) extra = " type=\"button\"" + extra;
        var btn = $"ant-btn ant-btn-{Html.E(ButtonType)}";
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddStyle("display: inline-block; width: fit-content;");
        output.Content.SetHtmlContent(
            $"""<div class="m-dropdown" x-data="dropdown" @click.outside="close()" @keydown.escape="close()">""" +
            $"""<div class="ant-dropdown-button ant-btn-group"><button class="{btn}"{extra}><span>{Html.E(Text)}</span></button>""" +
            $"""<span><button type="button" class="ant-dropdown-trigger {btn} ant-btn-icon-only" @click="toggle()" :class="open && 'ant-dropdown-open'" aria-haspopup="menu" :aria-expanded="open" aria-label="More options">{AntIcons.Span(Icon)}</button></span></div>""" +
            """<div class="ant-dropdown ant-dropdown-placement-bottomRight m-dropdown-panel m-dropdown-right" x-show="open" x-transition.opacity x-cloak @click="close()">""" +
            $"""<ul class="ant-dropdown-menu ant-dropdown-menu-root ant-dropdown-menu-vertical ant-dropdown-menu-light" role="menu">{menu}</ul></div></div>""");
    }
}
