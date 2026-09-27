using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// One level of a nested, expandable editable grid (the MVC twin of Blazor's <c>NestedGrid</c>):
/// <code>&lt;app-nested-grid view="Model" url="/tables/nested/level" /&gt;</code>
/// It renders an <c>app-editable-grid</c> in its own form with search, sort and paging. A row that can expand holds
/// a placeholder that loads the next level from <c>url</c> the first time it is shown (<c>hx-trigger="intersect once"</c>),
/// so each level is fetched lazily and posts only its own rows. Read the state with <see cref="NestedGridForm"/>.
/// </summary>
[HtmlTargetElement("app-nested-grid", TagStructure = TagStructure.WithoutEndTag)]
public class NestedGridTagHelper : TagHelper
{
    public NestedGridView View { get; set; } = default!;

    /// <summary>The endpoint that renders a level (GET) and takes its posts (POST).</summary>
    public string Url { get; set; } = "";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var v = View;
        var r = v.Request;
        var level = v.Settings.Level(r.Level) ?? new NestedGridLevel();
        var name = NestedGridForm.FieldName(r.Level);
        var container = NestedGridForm.ContainerId(r.Level, r.ParentId);
        var loaded = v.LoadedIds.ToHashSet(StringComparer.Ordinal);
        var child = v.Settings.Level(r.Level + 1);
        var title = v.Title ?? level.Title;

        var grid = new EditableGridTagHelper
        {
            Name = name,
            Columns = v.Columns,
            Rows = v.Rows,
            HeaderTitle = title,
            Errors = v.Errors,
            Dirty = v.Dirty,
            AllowAddRow = level.Editable && level.AllowAdd,
            AllowDeleteRow = level.Editable && level.AllowDelete,
            AllowColumnEdit = false,
            ShowSave = level.Editable,
            ShowDiscard = level.Editable,
            RowDetail = child is not null && level.Expandable ? row => new HtmlString(Detail(row, loaded, child)) : null,
        };
        if (!level.Editable) foreach (var c in v.Columns) c.Editable = false;

        var toolbar = Toolbar(level, r, container);
        var summary = Pager(r, v.Total, container);
        var gridContext = new TagHelperContext("app-editable-grid", [], new Dictionary<object, object>(), context.UniqueId + "-g");
        var gridOutput = new TagHelperOutput("app-editable-grid", [new TagHelperAttribute("id", container + "-grid")], (_, _) =>
        {
            // Fill the grid's slots the way <grid-toolbar> and friends would.
            var slots = (Dictionary<string, string>)gridContext.Items[typeof(EditableGridTagHelper)];
            slots["grid-toolbar"] = toolbar;
            slots["grid-title-extra"] = $"""<span class="m-badge m-badge-gray-lt">{v.Total.ToString(CultureInfo.CurrentCulture)}</span>""";
            if (summary.Length > 0) slots["grid-summary"] = summary;
            return Task.FromResult<TagHelperContent>(new DefaultTagHelperContent());
        });
        await grid.ProcessAsync(gridContext, gridOutput);
        using var writer = new StringWriter();
        gridOutput.WriteTo(writer, HtmlEncoder.Default);

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("id", container);
        output.AddClasses("m-nested", $"m-nested-level-{r.Level}");
        // A child level sits inside its parent's grid: keep its "dirty" events and submits to itself.
        output.Attributes.SetAttribute("@grid-change.stop", "");

        var sb = new StringBuilder();
        sb.Append($"""<form method="post" hx-post="{Html.E(NestedGridForm.Url(Url, r))}" hx-target="#{container}" hx-swap="outerHTML" onsubmit="event.stopPropagation()" novalidate>""");
        foreach (var id in v.LoadedIds) sb.Append($"""<input type="hidden" name="{name}.loaded" value="{Html.E(id)}" />""");
        sb.Append(writer.ToString()).Append("</form>");
        output.Content.SetHtmlContent(sb.ToString());
    }

    // The next level, loaded when the detail row first becomes visible.
    private string Detail(GridRecord row, HashSet<string> loaded, NestedGridLevel child)
    {
        if (!loaded.Contains(row.Id))
            return $"""<div class="m-nested-note">Save this row to add its {Html.E(child.Title.ToLowerInvariant())}.</div>""";
        if (!View.Settings.CanExpand(View.Request.Level, row))
            return $"""<div class="m-nested-note">No {Html.E(child.Title.ToLowerInvariant())} for this row.</div>""";
        var url = NestedGridForm.Url(Url, new NestedGridRequest(View.Request.Level + 1, row.Id));
        return $"""<div class="m-nested-lazy" hx-get="{Html.E(url)}" hx-trigger="intersect once" hx-target="this" hx-swap="outerHTML"><span class="m-muted">Loading {Html.E(child.Title.ToLowerInvariant())}…</span></div>""";
    }

    private string Toolbar(NestedGridLevel level, NestedGridRequest r, string container)
    {
        var target = $"""hx-target="#{container}" hx-swap="outerHTML" """;
        var sb = new StringBuilder();
        if (level.Searchable)
        {
            var url = NestedGridForm.Url(Url, r with { Page = 1 }, "q");
            sb.Append($"""<span class="ant-input-affix-wrapper m-nested-search"><span class="ant-input-prefix">{AntIcons.Span("search")}</span>""");
            sb.Append($"""<input class="ant-input" type="search" name="q" value="{Html.E(r.Keyword)}" placeholder="Search…" aria-label="Search {Html.E(level.Title)}" autocomplete="off" hx-get="{Html.E(url)}" hx-trigger="keyup[key=='Enter'], search" hx-include="this" {target}onkeydown="if (event.key === 'Enter') event.preventDefault()" /></span>""");
        }
        if (level.Sortable)
        {
            var url = NestedGridForm.Url(Url, r with { Page = 1 }, "sort");
            var current = r.SortField is null ? "" : $"{r.SortField}:{(r.Descending ? "desc" : "asc")}";
            sb.Append($"""<select class="m-select m-nested-sort" name="sort" aria-label="Sort by" hx-get="{Html.E(url)}" hx-trigger="change" hx-include="this" {target}onchange="event.stopPropagation()"><option value="">Sort: default</option>""");
            foreach (var c in View.Columns.Where(c => !c.Hidden))
                foreach (var (dir, arrow) in new[] { ("asc", "↑"), ("desc", "↓") })
                {
                    var value = $"{c.Key}:{dir}";
                    sb.Append($"""<option value="{Html.E(value)}"{(value == current ? " selected" : "")}>{Html.E(c.Title)} {arrow}</option>""");
                }
            sb.Append("</select>");
        }
        return sb.ToString();
    }

    // Ant Design's small pagination; each page is an htmx GET of this level.
    private string Pager(NestedGridRequest r, int total, string container)
    {
        var pages = (int)Math.Ceiling(total / (double)Math.Max(1, r.PageSize));
        if (pages <= 1) return "";
        var page = Math.Clamp(r.Page, 1, pages);
        string Link(int p, string text, string label) =>
            $"""<a href="#" hx-get="{Html.E(NestedGridForm.Url(Url, r with { Page = p }))}" hx-target="#{container}" hx-swap="outerHTML" aria-label="{label}">{text}</a>""";
        var sb = new StringBuilder("""<ul class="ant-pagination ant-pagination-mini m-nested-pager">""");
        sb.Append(page > 1
            ? $"""<li class="ant-pagination-prev">{Link(page - 1, AntIcons.Span("left"), "Previous page")}</li>"""
            : $"""<li class="ant-pagination-prev ant-pagination-disabled" aria-disabled="true"><span class="ant-pagination-item-link">{AntIcons.Span("left")}</span></li>""");
        for (var p = 1; p <= pages; p++)
            sb.Append($"""<li class="{Html.Cx("ant-pagination-item", $"ant-pagination-item-{p}", p == page ? "ant-pagination-item-active" : null)}"{(p == page ? " aria-current=\"page\"" : "")}>{Link(p, p.ToString(CultureInfo.InvariantCulture), $"Page {p}")}</li>""");
        sb.Append(page < pages
            ? $"""<li class="ant-pagination-next">{Link(page + 1, AntIcons.Span("right"), "Next page")}</li>"""
            : $"""<li class="ant-pagination-next ant-pagination-disabled" aria-disabled="true"><span class="ant-pagination-item-link">{AntIcons.Span("right")}</span></li>""");
        return sb.Append("</ul>").ToString();
    }
}
