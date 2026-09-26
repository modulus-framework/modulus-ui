using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>Overlapping avatars: <c>&lt;app-avatar-group max-count="3"&gt;&lt;app-avatar name="…" /&gt;…&lt;/app-avatar-group&gt;</c>.</summary>
[HtmlTargetElement("app-avatar-group")]
public partial class AvatarGroupTagHelper : TagHelper
{
    /// <summary>Show at most this many avatars, then a "+N" avatar.</summary>
    public int? MaxCount { get; set; }

    /// <summary>small | default | large</summary>
    public string Size { get; set; } = "default";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var html = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-avatar-group");
        var avatars = AvatarSplit().Split(html).Where(a => !string.IsNullOrWhiteSpace(a)).ToList();
        if (MaxCount is { } max && avatars.Count > max)
        {
            var sizeClass = Size switch { "small" => " ant-avatar-sm", "large" => " ant-avatar-lg", _ => "" };
            html = string.Concat(avatars.Take(max)) +
                   $"""<span class="ant-avatar ant-avatar-circle{sizeClass} m-avatar-more"><span class="ant-avatar-string">+{avatars.Count - max}</span></span>""";
        }
        output.Content.SetHtmlContent(html);
    }

    // Each <app-avatar> renders one top-level <span class="ant-avatar …">…</span>; split before each one.
    [GeneratedRegex(@"(?=<span class=""ant-avatar )")]
    private static partial Regex AvatarSplit();
}

/// <summary>
/// Headline number: <c>&lt;app-statistic title="Active users" value="112893" /&gt;</c>,
/// <c>precision="1" suffix="%" prefix-icon="arrow-up" value-color="var(--m-success)"</c>.
/// </summary>
[HtmlTargetElement("app-statistic", TagStructure = TagStructure.WithoutEndTag)]
public class StatisticTagHelper : TagHelper
{
    public string? Title { get; set; }

    /// <summary>A number (formatted with group separators) or any text.</summary>
    public string? Value { get; set; }

    public int? Precision { get; set; }
    public string? Prefix { get; set; }
    public string? PrefixIcon { get; set; }
    public string? Suffix { get; set; }
    public string? ValueColor { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        string inner;
        if (decimal.TryParse(Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var number))
        {
            var text = Precision is { } p ? number.ToString($"N{p}", CultureInfo.CurrentCulture) : number.ToString("#,0.##########", CultureInfo.CurrentCulture);
            var sep = CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator;
            var i = text.IndexOf(sep, StringComparison.Ordinal);
            inner = i < 0
                ? $"""<span class="ant-statistic-content-value-int">{Html.E(text)}</span>"""
                : $"""<span class="ant-statistic-content-value-int">{Html.E(text[..i])}</span><span class="ant-statistic-content-value-decimal">{Html.E(text[i..])}</span>""";
        }
        else
        {
            inner = Html.E(Value);
        }
        var prefix = PrefixIcon is not null ? AntIcons.Span(PrefixIcon) : Prefix is not null ? Html.E(Prefix) : null;
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-statistic");
        output.Content.SetHtmlContent(
            (Title is null ? "" : $"""<div class="ant-statistic-title">{Html.E(Title)}</div>""") +
            $"""<div class="ant-statistic-content"{(ValueColor is null ? "" : $" style=\"color:{Html.E(ValueColor)}\"")}>""" +
            (prefix is null ? "" : $"""<span class="ant-statistic-content-prefix">{prefix}</span>""") +
            $"""<span class="ant-statistic-content-value">{inner}</span>""" +
            (Suffix is null ? "" : $"""<span class="ant-statistic-content-suffix">{Html.E(Suffix)}</span>""") + "</div>");
    }
}

/// <summary>
/// Read-only details of one record:
/// <code>
/// &lt;app-descriptions title="Customer" bordered column="2"&gt;
///   &lt;app-descriptions-item label="Name"&gt;Acme&lt;/app-descriptions-item&gt;
///   &lt;app-descriptions-item label="Address" span="2"&gt;…&lt;/app-descriptions-item&gt;
/// &lt;/app-descriptions&gt;
/// </code>
/// </summary>
[HtmlTargetElement("app-descriptions")]
public class DescriptionsTagHelper : TagHelper
{
    public string? Title { get; set; }
    public bool Bordered { get; set; }
    public int Column { get; set; } = 3;

    /// <summary>default | middle | small</summary>
    public string Size { get; set; } = "default";

    /// <summary>horizontal | vertical</summary>
    public string Layout { get; set; } = "horizontal";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new DescriptionsSlots();
        context.Items[typeof(DescriptionsTagHelper)] = slots;
        await output.GetChildContentAsync();

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-descriptions", Bordered ? "ant-descriptions-bordered" : null, Size == "default" ? null : $"ant-descriptions-{Size}");

        // Fill rows left to right; the last item of a row stretches to the end, as in Ant Design.
        var rows = new List<List<(string Label, string Content, int Span)>>();
        var row = new List<(string, string, int)>();
        var used = 0;
        foreach (var item in slots.Items)
        {
            var span = Math.Min(Math.Max(1, item.Span), Column);
            if (used + span > Column && row.Count > 0) { rows.Add(row); row = []; used = 0; }
            row.Add((item.Label, item.Content, span));
            used += span;
        }
        if (row.Count > 0) rows.Add(row);

        var sb = new StringBuilder();
        if (Title is not null || slots.Extra is not null)
            sb.Append($"""<div class="ant-descriptions-header"><div class="ant-descriptions-title">{Html.E(Title)}</div>""")
              .Append(slots.Extra is null ? "" : $"""<div class="ant-descriptions-extra">{slots.Extra}</div>""").Append("</div>");
        sb.Append("""<div class="ant-descriptions-view"><table><tbody>""");
        foreach (var r in rows)
        {
            var filled = r.Sum(i => i.Span);
            if (filled < Column) r[^1] = r[^1] with { Span = r[^1].Span + Column - filled };
            if (Layout == "vertical")
            {
                sb.Append("""<tr class="ant-descriptions-row">""");
                foreach (var (label, _, span) in r)
                    sb.Append(Bordered
                        ? $"""<th class="ant-descriptions-item-label" colspan="{span}">{Html.E(label)}</th>"""
                        : $"""<th class="ant-descriptions-item" colspan="{span}"><div class="ant-descriptions-item-container"><span class="ant-descriptions-item-label">{Html.E(label)}</span></div></th>""");
                sb.Append("""</tr><tr class="ant-descriptions-row">""");
                foreach (var (_, content, span) in r)
                    sb.Append(Bordered
                        ? $"""<td class="ant-descriptions-item-content" colspan="{span}">{content}</td>"""
                        : $"""<td class="ant-descriptions-item" colspan="{span}"><div class="ant-descriptions-item-container"><span class="ant-descriptions-item-content">{content}</span></div></td>""");
                sb.Append("</tr>");
                continue;
            }
            sb.Append("""<tr class="ant-descriptions-row">""");
            foreach (var (label, content, span) in r)
                sb.Append(Bordered
                    ? $"""<th class="ant-descriptions-item-label" scope="row">{Html.E(label)}</th><td class="ant-descriptions-item-content" colspan="{span * 2 - 1}">{content}</td>"""
                    : $"""<td class="ant-descriptions-item" colspan="{span}"><div class="ant-descriptions-item-container"><span class="ant-descriptions-item-label">{Html.E(label)}</span><span class="ant-descriptions-item-content">{content}</span></div></td>""");
            sb.Append("</tr>");
        }
        output.Content.SetHtmlContent(sb.Append("</tbody></table></div>").ToString());
    }

    internal sealed class DescriptionsSlots
    {
        public List<(string Label, string Content, int Span)> Items { get; } = [];
        public string? Extra { get; set; }
    }
}

[HtmlTargetElement("app-descriptions-item", ParentTag = "app-descriptions")]
public class DescriptionsItemTagHelper : TagHelper
{
    public string Label { get; set; } = "";
    public int Span { get; set; } = 1;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = (await output.GetChildContentAsync()).GetContent();
        if (context.Items.TryGetValue(typeof(DescriptionsTagHelper), out var slots))
            ((DescriptionsTagHelper.DescriptionsSlots)slots).Items.Add((Label, content, Span));
        output.SuppressOutput();
    }
}

[HtmlTargetElement("descriptions-extra", ParentTag = "app-descriptions")]
public class DescriptionsExtraTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(DescriptionsTagHelper), out var slots))
            ((DescriptionsTagHelper.DescriptionsSlots)slots).Extra = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}

/// <summary>
/// Events in time order:
/// <code>&lt;app-timeline&gt;&lt;app-timeline-item color="green" label="09:12"&gt;Paid&lt;/app-timeline-item&gt;…&lt;/app-timeline&gt;</code>
/// </summary>
[HtmlTargetElement("app-timeline")]
public class TimelineTagHelper : TagHelper
{
    /// <summary>Last item shows a spinner with this text (work in progress).</summary>
    public string? Pending { get; set; }

    public bool Reverse { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var items = new ItemList<string>();
        context.Items[typeof(TimelineTagHelper)] = items;
        await output.GetChildContentAsync();
        if (Reverse) items.Reverse();
        if (Pending is not null)
            items.Add($"""<li class="ant-timeline-item ant-timeline-item-pending"><div class="ant-timeline-item-tail"></div><div class="ant-timeline-item-head ant-timeline-item-head-custom ant-timeline-item-head-blue">{AntIcons.Span("loading", IconThemeType.Outline, "anticon-spin")}</div><div class="ant-timeline-item-content">{Html.E(Pending)}</div></li>""");

        output.TagName = "ul";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-timeline", Pending is null ? null : "ant-timeline-pending", Reverse ? "ant-timeline-reverse" : null);
        var sb = new StringBuilder();
        for (var i = 0; i < items.Count; i++)
            sb.Append(i == items.Count - 1 ? MarkLast(items[i]) : items[i]);
        output.Content.SetHtmlContent(sb.ToString());
    }

    // Every item starts with <li class="ant-timeline-item…"; tag the last one so its tail is hidden.
    private static string MarkLast(string item) => item.Insert(item.IndexOf('"') + 1, "ant-timeline-item-last ");
}

[HtmlTargetElement("app-timeline-item", ParentTag = "app-timeline")]
public class TimelineItemTagHelper : TagHelper
{
    private static readonly HashSet<string> Presets = ["blue", "red", "green", "gray"];

    /// <summary>blue | red | green | gray | any CSS colour</summary>
    public string Color { get; set; } = "blue";

    /// <summary>Icon instead of the dot.</summary>
    public string? Icon { get; set; }

    /// <summary>Text shown before the item (time, date).</summary>
    public string? Label { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var content = (await output.GetChildContentAsync()).GetContent();
        var preset = Presets.Contains(Color);
        var head = Html.Cx("ant-timeline-item-head", preset ? $"ant-timeline-item-head-{Color}" : null, Icon is null ? null : "ant-timeline-item-head-custom");
        var style = preset ? "" : $" style=\"border-color:{Html.E(Color)};color:{Html.E(Color)}\"";
        var label = Label is null ? "" : $"""<div class="ant-timeline-item-label m-timeline-label">{Html.E(Label)}</div>""";
        var html = $"""<li class="ant-timeline-item">{label}<div class="ant-timeline-item-tail"></div><div class="{head}"{style}>{(Icon is null ? "" : AntIcons.Span(Icon))}</div><div class="ant-timeline-item-content">{content}</div></li>""";
        if (context.Items.TryGetValue(typeof(TimelineTagHelper), out var items))
            ((ItemList<string>)items).Add(html);
        output.SuppressOutput();
    }
}

/// <summary>
/// Expandable panels:
/// <code>
/// &lt;app-collapse active="billing" accordion&gt;
///   &lt;app-panel key="billing" header="How is billing calculated?"&gt;…&lt;/app-panel&gt;
/// &lt;/app-collapse&gt;
/// </code>
/// </summary>
[HtmlTargetElement("app-collapse")]
public class CollapseTagHelper : TagHelper
{
    /// <summary>Comma-separated keys of the initially open panels.</summary>
    public string? Active { get; set; }

    /// <summary>Only one panel open at a time.</summary>
    public bool Accordion { get; set; }

    public bool Bordered { get; set; } = true;

    /// <summary>Transparent, borderless panels.</summary>
    public bool Ghost { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var open = (Active ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToHashSet();
        context.Items[typeof(CollapseTagHelper)] = open;
        var body = (await output.GetChildContentAsync()).GetContent();
        var keys = string.Join(", ", open.Select(k => $"'{Html.E(k)}'"));
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-collapse", "ant-collapse-icon-position-left", Bordered && !Ghost ? null : "ant-collapse-borderless", Ghost ? "ant-collapse-ghost" : null, "m-collapse");
        output.Attributes.SetAttribute("x-data", $"modulusCollapse([{keys}], {(Accordion ? "true" : "false")})");
        output.Content.SetHtmlContent(body);
    }
}

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

[HtmlTargetElement("app-list-item")]
public class ListItemTagHelper : TagHelper
{
    public string? Title { get; set; }
    public string? Description { get; set; }

    /// <summary>Makes the title a link.</summary>
    public string? Href { get; set; }

    public string? AvatarSrc { get; set; }
    public string? AvatarName { get; set; }
    public string? AvatarIcon { get; set; }
    public string? AvatarColor { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new Dictionary<string, string>();
        context.Items[typeof(ListItemTagHelper)] = slots;
        var content = (await output.GetChildContentAsync()).GetContent();

        string? avatar = null;
        if (AvatarSrc is not null || AvatarName is not null || AvatarIcon is not null)
        {
            var style = AvatarColor is null ? "" : $" style=\"background:{Html.E(AvatarColor)}\"";
            avatar = AvatarSrc is not null
                ? $"""<span class="ant-avatar ant-avatar-circle ant-avatar-image"><img src="{Html.E(AvatarSrc)}" alt="" /></span>"""
                : AvatarIcon is not null
                    ? $"""<span class="ant-avatar ant-avatar-circle ant-avatar-icon"{style}>{AntIcons.Span(AvatarIcon)}</span>"""
                    : $"""<span class="ant-avatar ant-avatar-circle"{style}><span class="ant-avatar-string">{Html.E(Initials(AvatarName!))}</span></span>""";
        }
        var title = Title is null ? null : Href is null ? Html.E(Title) : $"""<a href="{Html.E(Href)}">{Html.E(Title)}</a>""";
        var meta = title is null && Description is null && avatar is null ? "" :
            """<div class="ant-list-item-meta">""" +
            (avatar is null ? "" : $"""<div class="ant-list-item-meta-avatar">{avatar}</div>""") +
            """<div class="ant-list-item-meta-content">""" +
            (title is null ? "" : $"""<h4 class="ant-list-item-meta-title">{title}</h4>""") +
            (Description is null ? "" : $"""<div class="ant-list-item-meta-description">{Html.E(Description)}</div>""") + "</div></div>";
        var actions = slots.TryGetValue("list-item-actions", out var a) ? $"""<ul class="ant-list-item-action"><li>{a}</li></ul>""" : "";

        output.TagName = "li";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-list-item");
        output.Content.SetHtmlContent(meta + content + actions);
    }

    internal static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));
}

[HtmlTargetElement("list-item-actions", ParentTag = "app-list-item")]
public class ListItemActionsTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(ListItemTagHelper), out var slots))
            ((Dictionary<string, string>)slots)["list-item-actions"] = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}

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

/// <summary>
/// Wraps a server-rendered <c>&lt;table&gt;</c> in Ant Design's table chrome and styles its cells:
/// <code>&lt;app-table size="small"&gt;&lt;thead&gt;…&lt;/thead&gt;&lt;tbody&gt;…&lt;/tbody&gt;&lt;/app-table&gt;</code>
/// <para>
/// Column picker: mark optional <c>&lt;col&gt;</c>, <c>&lt;th&gt;</c> and <c>&lt;td&gt;</c> elements with
/// <c>data-col="key"</c> and pass <c>hidden-columns</c>; hidden ones are left out (see <see cref="TableColumnsTagHelper"/>).
/// <c>density</c> binds the size to the toolbar's density menu (<see cref="TableSettingsTagHelper"/>).
/// A <c>&lt;table-footer&gt;</c> child (e.g. the pager) renders under the table.
/// </para>
/// </summary>
[HtmlTargetElement("app-table")]
public partial class TableTagHelper : TagHelper
{
    /// <summary>default | middle | small</summary>
    public string Size { get; set; } = "default";

    public bool Bordered { get; set; }

    /// <summary>Shown when the body has no rows.</summary>
    public string EmptyText { get; set; } = "No data";

    /// <summary>Accessible table caption (visually hidden).</summary>
    public string? Caption { get; set; }

    /// <summary>Keys of <c>data-col</c> columns to leave out.</summary>
    public IEnumerable<string>? HiddenColumns { get; set; }

    /// <summary>Bind the size to the density menu of the enclosing <c>tableSelection</c> scope.</summary>
    public bool Density { get; set; }

    /// <summary>Table width in pixels; wider tables scroll horizontally inside the card.</summary>
    public int? ScrollX { get; set; }

    /// <summary>auto | fixed (fixed honours &lt;col&gt; widths).</summary>
    public string Layout { get; set; } = "auto";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new Dictionary<string, string>();
        context.Items[typeof(TableTagHelper)] = slots;
        var html = (await output.GetChildContentAsync()).GetContent();
        slots.TryGetValue("table-footer", out var footer);
        foreach (var key in HiddenColumns ?? [])
            html = HiddenColumn(key).Replace(html, "");
        html = Unclassed().Replace(html, m => m.Groups[1].Value switch
        {
            "thead" => """<thead class="ant-table-thead" """,
            "tbody" => """<tbody class="ant-table-tbody" """,
            "tfoot" => """<tfoot class="ant-table-summary" """,
            "th" or "td" => $"""<{m.Groups[1].Value} class="ant-table-cell" """,
            _ => m.Value,
        });
        html = BodyRows().Replace(html, m => BodyRow().Replace(m.Value, """<tr class="ant-table-row" """));
        if (EmptyBody().IsMatch(html))
        {
            var cols = Math.Max(1, HeaderCells().Matches(html).Count);
            html = EmptyBody().Replace(html, $"""<tbody class="ant-table-tbody"><tr class="ant-table-placeholder"><td class="ant-table-cell" colspan="{cols}">{EmptyTagHelper.Render(EmptyText, "ant-empty-small")}</td></tr></tbody>""");
        }
        var caption = Caption is null ? "" : $"""<caption class="m-sr-only">{Html.E(Caption)}</caption>""";
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-table-wrapper");
        var table = Html.Cx("ant-table", Size == "default" || Density ? null : $"ant-table-{Size}", Bordered ? "ant-table-bordered" : null, ScrollX is null ? null : "ant-table-scroll-horizontal");
        var content = ScrollX is null ? "" : """ style="overflow: auto hidden;" """.TrimEnd();
        var tableStyle = (ScrollX is { } x ? $"width: {x}px; min-width: 100%; " : "") + $"table-layout: {(Layout == "fixed" ? "fixed" : "auto")};";
        output.Content.SetHtmlContent(
            $"""<div class="ant-spin-nested-loading"><div class="ant-spin-container"><div class="{table}"{(Density ? " :class=\"density\"" : "")}><div class="ant-table-container"><div class="ant-table-content"{content}><table style="{tableStyle}">{caption}{html}</table></div></div></div>{footer}</div></div>""");
    }

    private static Regex HiddenColumn(string key)
    {
        var k = Regex.Escape(Html.E(key));
        return new Regex($"""<col(?=[\s/>])[^>]*\sdata-col="{k}"[^>]*>|<(th|td)(?=[\s>])[^>]*\sdata-col="{k}"[^>]*>.*?</\1>""", RegexOptions.Singleline);
    }

    [GeneratedRegex(@"<(thead|tbody|tfoot|th|td)(?=[\s>])(?![^>]*\sclass=)\s?")]
    private static partial Regex Unclassed();

    [GeneratedRegex(@"<tbody[^>]*>.*?</tbody>", RegexOptions.Singleline)]
    private static partial Regex BodyRows();

    [GeneratedRegex(@"<tr(?=[\s>])(?![^>]*\sclass=)\s?")]
    private static partial Regex BodyRow();

    [GeneratedRegex(@"<tbody class=""ant-table-tbody""\s*>\s*</tbody>")]
    private static partial Regex EmptyBody();

    [GeneratedRegex(@"<th[\s>]")]
    private static partial Regex HeaderCells();
}

/// <summary>Content under an <c>app-table</c>, inside its chrome (the pager goes here).</summary>
[HtmlTargetElement("table-footer", ParentTag = "app-table")]
public class TableFooterTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(TableTagHelper), out var slots))
            ((Dictionary<string, string>)slots)["table-footer"] = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}

/// <summary>
/// Image with a click-to-zoom preview: <c>&lt;app-image src="/img/product.jpg" alt="Desk lamp" width="160" /&gt;</c>.
/// </summary>
[HtmlTargetElement("app-image", TagStructure = TagStructure.WithoutEndTag)]
public class ImageTagHelper : TagHelper
{
    public string Src { get; set; } = "";
    public string Alt { get; set; } = "";
    public int? Width { get; set; }
    public int? Height { get; set; }
    public bool Preview { get; set; } = true;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var size = (Width is null ? "" : $" width=\"{Width}\"") + (Height is null ? "" : $" height=\"{Height}\"");
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-image", "m-image");
        if (Width is not null) output.AddStyle($"width:{Width}px");
        var img = $"""<img class="ant-image-img" src="{Html.E(Src)}" alt="{Html.E(Alt)}"{size} />""";
        if (!Preview)
        {
            output.Content.SetHtmlContent(img);
            return;
        }
        output.Attributes.SetAttribute("x-data", "{ open: false }");
        output.Content.SetHtmlContent(
            img +
            $"""<button type="button" class="ant-image-mask m-image-mask" @click="open = true" aria-label="Preview {Html.E(Alt)}"><span class="ant-image-mask-info">{AntIcons.Span("eye")} Preview</span></button>""" +
            $"""<template x-teleport="body"><div class="m-image-preview" x-show="open" x-transition.opacity @keydown.escape.window="open = false" role="dialog" aria-modal="true" aria-label="{Html.E(Alt)}">""" +
            """<div class="ant-image-preview-mask"></div><div class="ant-image-preview-wrap" @click="open = false"><div class="ant-image-preview-img-wrapper">""" +
            $"""<img class="ant-image-preview-img" src="{Html.E(Src)}" alt="{Html.E(Alt)}" @click.stop /></div></div>""" +
            $"""<button type="button" class="m-image-preview-close" aria-label="Close preview" @click="open = false" x-init="$watch('open', v => v && $nextTick(() => $el.focus()))">{AntIcons.Span("close")}</button></div></template>""");
    }
}

/// <summary>
/// Tooltip on hover and keyboard focus (CSS only): <c>&lt;app-tooltip title="Copy link"&gt;&lt;app-button icon="link" /&gt;&lt;/app-tooltip&gt;</c>.
/// </summary>
[HtmlTargetElement("app-tooltip")]
public class TooltipTagHelper : TagHelper
{
    public string Title { get; set; } = "";

    /// <summary>top | bottom | left | right</summary>
    public string Placement { get; set; } = "top";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-tooltip-host");
        output.PostContent.SetHtmlContent(
            $"""<span class="ant-tooltip ant-tooltip-placement-{Html.E(Placement)} m-tooltip m-pop-{Html.E(Placement)}" role="tooltip"><span class="ant-tooltip-content"><span class="ant-tooltip-arrow"><span class="ant-tooltip-arrow-content"></span></span><span class="ant-tooltip-inner">{Html.E(Title)}</span></span></span>""");
    }
}

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

[HtmlTargetElement("popover-content", ParentTag = "app-popover")]
public class PopoverContentTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(PopoverTagHelper), out var slots))
            ((Dictionary<string, string>)slots)["popover-content"] = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}
