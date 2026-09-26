using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

// The rest of the Ant Design set: AutoComplete, Mentions, InputGroup, image groups, Countdown, Comment and Drawer.
// Markup follows AntD Blazor; the interactive ones run on Alpine components in modulus-widgets.js
// (modulusAutoComplete, modulusMentions, modulusImageGroup, modulusCountdown) or reuse modulusModal (Drawer).

/// <summary>
/// Text input with suggestions: <c>&lt;app-auto-complete asp-for="Assignee" options="names" placeholder="…" /&gt;</c>.
/// Matching options (case-insensitive substring) drop down while typing; arrow keys and Enter pick one.
/// </summary>
[HtmlTargetElement("app-auto-complete", TagStructure = TagStructure.WithoutEndTag)]
public class AutoCompleteTagHelper : BoundControlTagHelper
{
    public IEnumerable<string> Options { get; set; } = [];

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, value) = Bound();
        var extra = output.TakeAttributes();
        var list = $"{(id.Length > 0 ? id : context.UniqueId)}-list";
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-auto-complete");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusAutoComplete({Widgets.Config(Options.ToArray())})"));
        output.Attributes.SetAttribute("@click.outside", "open = false");
        output.Content.SetHtmlContent(
            $"""<input class="ant-input" type="search" autocomplete="off" role="combobox" aria-autocomplete="list" aria-controls="{Html.E(list)}" :aria-expanded="open" name="{Html.E(name)}"{Html.Attr("id", id.Length > 0 ? id : null)} value="{Html.E(value)}"{Html.Attr("disabled", Disabled)}{extra}""" +
            """ x-ref="input" @input="filter($event.target.value)" @focus="filter($event.target.value)" @keydown.arrow-down.prevent="move(1)" @keydown.arrow-up.prevent="move(-1)" @keydown.enter="pick($event)" @keydown.escape="open = false" />""" +
            $"""<div class="ant-select-dropdown ant-select-dropdown-placement-bottomLeft m-auto-complete-dropdown" x-show="open && matches.length" x-transition.opacity x-cloak><div id="{Html.E(list)}" role="listbox">""" +
            """<template x-for="(m, i) in matches" :key="m"><div class="ant-select-item ant-select-item-option" :class="i === active && 'ant-select-item-option-active'" role="option" :aria-selected="i === active" @mousedown.prevent="choose(m)" @mouseenter="active = i"><div class="ant-select-item-option-content" x-text="m"></div></div></template>""" +
            "</div></div>");
    }
}

/// <summary>
/// Text area with @-mentions: <c>&lt;app-mentions asp-for="Comment" options="people" rows="3" /&gt;</c>. Each option's
/// Value is inserted after the <c>@</c>; its Text is shown in the list.
/// </summary>
[HtmlTargetElement("app-mentions", TagStructure = TagStructure.WithoutEndTag)]
public class MentionsTagHelper : BoundControlTagHelper
{
    public IEnumerable<SelectListItem> Options { get; set; } = [];
    public int Rows { get; set; } = 1;
    public string Prefix { get; set; } = "@";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, value) = Bound();
        var extra = output.TakeAttributes();
        var options = Options.Select(o => new { value = o.Value, label = o.Text }).ToArray();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-mentions");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusMentions({Widgets.Config(new { options, prefix = Prefix })})"));
        output.Attributes.SetAttribute("@click.outside", "open = false");
        output.Content.SetHtmlContent(
            $"""<div class="ant-mentions"><textarea class="rc-textarea" rows="{Rows}" name="{Html.E(name)}"{Html.Attr("id", id.Length > 0 ? id : null)}{Html.Attr("disabled", Disabled)}{extra}""" +
            $""" x-ref="input" @input="update()" @click="update()" @keydown.arrow-down="move($event, 1)" @keydown.arrow-up="move($event, -1)" @keydown.enter="pick($event)" @keydown.escape="open = false">{Html.E(value)}</textarea></div>""" +
            """<div class="ant-mentions-dropdown ant-mentions-dropdown-placement-bottomLeft m-mentions-dropdown" x-show="open && matches.length" x-transition.opacity x-cloak>""" +
            """<ul class="ant-mentions-dropdown-menu ant-mentions-dropdown-menu-root ant-mentions-dropdown-menu-vertical" role="listbox">""" +
            """<template x-for="(o, i) in matches" :key="o.value"><li class="ant-mentions-dropdown-menu-item" :class="i === active && 'ant-mentions-dropdown-menu-item-active'" role="option" :aria-selected="i === active" @mousedown.prevent="choose(o)" @mouseenter="active = i" x-text="o.label"></li></template>""" +
            "</ul></div>");
    }
}

/// <summary>Joins controls into one field: <c>&lt;app-input-group compact&gt;&lt;app-select …/&gt;&lt;app-input …/&gt;&lt;/app-input-group&gt;</c>.</summary>
[HtmlTargetElement("app-input-group")]
public class InputGroupTagHelper : TagHelper
{
    public bool Compact { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-input-group", Compact ? "ant-input-group-compact" : null, "m-input-group");
        if (Compact) output.AddStyle("display: flex;");
    }
}

internal sealed class ImageGroupContext
{
    public List<(string Src, string Alt)> Images { get; } = [];
}

/// <summary>
/// Groups <c>app-image</c>s so the preview pages through them (arrow keys or buttons):
/// <c>&lt;app-image-group&gt;&lt;app-image …/&gt;&lt;app-image …/&gt;&lt;/app-image-group&gt;</c>.
/// </summary>
[HtmlTargetElement("app-image-group")]
public class ImageGroupTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var group = new ImageGroupContext();
        context.Items[typeof(ImageGroupContext)] = group;
        var body = (await output.GetChildContentAsync()).GetContent();
        var items = group.Images.Select(i => new { src = i.Src, alt = i.Alt }).ToArray();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-image-group");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusImageGroup({Widgets.Config(items)})"));
        output.Content.SetHtmlContent(body +
            """<template x-teleport="body"><div class="m-image-preview" x-show="open" x-transition.opacity @keydown.escape.window="open && close()" @keydown.arrow-left.window="open && go(-1)" @keydown.arrow-right.window="open && go(1)" role="dialog" aria-modal="true" :aria-label="current.alt">""" +
            """<div class="ant-image-preview-mask"></div><div class="ant-image-preview-wrap" @click="close()"><div class="ant-image-preview-img-wrapper">""" +
            """<img class="ant-image-preview-img" :src="current.src" :alt="current.alt" @click.stop /></div></div>""" +
            $"""<button type="button" class="m-image-preview-close" aria-label="Close preview" @click="close()" x-ref="close">{AntIcons.Span("close")}</button>""" +
            $"""<button type="button" class="m-image-preview-switch m-image-preview-prev" aria-label="Previous image" @click="go(-1)">{AntIcons.Span("left")}</button>""" +
            $"""<button type="button" class="m-image-preview-switch m-image-preview-next" aria-label="Next image" @click="go(1)">{AntIcons.Span("right")}</button>""" +
            """<div class="m-image-preview-count" x-text="(index + 1) + ' / ' + items.length"></div></div></template>""");
    }
}

/// <summary>
/// Live countdown in the Statistic style: <c>&lt;app-countdown title="Offer ends in" value="deadline" format="hh:mm:ss" /&gt;</c>.
/// Tokens: <c>d</c> (days), <c>hh</c>, <c>mm</c>, <c>ss</c>; without <c>d</c>, hours run past 24.
/// </summary>
[HtmlTargetElement("app-countdown", TagStructure = TagStructure.WithoutEndTag)]
public class CountdownTagHelper : TagHelper
{
    public string? Title { get; set; }
    public DateTime Value { get; set; }
    public string Format { get; set; } = "hh:mm:ss";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var end = new DateTimeOffset(Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(Value, DateTimeKind.Local) : Value).ToUnixTimeMilliseconds();
        var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-statistic");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusCountdown({end.ToString(CultureInfo.InvariantCulture)}, '{Html.E(Format)}')"));
        output.Content.SetHtmlContent(
            (Title is null ? "" : $"""<div class="ant-statistic-title">{Html.E(Title)}</div>""") +
            $"""<div class="ant-statistic-content"><span class="ant-statistic-content-value" x-text="text" role="timer">{Html.E(FormatSpan(Math.Max(0, end - now), Format))}</span></div>""");
    }

    /// <summary>Formats the time left; mirrored by modulusCountdown in modulus-widgets.js.</summary>
    public static string FormatSpan(long ms, string format)
    {
        var total = ms / 1000;
        var days = total / 86400;
        var hours = format.Contains('d') ? total / 3600 % 24 : total / 3600;
        var result = format.Replace("hh", hours.ToString("00", CultureInfo.InvariantCulture))
                           .Replace("mm", (total / 60 % 60).ToString("00", CultureInfo.InvariantCulture))
                           .Replace("ss", (total % 60).ToString("00", CultureInfo.InvariantCulture));
        return result.Replace("d", days.ToString(CultureInfo.InvariantCulture));
    }
}

internal sealed class CommentSlots
{
    public List<string> Actions { get; } = [];
}

/// <summary>
/// A comment in a thread: <c>&lt;app-comment author="Liam Okafor" datetime="2 hours ago" avatar="LO" avatar-color="#52c41a"
/// content="…"&gt;</c>. <c>comment-action</c> children render under it; nested <c>app-comment</c>s are replies.
/// </summary>
[HtmlTargetElement("app-comment")]
public class CommentTagHelper : TagHelper
{
    public string Author { get; set; } = "";
    public string? Datetime { get; set; }
    public string Content { get; set; } = "";

    /// <summary>Initials (or text) for the avatar.</summary>
    public string? Avatar { get; set; }
    public string? AvatarColor { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new CommentSlots();
        context.Items[typeof(CommentSlots)] = slots;
        var replies = (await output.GetChildContentAsync()).GetContent();
        var avatar = Avatar is null ? "" :
            $"""<div class="ant-comment-avatar"><span class="ant-avatar"{(AvatarColor is null ? "" : $" style=\"background: {Html.E(AvatarColor)};\"")}><span class="ant-avatar-string" style="transform: scale(1) translateX(-50%);">{Html.E(Avatar)}</span></span></div>""";
        var time = Datetime is null ? "" : $"""<span class="ant-comment-content-author-time"><span>{Html.E(Datetime)}</span></span>""";
        var actions = slots.Actions.Count == 0 ? "" : $"""<ul class="ant-comment-actions">{string.Concat(slots.Actions.Select(a => $"<li>{a}</li>"))}</ul>""";
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-comment");
        output.Content.SetHtmlContent(
            $"""<div class="ant-comment-inner">{avatar}<div class="ant-comment-content"><div class="ant-comment-content-author"><span class="ant-comment-content-author-name"><a>{Html.E(Author)}</a></span>{time}</div>""" +
            $"""<div class="ant-comment-content-detail"><p>{Html.E(Content)}</p></div>{actions}</div></div>""" +
            $"""<div class="ant-comment-nested">{replies}</div>""");
    }
}

[HtmlTargetElement("comment-action", ParentTag = "app-comment")]
public class CommentActionTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var body = (await output.GetChildContentAsync()).GetContent();
        if (context.Items.TryGetValue(typeof(CommentSlots), out var slots)) ((CommentSlots)slots).Actions.Add($"<span>{body}</span>");
        output.SuppressOutput();
    }
}

/// <summary>
/// Panel that slides in from an edge. Open it like <c>app-modal</c>: <c>$dispatch('modulus:modal-open', 'invoice')</c>
/// or <c>modulusModal.open('invoice')</c>.
/// <code>&lt;app-drawer name="invoice" title="Invoice INV-1042" placement="right" width="360"&gt;…&lt;/app-drawer&gt;</code>
/// </summary>
[HtmlTargetElement("app-drawer")]
public class DrawerTagHelper : TagHelper
{
    public string Name { get; set; } = "";
    public string? Title { get; set; }

    /// <summary>right | left | top | bottom</summary>
    public string Placement { get; set; } = "right";

    /// <summary>Width (left/right) in pixels.</summary>
    public int Width { get; set; } = 378;

    /// <summary>Height (top/bottom) in pixels.</summary>
    public int Height { get; set; } = 378;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var body = (await output.GetChildContentAsync()).GetContent();
        var side = Placement is "left" or "top" or "bottom" ? Placement : "right";
        var size = side is "top" or "bottom" ? $"height: {Height}px; max-height: 100vh" : $"width: {Width}px; max-width: 100vw";
        var titleId = $"{context.UniqueId}-title";
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-drawer", $"ant-drawer-{side}", "ant-drawer-open", "m-drawer");
        output.Attributes.SetAttribute("x-data", $"modulusModal('{Html.E(Name)}')");
        output.Attributes.SetAttribute("x-show", "open");
        output.Attributes.SetAttribute(":data-open", "open");
        output.Attributes.SetAttribute("x-cloak", "");
        output.Attributes.SetAttribute("@keydown.escape.window", "open && hide()");
        output.Attributes.SetAttribute("role", "dialog");
        output.Attributes.SetAttribute("aria-modal", "true");
        if (Title is not null) output.Attributes.SetAttribute("aria-labelledby", titleId);
        output.Content.SetHtmlContent(
            """<div class="ant-drawer-mask" @click="hide()" x-show="open" x-transition.opacity></div>""" +
            $"""<div class="ant-drawer-content-wrapper" style="{size}" x-show="open" x-transition:enter="m-drawer-enter" x-transition:enter-start="m-drawer-hidden-{side}" x-transition:leave="m-drawer-enter" x-transition:leave-end="m-drawer-hidden-{side}">""" +
            """<div class="ant-drawer-content"><div class="ant-drawer-wrapper-body"><div class="ant-drawer-header">""" +
            $"""<div class="ant-drawer-title" id="{titleId}">{Html.E(Title)}</div><button type="button" class="ant-drawer-close" aria-label="Close" @click="hide()">{AntIcons.Span("close")}</button></div>""" +
            $"""<div class="ant-drawer-body">{body}</div></div></div></div>""");
    }
}
