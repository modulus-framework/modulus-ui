using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using SortDirection = Modulus.Ui.Abstractions.SortDirection;
using Modulus.Ui.Abstractions.Shell;

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
            AddRowText = $"Add {GridSchema.Singular(level.Title)}",
            AllowDeleteRow = level.Editable && level.AllowDelete,
            AllowColumnEdit = false,
            ShowSave = level.Editable,
            ShowDiscard = level.Editable,
            RowDetail = child is not null && level.Expandable ? row => new HtmlString(Detail(row, loaded, child)) : null,
            RowExpandable = row => v.Settings.CanExpand(r.Level, row),
            // Search and filters run on the server for a level (they page); the toolbar and filter row below send them.
            Searchable = false,
            ClientFilters = false,
            ExportFileName = level.Title,
            ReloadHref = NestedGridForm.Url(Url, r),
            ReloadTarget = "#" + container,
            // Headers sort and search the level on the server too: each is an htmx GET of this level.
            Sortable = level.Sortable,
            // Rows drag to a new order, posted with Save (not while the level is sorted by a column).
            RowDraggable = level.RowDraggable,
            ColumnSearchable = level.Searchable,
            // A level is one page of the server's rows; its own pager (below) pages on the server.
            Paged = false,
            // The filters sit under the column headers (a select for option columns), shown from the start.
            FilterRowOpen = level.Searchable,
            SortHref = key => NestedGridForm.Url(Url, r.WithSort(key)),
            SortField = r.SortField,
            SortDescending = r.Descending,
            SearchHref = key => NestedGridForm.Url(Url, r.WithSearch(key, null)),
            Searches = r.Searches,
            ServerTarget = "#" + container,
        };
        if (!level.Editable) foreach (var c in v.Columns) c.Editable = false;

        var toolbar = Toolbar(level, r, container);
        var filters = Filters(r, container, level.Searchable);
        // The pager, then the level's rollup totals (a tree level's children add up under their parent).
        var summary = Pager(r, v.Total, container, Math.Max(1, level.PageSize)) + (v.Total > 0 ? TableGroups.Totals(v.Totals, v.Total) : "");
        var gridContext = new TagHelperContext("app-editable-grid", [], new Dictionary<object, object>(), context.UniqueId + "-g");
        var gridOutput = new TagHelperOutput("app-editable-grid", [new TagHelperAttribute("id", container + "-grid")], (_, _) =>
        {
            // Fill the grid's slots the way <grid-toolbar> and friends would.
            var slots = (Dictionary<string, string>)gridContext.Items[typeof(EditableGridTagHelper)];
            slots["grid-toolbar"] = toolbar;
            if (filters.Length > 0) slots["grid-filters"] = filters;
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
        sb.Append($"""<form method="post" hx-post="{Html.E(NestedGridForm.Url(Url, r))}" hx-target="#{container}" hx-swap="outerHTML" data-m-stop="submit" novalidate>""");
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
            // The box folds into a search icon while empty (modulus-shell.js, [data-m-search-reveal]).
            var open = !string.IsNullOrEmpty(r.Keyword);
            sb.Append($"""<button type="button" class="ant-btn ant-btn-text ant-btn-icon-only m-table-search-toggle" aria-label="Search {Html.E(level.Title)}" title="Search (Ctrl+F)" data-m-search-toggle data-m-search-reveal{(open ? " hidden" : "")}>{AntIcons.Span("search")}</button>""");
            sb.Append($"""<span class="ant-input-affix-wrapper m-table-toolbar-search m-nested-search"{(open ? "" : " hidden")}><span class="ant-input-prefix">{AntIcons.Span("search")}</span>""");
            sb.Append($"""<input class="ant-input" type="search" name="q" value="{Html.E(r.Keyword)}" placeholder="Search {Html.E(level.Title.ToLowerInvariant())}…" aria-label="Search {Html.E(level.Title)}" autocomplete="off" hx-get="{Html.E(url)}" hx-trigger="keyup[key=='Enter'], search" hx-include="this" {target}data-m-no-enter /></span>""");
        }
        // One click clears the level's keyword, filters and column searches.
        if (r.Filtered)
            sb.Append($"""<button type="button" class="ant-btn ant-btn-text m-table-clear" title="Clear the search and filters" hx-get="{Html.E(NestedGridForm.Url(Url, r.Cleared()))}" {target}>{AntIcons.Span("close-circle")}<span>Clear</span></button>""");
        return sb.ToString();
    }

    // The filter bar above the table: a dropdown per Filterable column; each change is an htmx GET of this level with
    // f.{key}. A searchable level filters in the row under its headers instead (no bar); the toolbar's Clear resets both.
    private string Filters(NestedGridRequest r, string container, bool filterRow)
    {
        IReadOnlyList<GridColumn> columns = filterRow ? [] : GridSchema.FilterColumns(View.Columns);
        if (columns.Count == 0) return "";
        var sb = new StringBuilder();
        foreach (var c in columns)
        {
            var current = r.Filters?.GetValueOrDefault(c.Key);
            var url = NestedGridForm.Url(Url, r.WithFilter(c.Key, null));
            sb.Append($"""<select class="m-select" name="{NestedGridForm.FilterPrefix}{Html.E(c.Key)}" aria-label="{Html.E(c.Title)}" hx-get="{Html.E(url)}" hx-trigger="change" hx-include="this" hx-target="#{container}" hx-swap="outerHTML" data-m-stop="change"><option value="">Any {Html.E(c.Title.ToLowerInvariant())}</option>""");
            foreach (var o in GridSchema.FilterOptions(c))
                sb.Append($"""<option value="{Html.E(o.Value)}"{(o.Value == current ? " selected" : "")}>{Html.E(o.Label)}</option>""");
            sb.Append("</select>");
        }
        return sb.ToString();
    }

    // The data table's pager, on the server: the "1–5 of 14" range, Ant Design's page window with ••• jumps
    // (Paging.Window) and rows per page (size=, carried in the level's URLs). Each is an htmx GET of this level.
    private string Pager(NestedGridRequest r, int total, string container, int levelSize)
    {
        if (total <= 0) return "";
        var pages = Paging.Pages(total, r.PageSize);
        var page = Math.Clamp(r.Page, 1, pages);
        var target = $"""hx-target="#{container}" hx-swap="outerHTML" """.TrimEnd();
        string Link(int p, string text, string label, string cls = "") =>
            $"""<a{(cls.Length > 0 ? $" class=\"{cls}\"" : "")} href="#" hx-get="{Html.E(NestedGridForm.Url(Url, r with { Page = p }))}" {target} aria-label="{label}">{text}</a>""";
        var sb = new StringBuilder("""<ul class="ant-pagination ant-pagination-mini m-nested-pager" aria-label="Pagination">""");
        sb.Append($"""<li class="ant-pagination-total-text">{Paging.Range(page, r.PageSize, total)}</li>""");
        sb.Append(page > 1
            ? $"""<li class="ant-pagination-prev">{Link(page - 1, AntIcons.Span("left"), "Previous page", "ant-pagination-item-link")}</li>"""
            : $"""<li class="ant-pagination-prev ant-pagination-disabled" aria-disabled="true"><span class="ant-pagination-item-link">{AntIcons.Span("left")}</span></li>""");
        var window = Paging.Window(page, pages);
        for (var i = 0; i < window.Count; i++)
        {
            var p = window[i];
            if (p == 0)
            {
                // AntD's jump item: ••• that moves five pages.
                var next = i > 0 && window[i - 1] >= page;
                var kind = next ? "jump-next" : "jump-prev";
                var to = next ? Math.Min(pages, page + 5) : Math.Max(1, page - 5);
                var jumpTitle = next ? "Next 5 pages" : "Previous 5 pages";
                const string ellipsis = """<span class="ant-pagination-item-container"><span class="ant-pagination-item-ellipsis">•••</span></span>""";
                sb.Append($"""<li class="ant-pagination-{kind} ant-pagination-{kind}-custom-icon" title="{jumpTitle}">{Link(to, ellipsis, jumpTitle, "ant-pagination-item-link")}</li>""");
                continue;
            }
            sb.Append($"""<li class="{Html.Cx("ant-pagination-item", $"ant-pagination-item-{p}", p == page ? "ant-pagination-item-active" : null)}"{(p == page ? " aria-current=\"page\"" : "")}>{Link(p, p.ToString(CultureInfo.InvariantCulture), $"Page {p}")}</li>""");
        }
        sb.Append(page < pages
            ? $"""<li class="ant-pagination-next">{Link(page + 1, AntIcons.Span("right"), "Next page", "ant-pagination-item-link")}</li>"""
            : $"""<li class="ant-pagination-next ant-pagination-disabled" aria-disabled="true"><span class="ant-pagination-item-link">{AntIcons.Span("right")}</span></li>""");
        // Rows per page: the select's own value is the size= of the request (the URL leaves it out).
        var sizeUrl = NestedGridForm.Url(Url, r with { Page = 1 }, "size");
        sb.Append($"""<li class="ant-pagination-options"><select class="m-select m-select-sm m-pager-size" name="size" aria-label="Rows per page" hx-get="{Html.E(sizeUrl)}" hx-trigger="change" hx-include="this" {target} data-m-stop="change">""");
        foreach (var size in Paging.SizesWith(levelSize))
            sb.Append($"""<option value="{size}"{(size == r.PageSize ? " selected" : "")}>{size} / page</option>""");
        return sb.Append("</select></li></ul>").ToString();
    }
}
