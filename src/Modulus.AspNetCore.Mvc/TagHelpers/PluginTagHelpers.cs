using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Plugins;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Inline media player, the same markup as the Blazor <c>AppMediaPlayer</c> (<see cref="MediaPlayerHtml"/>), run by modulus-plugins.js.
/// <code>&lt;app-media-player src="/media/intro.mp4" poster="/media/intro.jpg" captions="@Model.Captions" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-media-player", TagStructure = TagStructure.WithoutEndTag)]
public class MediaPlayerTagHelper : TagHelper
{
    public string Src { get; set; } = "";

    /// <summary>An &lt;audio&gt; bar instead of a video.</summary>
    public bool Audio { get; set; }

    public string? Poster { get; set; }

    public string? Title { get; set; }

    public string? Subtitle { get; set; }

    public IReadOnlyList<MediaCaption>? Captions { get; set; }

    [HtmlAttributeName("captions-on")] public bool CaptionsOn { get; set; }

    public bool Loop { get; set; }

    [HtmlAttributeName("aspect-ratio")] public string AspectRatio { get; set; } = "16 / 9";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        output.TagName = null;
        output.Content.SetHtmlContent(MediaPlayerHtml.Render(new MediaPlayerOptions
        {
            Src = Src,
            Video = !Audio,
            Poster = Poster,
            Title = Title,
            Subtitle = Subtitle,
            Captions = Captions ?? [],
            CaptionsOn = CaptionsOn,
            Loop = Loop,
            AspectRatio = AspectRatio,
            CssClass = cls,
        }));
    }
}

/// <summary>
/// WYSIWYG editor (Tiptap with an Ant Design toolbar), the same markup as the Blazor <c>AppEditor</c> (<see cref="EditorHtml"/>).
/// Posts HTML; the starting value is cleaned with <c>RichText.Sanitize(html, images: true)</c>, and what is posted must be
/// cleaned the same way on the server.
/// <code>&lt;app-editor asp-for="Body" placeholder="Write the announcement…" label="Announcement" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-editor", TagStructure = TagStructure.WithoutEndTag)]
public class EditorTagHelper : BoundControlTagHelper
{
    public string? Placeholder { get; set; }

    /// <summary>Accessible name of the editing area.</summary>
    public string? Label { get; set; }

    /// <summary>Least height of the editing area in pixels.</summary>
    [HtmlAttributeName("min-height")] public int MinHeight { get; set; } = 220;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, raw) = Bound();
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        output.TagName = null;
        output.Content.SetHtmlContent(EditorHtml.Render(name, id.Length > 0 ? id : null, raw, Placeholder, Label, Disabled, MinHeight,
            EffectiveStatus == "error", cls));
    }
}

/// <summary>
/// Tile map (MapLibre GL JS, OpenStreetMap), the same markup as the Blazor <c>AppMap</c> (<see cref="MapHtml.Tile"/>).
/// Buttons with <c>data-m-map-go="index" data-m-map-target="id"</c> fly to a marker.
/// <code>&lt;app-map id="stores" markers="@Model.Stores" height="400px" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-map", TagStructure = TagStructure.WithoutEndTag)]
public class MapTagHelper : TagHelper
{
    public string Id { get; set; } = "map";

    public IReadOnlyList<MapMarker> Markers { get; set; } = [];

    /// <summary>Centre latitude; with <see cref="CenterLng"/> it fixes the view, otherwise the map fits the markers.</summary>
    [HtmlAttributeName("center-lat")] public double? CenterLat { get; set; }

    [HtmlAttributeName("center-lng")] public double? CenterLng { get; set; }

    public int Zoom { get; set; } = 4;

    [HtmlAttributeName("scroll-zoom")] public bool ScrollZoom { get; set; }

    public string Height { get; set; } = "360px";

    public string? Label { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        output.TagName = null;
        (double, double)? center = CenterLat is { } lat && CenterLng is { } lng ? (lat, lng) : null;
        output.Content.SetHtmlContent(MapHtml.Tile(Id, Markers, center, Zoom, ScrollZoom, Height, Label, cls));
    }
}

/// <summary>
/// World choropleth (jsVectorMap) with its legend, the same markup as the Blazor <c>AppVectorMap</c> (<see cref="MapHtml.Vector"/>).
/// <code>&lt;app-vector-map id="sales-map" data="@Model.SalesByCountry" label="Sales by country" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-vector-map", TagStructure = TagStructure.WithoutEndTag)]
public class VectorMapTagHelper : TagHelper
{
    public string Id { get; set; } = "vector-map";

    public Choropleth? Data { get; set; }

    public string? Label { get; set; }

    public string Height { get; set; } = "420px";

    public string Map { get; set; } = "world";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = null;
        output.Content.SetHtmlContent(MapHtml.Vector(Id, Data ?? Choropleth.Build(new Dictionary<string, double>(), ["#91caff"], v => v.ToString(CultureInfo.CurrentCulture)), Label, Height, Map));
    }
}

/// <summary>
/// Spreadsheet (Univer with a formula bar and toolbar; a bare grid when read-only), the same markup as the Blazor
/// <c>AppSpreadsheet</c> (<see cref="SpreadsheetHtml"/>). With <c>name</c> the hidden input posts the edited sheets as JSON.
/// <code>&lt;app-spreadsheet id="budget" name="Budget" sheets="@Model.Budget" height="480px" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-spreadsheet", TagStructure = TagStructure.WithoutEndTag)]
public class SpreadsheetTagHelper : TagHelper
{
    public string Id { get; set; } = "sheet";

    public IReadOnlyList<SpreadsheetSheet>? Sheets { get; set; }

    /// <summary>Form field name; the hidden input posts the edited sheets as JSON under it.</summary>
    public string? Name { get; set; }

    /// <summary>A bare grid without toolbar, formula bar or context menu.</summary>
    public bool ReadOnly { get; set; }

    public string Height { get; set; } = "420px";

    /// <summary>Accessible name of the spreadsheet region.</summary>
    public string? Label { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        output.TagName = null;
        output.Content.SetHtmlContent(SpreadsheetHtml.Render(Id, Sheets ?? [], !ReadOnly, Height, Name, Label, cls));
    }
}

/// <summary>
/// Document editor (Univer Docs: a Word-style ribbon over A4 pages; the pages alone when read-only), the same markup as the
/// Blazor <c>AppDocument</c> (<see cref="DocumentHtml"/>). <c>value</c> is the document's JSON (build it with
/// <c>Modulus.Documents.UniverDocument</c>); with <c>name</c> the hidden input posts the edited JSON.
/// <code>&lt;app-document id="contract" name="Contract" value="@Model.ContractJson" height="720px" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-document", TagStructure = TagStructure.WithoutEndTag)]
public class DocumentTagHelper : TagHelper
{
    public string Id { get; set; } = "document";

    /// <summary>The document's JSON; empty starts a blank page.</summary>
    public string? Value { get; set; }

    /// <summary>Form field name; the hidden input posts the edited document's JSON under it.</summary>
    public string? Name { get; set; }

    /// <summary>The pages to read, without the ribbon.</summary>
    public bool ReadOnly { get; set; }

    public string Height { get; set; } = "640px";

    /// <summary>Accessible name of the editor region.</summary>
    public string? Label { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        output.TagName = null;
        output.Content.SetHtmlContent(DocumentHtml.Render(Id, Value, !ReadOnly, Height, Name, Label, cls));
    }
}

/// <summary>
/// Sortable list: drag items (pointer, touch) or move them with the arrow keys on their handle, within the list or to another
/// <c>app-sortable</c> of the same <c>group</c>. The items move on the page and each one's hidden input takes the list's
/// <c>input</c> name, so a form posts every list in its new order. After a move the <c>[data-m-sort-result]</c> input holds
/// <c>{ id, from, to, index }</c> and raises change (read it with <see cref="SortMove.TryParse"/>, or trigger htmx on it).
/// <code>
/// &lt;app-sortable name="todo" input="Todo" group="board" title="To do"&gt;
///   &lt;app-sortable-item item-id="t1" label="Write the brief"&gt;Write the brief&lt;/app-sortable-item&gt;
/// &lt;/app-sortable&gt;
/// </code>
/// </summary>
[HtmlTargetElement("app-sortable")]
public class SortableTagHelper : TagHelper
{
    /// <summary>The list's name, reported as <c>from</c> / <c>to</c>.</summary>
    public string Name { get; set; } = "list";

    /// <summary>Form field name of the items' hidden inputs (their ids, in order).</summary>
    public string? Input { get; set; }

    /// <summary>Form field name of the last move's JSON, if it should be posted.</summary>
    [HtmlAttributeName("result-name")] public string? ResultName { get; set; }

    public string? Group { get; set; }

    public SortableLayout Layout { get; set; } = SortableLayout.Vertical;

    /// <summary>Shows a drag handle (needed for keyboard moves).</summary>
    public bool Handle { get; set; } = true;

    [HtmlAttributeName("handle-only")] public bool HandleOnly { get; set; }

    public bool Disabled { get; set; }

    public string? Title { get; set; }

    [HtmlAttributeName("empty-text")] public string EmptyText { get; set; } = "Drop items here";

    internal string HelpId => $"m-sort-help-{Name}";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        context.Items[typeof(SortableTagHelper)] = this;
        var items = (await output.GetChildContentAsync()).GetContent();
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        var style = output.Attributes.TryGetAttribute("style", out var st) ? st.Value?.ToString() : null;
        output.Attributes.RemoveAll("class");
        output.Attributes.RemoveAll("style");
        var extra = output.TakeAttributes();

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-sortable-root");
        output.Attributes.SetAttribute("data-m-sort-root", "");
        var sb = new StringBuilder();
        sb.Append($"""<ul class="m-sortable{SortableHtml.LayoutClass(Layout)}{(string.IsNullOrEmpty(cls) ? "" : " " + Html.E(cls))}" data-m-sortable="{Html.E(Name)}" """)
          .Append(Html.Attr("data-m-sort-input", Input)).Append(Html.Attr("data-m-sort-group", Group)).Append(Html.Attr("data-m-sort-layout", SortableHtml.LayoutAttribute(Layout)))
          .Append(Html.Attr("data-m-sort-handle-only", HandleOnly)).Append(Html.Attr("data-m-sort-disabled", Disabled)).Append(Html.Attr("data-m-sort-title", Title))
          .Append(Html.Attr("aria-label", Title)).Append(Html.Attr("data-empty", EmptyText)).Append(Html.Attr("style", style)).Append(extra).Append('>')
          .Append(items).Append("</ul>")
          .Append($"""<input type="hidden" data-m-sort-result{Html.Attr("name", ResultName)} />""")
          .Append("""<span class="m-sort-live" data-m-sort-live aria-live="assertive"></span>""")
          .Append($"""<span class="m-sort-live" id="{Html.E(HelpId)}">{Html.E(SortableHtml.KeyboardHelp)}</span>""");
        output.Content.SetHtmlContent(sb.ToString());
    }
}

/// <summary>An item of <c>app-sortable</c>; <c>item-id</c> is posted in the list's order.</summary>
[HtmlTargetElement("app-sortable-item", ParentTag = "app-sortable")]
public class SortableItemTagHelper : TagHelper
{
    [HtmlAttributeName("item-id")] public string ItemId { get; set; } = "";

    /// <summary>Name read out when the item moves (defaults to its text).</summary>
    public string? Label { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var list = context.Items.TryGetValue(typeof(SortableTagHelper), out var l) && l is SortableTagHelper s ? s : new SortableTagHelper();
        var body = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "li";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-sort-item");
        output.Attributes.SetAttribute("data-m-sort-id", ItemId);
        if (Label is not null) output.Attributes.SetAttribute("data-m-sort-label", Label);
        var sb = new StringBuilder();
        if (list.Handle) sb.Append(SortableHtml.Handle(Label ?? ItemId, list.HelpId, list.Disabled));
        sb.Append($"""<div class="m-sort-item-body">{body}</div>""");
        if (list.Input is { Length: > 0 } input) sb.Append($"""<input type="hidden" name="{Html.E(input)}" value="{Html.E(ItemId)}" data-m-sort-value />""");
        output.Content.SetHtmlContent(sb.ToString());
    }
}
