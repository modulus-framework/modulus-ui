using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Plugins;
using SortDirection = Modulus.Ui.Abstractions.SortDirection;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Schema-driven editable grid, the MVC twin of Blazor's <c>EditableGrid</c> (same DOM, same model):
/// <code>
/// &lt;form method="post" hx-post hx-target="#projects" hx-select="#projects" hx-swap="outerHTML"&gt;
///     &lt;app-editable-grid id="projects" columns="Model.Columns" rows="Model.Rows" header-title="Projects"
///                        errors="Model.Errors" row-detail="detail" /&gt;
/// &lt;/form&gt;
/// </code>
/// Click a cell to edit it; switches and ratings change in place; rows are added and deleted in the browser.
/// Column operations (pin, hide, move, remove, add) and Save post the form with a <c>{name}.op</c> value; read the
/// post with <see cref="EditableGridForm.Read"/>, apply it with <see cref="EditableGridForm.Apply"/> and render the
/// grid again. Every post carries the schema and all rows, so nothing is lost between operations.
/// </summary>
[HtmlTargetElement("app-editable-grid")]
public class EditableGridTagHelper : TagHelper
{
    public IList<GridColumn> Columns { get; set; } = [];

    public IReadOnlyList<GridRecord> Rows { get; set; } = [];

    /// <summary>Prefix of the posted fields (<c>grid.schema</c>, <c>grid.index</c>, <c>grid[id].key</c>, <c>grid.op</c>).</summary>
    public string Name { get; set; } = "grid";

    public string? HeaderTitle { get; set; }

    /// <summary>Cell errors to show (from <see cref="GridSchema.Validate"/> after a Save).</summary>
    public IEnumerable<GridError>? Errors { get; set; }

    /// <summary>Show "Unsaved changes" (the posted grid differs from the saved one).</summary>
    public bool Dirty { get; set; }

    /// <summary>Content of an expanded row, e.g. a Razor template <c>@&lt;div&gt;…@item["notes"]…&lt;/div&gt;</c>.</summary>
    public Func<GridRecord, IHtmlContent>? RowDetail { get; set; }

    public bool AllowAddRow { get; set; } = true;

    /// <summary>Tooltip of the add-row button ("Add style", say).</summary>
    public string AddRowText { get; set; } = "Add row";

    public bool AllowDeleteRow { get; set; } = true;

    /// <summary>Column menus, the column settings panel and "Add column".</summary>
    public bool AllowColumnEdit { get; set; } = true;

    public bool ShowSave { get; set; } = true;

    public bool ShowDiscard { get; set; } = true;

    /// <summary>A search box that hides the rows whose visible text doesn't contain the keyword (in the browser).</summary>
    public bool Searchable { get; set; } = true;

    /// <summary>A filter row with a dropdown for each <see cref="GridColumn.Filterable"/> column (filters in the browser).</summary>
    public bool ClientFilters { get; set; } = true;

    /// <summary>A checkbox column and a batch bar for the selected rows: set a value, export, delete.</summary>
    public bool Selectable { get; set; } = true;

    /// <summary>"Export CSV" (the rows shown) in the toolbar and "Export selected" in the batch bar.</summary>
    public bool Exportable { get; set; } = true;

    /// <summary>File name of the CSV export (default: the title).</summary>
    public string? ExportFileName { get; set; }

    /// <summary>
    /// A pager under the grid ("1–10 of 42", pages, rows per page), paging the rows after search and filters in the
    /// browser (modulusGrid). Off for a grid whose rows are one page of a server list (the nested grid's levels).
    /// </summary>
    public bool Paged { get; set; } = true;

    /// <summary>Rows per page to start with; the pager also offers <see cref="Paging.Sizes"/>.</summary>
    [HtmlAttributeName("page-size")] public int PageSize { get; set; } = 10;

    /// <summary>The Columns button (show and hide columns) even when <see cref="AllowColumnEdit"/> is off.</summary>
    public bool AllowColumnHide { get; set; } = true;

    /// <summary>Drag a header's right edge to resize the column; the width goes into the posted schema.</summary>
    public bool Resizable { get; set; } = true;

    /// <summary>With <see cref="ReloadTarget"/>: a Reload button that fetches this URL with htmx.</summary>
    public string? ReloadHref { get; set; }

    public string? ReloadTarget { get; set; }

    /// <summary>Column headers sort the rows: ascending, descending, then unsorted (in the browser, or with <see cref="SortHref"/>).</summary>
    public bool Sortable { get; set; } = true;

    /// <summary>
    /// A drag handle on each row: drag it (or focus it and press Up, Down, Home, End) to reorder the rows. The rows
    /// post in their new order. Locked while a column sort is on.
    /// </summary>
    [HtmlAttributeName("row-draggable")] public bool RowDraggable { get; set; } = true;

    /// <summary>A search icon in each column header for a search of that column (see <see cref="ColumnSearch"/>).</summary>
    public bool ColumnSearchable { get; set; } = true;

    /// <summary>Starts with the filter row (a box or select under each column header) shown; the toolbar's filter button still toggles it.</summary>
    [HtmlAttributeName("filter-row-open")] public bool FilterRowOpen { get; set; }

    /// <summary>Server sorting (the nested grid): column key → URL of the next sort. Null sorts in the browser.</summary>
    public Func<string, string>? SortHref { get; set; }

    /// <summary>With <see cref="SortHref"/>: the column the server sorted by.</summary>
    public string? SortField { get; set; }

    public bool SortDescending { get; set; }

    /// <summary>Server column search: column key → URL without that column's search. Null searches in the browser.</summary>
    public Func<string, string>? SearchHref { get; set; }

    /// <summary>With <see cref="SearchHref"/>: the column searches the server applied.</summary>
    public IReadOnlyDictionary<string, string?>? Searches { get; set; }

    /// <summary>With <see cref="SortHref"/> or <see cref="SearchHref"/>: the element the htmx response replaces (outerHTML).</summary>
    public string? ServerTarget { get; set; }

    /// <summary>Bands over the column titles (a multi-level header): <see cref="HeaderGroup"/>s over <see cref="GridColumn.Key"/>s.</summary>
    public IReadOnlyList<HeaderGroup>? HeaderGroups { get; set; }

    private const int ExpandWidth = 48;
    private const int SelectionWidth = 48;
    private const int ActionsWidth = 64;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new Dictionary<string, string>();
        context.Items[typeof(EditableGridTagHelper)] = slots;
        await output.GetChildContentAsync();

        var culture = CultureInfo.CurrentCulture;
        var visible = GridSchema.Visible(Columns);
        var detail = RowDetail is not null;
        var lead = DragWidth + (Selectable ? SelectionWidth : 0) + (detail ? ExpandWidth : 0);
        var offsets = GridSchema.StickyOffsets(visible, lead);
        var errors = (Errors ?? []).GroupBy(e => (e.RowId, e.Key)).ToDictionary(g => g.Key, g => g.First().Message);
        var hasLeft = visible.Any(c => c.Fixed == GridFixed.Left);
        var hasRight = AllowDeleteRow || visible.Any(c => c.Fixed == GridFixed.Right);
        var width = GridSchema.ScrollWidth(Columns, lead + (AllowDeleteRow ? ActionsWidth : 0));
        var id = output.Attributes.TryGetAttribute("id", out var idAttr) ? idAttr.Value?.ToString() ?? "" : $"grid-{context.UniqueId}";
        var n = Name;

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("id", id);
        output.AddClasses("m-table-card", "m-grid", "ant-card");
        if (Resizable) output.AddClasses("m-resizable");
        var filterRowOpen = FilterRowOpen && ColumnSearchable;
        if (filterRowOpen) output.Attributes.SetAttribute("data-m-filter-row-open", "");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusGrid({Widgets.Config(ClientConfig(culture))})"));
        output.Attributes.SetAttribute("@grid-change", "touch($event.target)");
        output.Attributes.SetAttribute("@keydown.escape", "closeMenu()");
        if (Resizable) output.Attributes.SetAttribute("@m-col-resize", "resized($event.detail)");
        if (ColumnSearchable && SearchHref is null) output.Attributes.SetAttribute("@m-col-search.stop", "setSearch($event.detail)");
        if (RowDraggable)
        {
            // modulus-plugins.js moves the row (with its detail row); modulusGrid marks it changed (modulus:sort).
            output.Attributes.SetAttribute("data-m-sort-rows", id);
            output.Attributes.SetAttribute("data-m-sort-root", "");
            if (HeaderTitle is not null) output.Attributes.SetAttribute("data-m-sort-title", HeaderTitle);
            if (ServerSorted) output.Attributes.SetAttribute("data-m-sort-disabled", "");
        }

        var sb = new StringBuilder("""<div class="ant-card-body">""");
        if (RowDraggable) sb.Append(SortableHtml.RowsHelp(HelpId(id)));
        sb.Append($"""<input type="hidden" name="{Html.E(n)}.schema" value="{Html.E(GridSchema.Serialize(Columns))}" x-ref="schema" />""");

        // Toolbar, as the Blazor grid's: the title and count; the unsaved-changes count with Discard and Save; icon
        // buttons with tooltips (search, filter row, columns, add row, reload) and a "More" menu (export, add column,
        // density). modulus-shell.js adds the keys: Ctrl+S saves, Insert adds a row, Ctrl+F searches, arrows move between cells.
        sb.Append("""<div class="m-table-toolbar"><div class="m-table-toolbar-title">""").Append(Html.E(HeaderTitle));
        if (slots.TryGetValue("grid-title-extra", out var titleExtra)) sb.Append(' ').Append(titleExtra);
        sb.Append("""</div><div class="m-table-toolbar-actions">""");
        var hideClean = Dirty ? "" : " style=\"display: none;\"";
        sb.Append($"""<span class="m-grid-dirty" x-show="dirty"{hideClean}><span class="ant-badge ant-badge-status ant-badge-not-a-wrapper" role="status"><span class="ant-badge-status-dot ant-badge-status-warning"></span><span class="ant-badge-status-text" x-text="changesText()">Unsaved changes</span></span></span>""");
        if (ShowDiscard)
            sb.Append($"""<button type="submit" name="{Html.E(n)}.op" value="discard" class="ant-btn ant-btn-text m-grid-discard" x-show="dirty"{hideClean}><span>Discard</span></button>""");
        if (ShowSave)
            sb.Append($$"""<button type="submit" name="{{Html.E(n)}}.op" value="save" class="ant-btn {{(Dirty ? "ant-btn-primary" : "ant-btn-default")}} m-grid-save" :class="{ 'ant-btn-primary': dirty, 'ant-btn-default': !dirty }" title="Save (Ctrl+S)" data-m-grid-save>{{AntIcons.Span("save")}}<span>Save</span></button>""");
        if (Searchable)
        {
            var what = HeaderTitle ?? "rows";
            // No name: the keyword stays in the browser and is never posted with the grid. An empty box folds into its icon.
            sb.Append($"""<button type="button" class="ant-btn ant-btn-text ant-btn-icon-only m-table-search-toggle" aria-label="Search {Html.E(what)}" title="Search (Ctrl+F)" data-m-search-toggle x-show="!searchShown()" @click="openSearch()">{AntIcons.Span("search")}</button>""");
            sb.Append($"""<span class="ant-input-affix-wrapper m-table-toolbar-search m-grid-search" x-show="searchShown()" style="display: none;"><span class="ant-input-prefix">{AntIcons.Span("search")}</span>""")
              .Append($"""<input class="ant-input" type="text" placeholder="Search {Html.E(what.ToLowerInvariant())}…" aria-label="Search {Html.E(what)}" autocomplete="off" x-ref="search" x-model="keyword" @input.debounce.150ms="applyFilter()" @keydown.enter.prevent @keydown.escape.stop="keyword = ''; applyFilter(); searchOpen = false" @blur="if (!keyword) searchOpen = false" />""")
              .Append($"""<span class="ant-input-suffix"><button type="button" class="ant-input-clear-icon m-input-clear ant-input-clear-icon-hidden" :class="keyword ? '' : 'ant-input-clear-icon-hidden'" aria-label="Clear search" tabindex="-1" @click="keyword = ''; applyFilter()">{AntIcons.Span("close-circle", IconThemeType.Fill)}</button></span></span>""");
        }
        if (slots.TryGetValue("grid-toolbar", out var toolbar)) sb.Append(toolbar);
        // One click clears the search, the filters and the column searches (the nested grid adds its own, on the server).
        if (ServerTarget is null)
            sb.Append($"""<button type="button" class="ant-btn ant-btn-text m-table-clear" title="Clear the search and filters" x-show="anyFilter()" style="display: none;" @click="clearFilters()">{AntIcons.Span("close-circle")}<span>Clear</span></button>""");
        if (ColumnSearchable) sb.Append(TableSettingsTagHelper.FilterRowButton);
        if (AllowColumnHide || AllowColumnEdit) sb.Append(ColumnSettings());
        if (Exportable)
            sb.Append($"""<button type="button" class="ant-btn ant-btn-default m-grid-export" title="Export the rows (after search and filters) to a CSV file" @click="exportCsv(false)">{AntIcons.Span("download")}<span>Export CSV</span></button>""");
        if (AllowAddRow)
            sb.Append($"""<button type="submit" name="{Html.E(n)}.op" value="add-row" class="ant-btn ant-btn-text ant-btn-icon-only m-grid-add" aria-label="{Html.E(AddRowText)}" title="{Html.E(AddRowText)} (Insert)" data-m-grid-add @click.prevent="addRow()">{AntIcons.Span("plus")}</button>""");
        sb.Append("""<div class="m-table-toolbar-settings">""");
        if (ReloadHref is not null && ReloadTarget is not null)
            sb.Append($"""<button type="button" class="ant-btn ant-btn-text ant-btn-icon-only" aria-label="Reload" title="Reload" hx-get="{Html.E(ReloadHref)}" hx-target="{Html.E(ReloadTarget)}" hx-swap="outerHTML">{AntIcons.Span("reload")}</button>""");
        var more = new StringBuilder();
        if (AllowColumnEdit) more.Append(DropdownTagHelper.Item($"""{AntIcons.Span("insert-row-right")}<span>Add column</span>""", null, " @click=\"openColumnDialog()\""));
        if (more.Length > 0) more.Append("""<li class="ant-dropdown-menu-item-divider" role="separator"></li>""");
        more.Append("""<li class="ant-dropdown-menu-item-group-title" role="presentation">Density</li>""");
        foreach (var (label, value) in new[] { ("Comfortable", ""), ("Standard", "ant-table-middle"), ("Compact", "ant-table-small") })
            more.Append(DropdownTagHelper.Item(label, null, $""" @click="density = '{value}'" :class="density === '{value}' && 'ant-dropdown-menu-item-selected'" """.TrimEnd()));
        sb.Append(DropdownTagHelper.Render(
            $"""<button type="button" class="ant-btn ant-btn-text ant-btn-icon-only" aria-label="More actions" title="More actions">{AntIcons.Span("more")}</button>""",
            more.ToString(), "bottomRight"));
        sb.Append("</div></div></div>");

        // Filter row: the app's own (grid-filters), or a dropdown per Filterable column, applied in the browser.
        if (slots.TryGetValue("grid-filters", out var filters))
            sb.Append($"""<div class="m-table-filters">{filters}</div>""");
        else if (ClientFilters && GridSchema.FilterColumns(Columns) is { Count: > 0 } filterColumns)
        {
            sb.Append("""<div class="m-table-filters">""");
            foreach (var c in filterColumns)
            {
                sb.Append($"""<select class="m-select" aria-label="{Html.E(c.Title)}" data-filter="{Html.E(c.Key)}" @change="setFilter('{Html.E(c.Key)}', $el.value)"><option value="">Any {Html.E(c.Title.ToLowerInvariant())}</option>""");
                foreach (var o in GridSchema.FilterOptions(c)) sb.Append($"""<option value="{Html.E(o.Value)}">{Html.E(o.Label)}</option>""");
                sb.Append("</select>");
            }
            sb.Append("</div>"); // the toolbar's Clear resets them

        }

        if (Selectable) sb.Append(BatchBar(slots.GetValueOrDefault("grid-batch")));

        if (errors.Count > 0)
            sb.Append($"""<div class="ant-alert ant-alert-error m-table-alert" role="alert">{AntIcons.Span("close-circle", IconThemeType.Fill, "ant-alert-icon")}<div class="ant-alert-content"><div class="ant-alert-message">{errors.Count} {(errors.Count == 1 ? "cell needs" : "cells need")} attention before saving.</div></div></div>""");

        // Table
        var tableClass = Html.Cx("ant-table", "ant-table-middle", "ant-table-fixed-column", "ant-table-scroll-horizontal", hasLeft ? "ant-table-has-fix-left" : null, hasRight ? "ant-table-has-fix-right" : null);
        sb.Append($"""<div class="ant-table-wrapper"><div class="ant-table-spin-wrapper ant-spin-nested-loading"><div><div class="ant-spin-container"><div class="{tableClass}" x-ref="table" :class="tableClass()">""");
        sb.Append($"""<div class="ant-table-container"><div class="ant-table-content" style="overflow: auto hidden;" x-ref="scroller" @scroll.passive="onScroll()">""");
        sb.Append($"""<table style="width: {width}px; min-width: 100%; table-layout: fixed;"><colgroup>""");
        if (RowDraggable) sb.Append($"""<col style="width: {DragWidth}px;">""");
        if (Selectable) sb.Append($"""<col class="ant-table-selection-col" style="width: {SelectionWidth}px;">""");
        if (detail) sb.Append("""<col class="ant-table-expand-icon-col" style="width: 48px;">""");
        foreach (var c in visible) sb.Append($"""<col style="width: {c.Width ?? 150}px; min-width: {c.Width ?? 150}px;">""");
        if (AllowDeleteRow) sb.Append($"""<col style="width: {ActionsWidth}px; min-width: {ActionsWidth}px;">""");
        sb.Append($"""</colgroup><thead class="ant-table-thead{(filterRowOpen ? " m-filter-row-on" : "")}">{BandRows(visible, detail)}<tr>""");
        if (RowDraggable)
            sb.Append($"""<th class="{Html.Cx("ant-table-cell", "m-row-drag-cell", hasLeft ? "ant-table-cell-fix-left" : null)}"{(hasLeft ? " style=\"position: sticky; left: 0px;\"" : "")}><span class="m-sr-only">Reorder</span></th>""");
        if (Selectable)
            sb.Append($"""<th class="{Html.Cx("ant-table-cell", "ant-table-selection-column", hasLeft ? "ant-table-cell-fix-left" : null)}"{(hasLeft ? $" style=\"position: sticky; left: {DragWidth}px;\"" : "")}>{Checkbox("Select all rows", " data-select-all @change=\"selectAll($el.checked)\"")}</th>""");
        if (detail) sb.Append($"""<th class="{Html.Cx("ant-table-cell", "ant-table-row-expand-icon-cell", hasLeft ? "ant-table-cell-fix-left" : null)}"{(hasLeft ? $" style=\"position: sticky; left: {DragWidth + (Selectable ? SelectionWidth : 0)}px;\"" : "")}></th>""");
        for (var i = 0; i < visible.Count; i++)
        {
            var c = visible[i];
            var (cls, style) = Fix(visible, i, offsets, "m-grid-td");
            var menu = AllowColumnEdit
                ? $"""<button type="button" class="m-grid-th-menu" aria-label="{Html.E(c.Title)} column options" aria-haspopup="menu" @click.stop="openMenu($el, '{Html.E(c.Key)}', '{c.Fixed.ToString().ToLowerInvariant()}', {Js(Columns.IndexOf(c) == 0)}, {Js(Columns.IndexOf(c) == Columns.Count - 1)})">{AntIcons.Span("more")}</button>"""
                : "";
            sb.Append($"""<th class="{cls}{ColumnHeader.ThClass(Sortable, SortOf(c.Key))}"{style} data-key="{Html.E(c.Key)}"{ColumnHeader.AriaSort(Sortable, SortOf(c.Key))}>{Header(c, menu)}</th>""");
        }
        if (AllowDeleteRow) sb.Append("""<th class="m-grid-actions ant-table-cell ant-table-cell-fix-right ant-table-cell-fix-right-first" style="text-align: center; position: sticky; right: 0px;"><span class="m-sr-only">Actions</span></th>""");
        sb.Append("""</tr></thead><tbody class="ant-table-tbody" x-ref="body">""");
        var colspan = visible.Count + (RowDraggable ? 1 : 0) + (Selectable ? 1 : 0) + (detail ? 1 : 0) + (AllowDeleteRow ? 1 : 0);
        foreach (var row in Rows) sb.Append(Row(row, visible, offsets, errors, detail, colspan, culture, newRow: false, id));
        if (Rows.Count == 0)
            sb.Append($"""<tr class="ant-table-placeholder" x-ref="empty"><td class="ant-table-cell" colspan="{colspan}"><div class="m-table-empty">No rows yet. Use Add row to start.</div></td></tr>""");
        sb.Append("</tbody></table></div></div></div></div></div></div></div>");

        // The row the browser clones for "Add row" (__id__ is replaced by a fresh id).
        if (AllowAddRow)
        {
            var blank = new GridRecord("__id__");
            foreach (var c in Columns) blank[c.Key] = c.Type == GridCellType.Switch ? "false" : null;
            sb.Append($"""<template x-ref="rowTemplate">{Row(blank, visible, offsets, new(), detail, colspan, culture, newRow: true, id)}</template>""");
        }

        if (Paged) sb.Append(Pager());
        if (slots.TryGetValue("grid-summary", out var summary)) sb.Append($"""<div class="m-grid-summary">{summary}</div>""");
        sb.Append("</div>");

        if (AllowColumnEdit)
        {
            sb.Append(ColumnMenu());
            sb.Append(ColumnDialog(id));
        }
        output.Content.SetHtmlContent(sb.ToString());
    }

    // ---- Headers: sorting and column search, in the browser or (the nested grid) on the server ----

    // The band rows over the shown columns: the drag handle, checkbox and expand columns lead, the delete column ends.
    private string BandRows(IReadOnlyList<GridColumn> visible, bool detail) =>
        Modulus.Ui.Abstractions.HeaderGroups.Rows(
            [.. Enumerable.Repeat<string?>(null, (RowDraggable ? 1 : 0) + (Selectable ? 1 : 0) + (detail ? 1 : 0)),
             .. visible.Select(c => (string?)c.Key),
             .. (AllowDeleteRow ? new string?[] { null } : [])],
            HeaderGroups);

    private int DragWidth => RowDraggable ? SortableHtml.RowHandleWidth : 0;

    // A server sort (the nested grid) decides the order, so rows don't drag.
    private bool ServerSorted => SortHref is not null && SortField is not null;

    private static string HelpId(string gridId) => $"{gridId}-drag-help";

    private SortDirection SortOf(string key) =>
        SortHref is null || SortField != key ? SortDirection.None : SortDescending ? SortDirection.Descending : SortDirection.Ascending;

    private string Header(GridColumn c, string menu)
    {
        var key = Html.E(c.Key);
        var hx = ServerTarget is null ? "" : $""" hx-target="{Html.E(ServerTarget)}" hx-swap="outerHTML" """.TrimEnd();
        var sortHref = SortHref?.Invoke(c.Key);
        var sortAttributes = sortHref is not null
            ? $""" hx-get="{Html.E(sortHref)}"{hx}"""
            : $""" @click="sortBy('{key}')" @keydown.enter.prevent="sortBy('{key}')" @keydown.space.prevent="sortBy('{key}')" """.TrimEnd();
        return ColumnHeader.Content(c.Key, c.Title, GridSchema.Kind(c), c.Required,
            // The link is "#": a level's endpoint answers with a fragment, so only htmx follows it.
            sortable: Sortable, sort: SortOf(c.Key), sortHref: sortHref is null ? null : "#", sortAttributes: sortAttributes,
            searchable: ColumnSearchable, search: Searches?.GetValueOrDefault(c.Key), searchHref: SearchHref?.Invoke(c.Key), searchAttributes: SearchHref is null ? null : hx,
            // Option columns get a checkbox list; searched here, with each value's count (the server's level can't count).
            extra: menu, choices: GridSchema.SearchChoices(c), values: GridSchema.SearchValues(c, SearchHref is null ? Rows : null));
    }

    // ---- Rows and cells ----

    private string Row(GridRecord row, IReadOnlyList<GridColumn> visible, IReadOnlyDictionary<string, int> offsets,
        Dictionary<(string, string), string> errors, bool detail, int colspan, CultureInfo culture, bool newRow, string gridId)
    {
        var n = Name;
        var hasLeft = visible.Any(c => c.Fixed == GridFixed.Left);
        var sb = new StringBuilder($"""<tr data-row-key="{Html.E(row.Id)}" class="{Html.Cx("ant-table-row", "ant-table-row-level-0", newRow ? "m-grid-row-new" : null)}">""");
        // Hidden columns still post their values, so hiding a column and saving keeps its data.
        var index = $"""<input type="hidden" name="{Html.E(n)}.index" value="{Html.E(row.Id)}" />""" +
                    string.Concat(Columns.Where(c => c.Hidden).Select(c =>
                        $"""<input type="hidden" name="{Html.E(n)}[{Html.E(row.Id)}].{Html.E(c.Key)}" value="{Html.E(row[c.Key] ?? "")}" />"""));
        if (RowDraggable)
        {
            var label = visible.Count > 0 && GridSchema.Format(visible[0], row[visible[0].Key], culture) is { Length: > 0 } text ? text : "row";
            sb.Append($"""<td class="{Html.Cx("ant-table-cell", "m-row-drag-cell", hasLeft ? "ant-table-cell-fix-left" : null)}"{(hasLeft ? " style=\"position: sticky; left: 0px;\"" : "")}>{index}{SortableHtml.RowHandle(row.Id, label, HelpId(gridId), ServerSorted)}</td>""");
            index = "";
        }
        if (Selectable)
        {
            // The checkbox has no name: selection lives in the browser and is not posted with the grid.
            sb.Append($"""<td class="{Html.Cx("ant-table-cell", "ant-table-selection-column", hasLeft ? "ant-table-cell-fix-left" : null)}"{(hasLeft ? $" style=\"position: sticky; left: {DragWidth}px;\"" : "")}>{index}{Checkbox("Select row", " data-select-row @change=\"selectRow($el)\"")}</td>""");
            index = "";
        }
        if (detail)
        {
            var fix = hasLeft ? $" style=\"position: sticky; left: {DragWidth + (Selectable ? SelectionWidth : 0)}px;\"" : "";
            var button = newRow
                ? """<button type="button" class="ant-table-row-expand-icon ant-table-row-expand-icon-spaced" aria-hidden="true" tabindex="-1"></button>"""
                : """<button type="button" class="ant-table-row-expand-icon ant-table-row-expand-icon-collapsed" aria-label="Expand row" title="Show or hide the details" aria-expanded="false" @click="toggleRow($el)"></button>""";
            sb.Append($"""<td class="{Html.Cx("ant-table-cell", "ant-table-row-expand-icon-cell", hasLeft ? "ant-table-cell-fix-left" : null)}"{fix}>{index}{button}</td>""");
            index = "";
        }
        for (var i = 0; i < visible.Count; i++)
        {
            var c = visible[i];
            var (cls, style) = Fix(visible, i, offsets, "m-grid-td");
            sb.Append($"""<td class="{cls}"{style} data-key="{Html.E(c.Key)}" data-label="{Html.E(c.Title)}">{index}{Cell(row, c, errors.GetValueOrDefault((row.Id, c.Key)), culture)}</td>""");
            index = "";
        }
        if (AllowDeleteRow)
            sb.Append($"""<td class="m-grid-actions ant-table-cell ant-table-cell-fix-right ant-table-cell-fix-right-first" style="text-align: center; position: sticky; right: 0px;">{index}<button type="submit" name="{Html.E(n)}.op" value="delete-row:{Html.E(row.Id)}" class="ant-btn ant-btn-text ant-btn-sm ant-btn-icon-only ant-btn-dangerous" aria-label="Delete row" title="Delete row" @click.prevent="deleteRow($el)">{AntIcons.Span("delete")}</button></td>""");
        sb.Append("</tr>");
        if (detail && !newRow)
        {
            using var writer = new StringWriter();
            RowDetail!(row).WriteTo(writer, HtmlEncoder.Default);
            sb.Append($"""<tr class="ant-table-expanded-row ant-table-expanded-row-level-1" data-detail-for="{Html.E(row.Id)}" style="display: none;"><td class="ant-table-cell" colspan="{colspan}">{writer}</td></tr>""");
        }
        return sb.ToString();
    }

    private string Cell(GridRecord row, GridColumn c, string? error, CultureInfo culture)
    {
        var value = row[c.Key];
        var field = $"{Name}[{row.Id}].{c.Key}";
        var message = error is null ? "" : $"""<div class="m-grid-cell-message" role="alert">{Html.E(error)}</div>""";
        var errorClass = error is null ? null : "m-grid-cell-error";

        if (c.Type == GridCellType.Switch)
        {
            var on = GridSchema.Flag(value);
            var click = c.Editable ? " @click=\"on = !on; $dispatch('grid-change')\"" : "";
            return $"""<div class="{Html.Cx("m-grid-cell", errorClass)}" tabindex="-1"><button type="button" role="switch" aria-label="{Html.E(c.Title)}" aria-checked="{Js(on)}" class="{Html.Cx("ant-switch", "ant-switch-small", on ? "ant-switch-checked" : null, c.Editable ? null : "ant-switch-disabled")}"{(c.Editable ? "" : " disabled")} x-data="{"{ on: " + Js(on) + " }"}" :aria-checked="on" :class="on && 'ant-switch-checked'"{click}><div class="ant-switch-handle"></div><span class="ant-switch-inner"></span><input type="hidden" name="{Html.E(field)}" value="{Js(on)}" :value="on" /></button></div>{message}""";
        }
        if (c.Type == GridCellType.Rating)
        {
            var count = (int)(c.Max ?? 5);
            var v = (int)Math.Round(GridSchema.Number(value) ?? 0);
            var star = AntIcons.Span("star", IconThemeType.Fill);
            var sb = new StringBuilder($"""<div class="{Html.Cx("m-grid-cell", errorClass)}" tabindex="-1"><span class="m-rate" x-data="{"{ v: " + v + ", h: 0 }"}"><ul class="{Html.Cx("m-grid-rate", "ant-rate", c.Editable ? null : "ant-rate-disabled")}" role="radiogroup" aria-label="{Html.E(c.Title)}" @mouseleave="h = 0">""");
            for (var i = 1; i <= count; i++)
            {
                var click = c.Editable ? $""" @click="v = v === {i} ? 0 : {i}; $dispatch('grid-change')" @mouseenter="h = {i}" @keydown.enter.prevent="v = {i}; $dispatch('grid-change')" """.TrimEnd() : "";
                sb.Append($"""<li class="ant-rate-star {(v >= i ? "ant-rate-star-full" : "ant-rate-star-zero")}" :class="(h || v) >= {i} ? 'ant-rate-star-full' : 'ant-rate-star-zero'"><div role="radio" aria-label="{i} star{(i == 1 ? "" : "s")}" aria-checked="{Js(v == i)}" :aria-checked="v === {i}" aria-posinset="{i}" aria-setsize="{count}" tabindex="{(c.Editable ? 0 : -1)}"{click}><div class="ant-rate-star-first">{star}</div><div class="ant-rate-star-second">{star}</div></div></li>""");
            }
            sb.Append($"""</ul><input type="hidden" name="{Html.E(field)}" value="{v}" :value="v" /></span></div>{message}""");
            return sb.ToString();
        }

        var view = View(c, row, culture);
        if (!c.Editable)
            return $"""<div class="{Html.Cx("m-grid-cell", errorClass)}">{view}</div><input type="hidden" name="{Html.E(field)}" value="{Html.E(value)}" />{message}""";

        var label = $"Edit {c.Title}: {GridSchema.Format(c, value, culture)}";
        return $"""<div class="{Html.Cx("m-grid-cell", "m-grid-cell-editable", errorClass)}" tabindex="0" role="button" aria-label="{Html.E(label)}"{(error is null ? "" : $" title=\"{Html.E(error)}\"")} @click="edit($el)" @keydown.enter.prevent="edit($el)" @keydown.f2.prevent="edit($el)">{view}</div>""" +
               $"""<div class="m-grid-cell m-grid-cell-editing" hidden @focusout="leave($event)" @keydown.enter.prevent="commit($el)" @keydown.escape.stop.prevent="cancel($el)"{EditorEvents(c)}>{Editor(c, field, value, row.Id)}</div>{message}""";
    }

    /// <summary>The read-only cell content; mirrors EditableGrid.View and modulusGrid.view() in modulus-widgets.js.</summary>
    internal static string View(GridColumn c, GridRecord row, CultureInfo culture)
    {
        var value = row[c.Key];
        switch (c.Type)
        {
            case GridCellType.Tag when GridSchema.Option(c, value) is { } tag:
                return Tag(tag);
            case GridCellType.MultiSelect when GridSchema.Many(value).Count > 0:
                return $"""<span class="m-grid-tags">{string.Concat(GridSchema.Many(value).Select(v => GridSchema.Option(c, v) is { } o ? Tag(o) : $"<span class=\"ant-tag\">{Html.E(v)}</span>"))}</span>""";
            case GridCellType.Progress when GridSchema.Number(value) is { } p:
                var percent = Math.Clamp(p, 0, 100);
                var done = percent >= 100;
                var text = done ? AntIcons.Span("check-circle", IconThemeType.Fill) : $"{percent:0}%";
                return $"""<div class="ant-progress ant-progress-status-{(done ? "success" : "normal")} ant-progress-small ant-progress-line ant-progress-show-info"><div class="ant-progress-outer"><div class="ant-progress-inner"><div class="ant-progress-bg" style="width: {percent.ToString("0.##", CultureInfo.InvariantCulture)}%; height: 6px;"></div></div></div><span title="{percent:0}%" class="ant-progress-text">{text}</span></div>""";
            default:
                var formatted = GridSchema.Format(c, value, culture);
                if (formatted.Length == 0) return """<span class="m-grid-empty">Add</span>""";
                var icon = c.Type == GridCellType.Select && GridSchema.Option(c, value)?.Icon is { } name ? AntIcons.Span(name) : "";
                if (c.Avatar || c.SubtitleKey is not null)
                {
                    var subtitle = c.SubtitleKey is { } sub ? row[sub] : null;
                    var avatar = c.Avatar ? $"""<span class="m-avatar m-grid-avatar m-av-{GridSchema.Tint(formatted)}" aria-hidden="true">{Html.E(GridSchema.Initials(formatted))}</span>""" : "";
                    var desc = string.IsNullOrEmpty(subtitle) ? "" : $"""<span class="m-grid-entity-desc">{Html.E(subtitle)}</span>""";
                    return $"""<span class="m-grid-entity">{avatar}<span class="m-grid-entity-text"><span class="m-grid-entity-title">{icon}{Html.E(formatted)}</span>{desc}</span></span>""";
                }
                if (icon.Length > 0) return $"""<span class="m-grid-icon-text">{icon}{Html.E(formatted)}</span>""";
                return $"""<span{(c.AlignRight ? " class=\"m-num\"" : "")}>{Html.E(formatted)}</span>""";
        }
    }

    private static string Tag(GridOption o) =>
        $"""<span class="{Html.Cx("ant-tag", o.Color is null or "default" ? null : $"ant-tag-{o.Color}")}">{(o.Icon is null ? "" : AntIcons.Span(o.Icon))}{Html.E(o.Label)}</span>""";

    private static string Checkbox(string label, string attributes) =>
        $"""<label class="ant-checkbox-wrapper"><span class="ant-checkbox"><input type="checkbox" class="ant-checkbox-input" aria-label="{Html.E(label)}"{attributes} /><span class="ant-checkbox-inner"></span></span></label>""";

    // The selection bar, as ProTable's: count, Clear, then "Set value", the app's actions, Export selected and Delete.
    private string BatchBar(string? extra)
    {
        var sb = new StringBuilder("""<div class="ant-alert ant-alert-info ant-alert-no-icon m-table-alert m-grid-batch" role="status" x-show="selected > 0" style="display: none;"><div class="ant-alert-content"><div class="ant-alert-message">""");
        sb.Append("""<span>Selected <strong x-text="selected"></strong> <span x-text="selected === 1 ? 'row' : 'rows'"></span></span>""");
        sb.Append("""<button type="button" class="ant-btn ant-btn-link ant-btn-sm" @click="selectAll(false)"><span>Clear</span></button><span class="m-spacer"></span>""");
        var batchColumns = Columns.Where(c => c.Editable && c.Type is GridCellType.Select or GridCellType.Tag or GridCellType.Switch).ToList();
        if (batchColumns.Count > 0)
        {
            sb.Append("""<select class="m-select m-grid-batch-set" aria-label="Set a value on the selected rows" @change="setSelected($el)"><option value="">Set value…</option>""");
            foreach (var c in batchColumns)
            {
                sb.Append($"""<optgroup label="{Html.E(c.Title)}">""");
                foreach (var o in GridSchema.FilterOptions(c)) sb.Append($"""<option value="{Html.E($"{c.Key}={o.Value}")}">{Html.E(o.Label)}</option>""");
                sb.Append("</optgroup>");
            }
            sb.Append("</select>");
        }
        sb.Append(extra);
        sb.Append($"""<button type="button" class="ant-btn ant-btn-default ant-btn-sm" @click="exportCsv(true)">{AntIcons.Span("download")}<span>Export selected</span></button>""");
        if (AllowDeleteRow)
            sb.Append($"""<button type="button" class="ant-btn ant-btn-default ant-btn-dangerous ant-btn-sm" data-m-grid-delete-selected @click="deleteSelected()">{AntIcons.Span("delete")}<span>Delete</span></button>""");
        return sb.Append("</div></div></div>").ToString();
    }

    // The option select ends the edit when it closes and posts "Create new …" (add-option); the date picker ends it on
    // a pick. On the editor, so the grid's commit and submitOp answer (inside the widget its own methods would).
    private static string EditorEvents(GridColumn c) => c.Type switch
    {
        GridCellType.Select or GridCellType.Tag or GridCellType.MultiSelect => """ @m-cselect-close="commit($el)" @m-cselect-add="submitOp($event.detail)" """.TrimEnd(),
        GridCellType.Date => """ @m-picker-pick="commit($el)" """.TrimEnd(),
        _ => "",
    };

    private static string SafeId(string value) =>
        "m-cselect-" + string.Concat(value.Select(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_'));

    private string Editor(GridColumn c, string field, string? value, string rowId)
    {
        var name = Html.E(field);
        var aria = Html.E(c.Title);
        switch (c.Type)
        {
            case GridCellType.Number or GridCellType.Currency or GridCellType.Percent or GridCellType.Progress:
                return $"""<input type="number" class="ant-input m-num" name="{name}" value="{Html.E(value)}" step="any" inputmode="decimal"{Html.Attr("min", c.Min?.ToString(CultureInfo.InvariantCulture))}{Html.Attr("max", c.Max?.ToString(CultureInfo.InvariantCulture))} aria-label="{aria}" />""";
            case GridCellType.Date:
                // Ant Design's picker popup (modulusPicker, as app-date-picker), like the Blazor grid's DatePicker; a
                // pick ends the edit (m-picker-pick). The hidden input posts yyyy-MM-dd.
                var date = GridSchema.Date(value) is { } d ? d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "";
                var cfg = Widgets.Config(new { picker = "date", showTime = false, values = new[] { date }, disabled = false });
                return $"""<div class="ant-picker m-picker" style="width: 100%;" x-data="modulusPicker({cfg})" @click.outside="close(true)" @mousedown="rootDown($event)">""" +
                       """<div class="ant-picker-input">""" +
                       PickerText.TextInput(0, PickerText.Placeholder("date"), date, null, false, false, "") +
                       $"""<span class="ant-picker-suffix" aria-hidden="true">{AntIcons.Span("calendar")}</span>""" +
                       (c.Required ? "" : PickerText.Clear("values[0]", false)) + "</div>" +
                       PickerText.Hidden(field, date, 0) + PickerText.Drop + "</div>";
            case GridCellType.Select or GridCellType.Tag or GridCellType.MultiSelect:
                // The Blazor grid's AppCreatableSelect (modulusCreatableSelect): coloured options, a check on the picked
                // ones and "Create new …", which posts add-option:{key}:{row} so the option joins the column's schema.
                var multiple = c.Type == GridCellType.MultiSelect;
                return CreatableSelectTagHelper.Render(new CreatableSelectModel(field, SafeId($"{field}-list"), c.Options)
                {
                    Multiple = multiple, Value = multiple ? null : value, Values = multiple ? GridSchema.Many(value) : [],
                    AllowClear = !c.Required, AriaLabel = c.Title, AddPlaceholder = "New option",
                    CreateText = $"Create new {c.Title.ToLowerInvariant()}", AllowAdd = c.Options.Count < GridSchema.MaxOptions,
                    GridOp = $"add-option:{c.Key}:{rowId}", OptionField = EditableGridForm.OptionField(Name, rowId, c.Key),
                    ColorField = EditableGridForm.ColorField(Name, rowId, c.Key),
                }, " style=\"width: 100%;\"");
            default:
                return $"""<input type="text" class="ant-input" name="{name}" value="{Html.E(value)}" maxlength="500" aria-label="{aria}" />""";
        }
    }

    private (string Class, string Style) Fix(IReadOnlyList<GridColumn> visible, int i, IReadOnlyDictionary<string, int> offsets, string extra)
    {
        var c = visible[i];
        var align = c.AlignRight ? "text-align: right; " : "";
        if (c.Type == GridCellType.Switch) extra += " m-grid-td-center"; // yes/no columns read best centred
        switch (c.Fixed)
        {
            case GridFixed.Left:
                var last = i + 1 >= visible.Count || visible[i + 1].Fixed != GridFixed.Left;
                return (Html.Cx(extra, "ant-table-cell", "ant-table-cell-fix-left", last ? "ant-table-cell-fix-left-last" : null),
                        $" style=\"{align}position: sticky; left: {offsets[c.Key]}px;\"");
            case GridFixed.Right:
                var first = i == 0 || visible[i - 1].Fixed != GridFixed.Right;
                return (Html.Cx(extra, "ant-table-cell", "ant-table-cell-fix-right", first ? "ant-table-cell-fix-right-first" : null),
                        $" style=\"{align}position: sticky; right: {offsets[c.Key] + (AllowDeleteRow ? ActionsWidth : 0)}px;\"");
            default:
                return (Html.Cx(extra, "ant-table-cell"), align.Length > 0 ? $" style=\"{align.TrimEnd()}\"" : "");
        }
    }

    // ---- Column UI ----

    private string ColumnSettings()
    {
        var n = Html.E(Name);
        var sb = new StringBuilder();
        for (var i = 0; i < Columns.Count; i++)
        {
            var c = Columns[i];
            var key = Html.E(c.Key);
            var title = Html.E(c.Title);
            var shown = !c.Hidden;
            sb.Append($"""<li class="m-grid-columns-item"><label class="{Html.Cx("ant-checkbox-wrapper", shown ? "ant-checkbox-wrapper-checked" : null)}"><span class="{Html.Cx("ant-checkbox", shown ? "ant-checkbox-checked" : null)}"><input type="checkbox" class="ant-checkbox-input"{(shown ? " checked" : "")} @change="submitOp('toggle:{key}')" /><span class="ant-checkbox-inner"></span></span><span>{title}</span></label>""");
            if (AllowColumnEdit)
                sb.Append("""<span class="m-grid-columns-tools">""")
                  .Append(Tool($"pin:{key}:{(c.Fixed == GridFixed.Left ? "none" : "left")}", "vertical-align-top", -90, $"Pin {title} to the left", c.Fixed == GridFixed.Left, false))
                  .Append(Tool($"pin:{key}:{(c.Fixed == GridFixed.Right ? "none" : "right")}", "vertical-align-top", 90, $"Pin {title} to the right", c.Fixed == GridFixed.Right, false))
                  .Append(Tool($"move:{key}:-1", "arrow-up", 0, $"Move {title} up", false, i == 0))
                  .Append(Tool($"move:{key}:1", "arrow-down", 0, $"Move {title} down", false, i == Columns.Count - 1))
                  .Append("</span>");
            sb.Append("</li>");
        }
        return $$"""<div class="m-dropdown" x-data="dropdown" @click.outside="close()" @keydown.escape="close()"><div class="m-dropdown-trigger ant-dropdown-trigger" @click="toggle()" :class="open && 'ant-dropdown-open'" aria-haspopup="dialog" :aria-expanded="open"><button type="button" class="ant-btn ant-btn-default m-table-columns-toggle" aria-label="Columns" title="Show, hide, pin and order columns">{{AntIcons.Span("setting")}}<span>Columns</span></button></div>""" +
               $$"""<div class="ant-popover ant-popover-placement-bottomRight m-dropdown-panel m-dropdown-right m-grid-columns-popover" x-show="open" x-transition.opacity x-cloak><div class="ant-popover-content"><div class="ant-popover-inner" role="dialog" aria-label="Columns"><div class="ant-popover-title"><div class="m-grid-columns-head"><span>Columns</span><span class="m-muted">{{Columns.Count(c => !c.Hidden)}} of {{Columns.Count}} shown</span></div></div>""" +
               $$"""<div class="ant-popover-inner-content"><ul class="m-grid-columns" role="list">{{sb}}</ul></div></div></div></div></div>""";

        string Tool(string op, string icon, int rotate, string label, bool active, bool disabled) =>
            $"""<button type="submit" name="{n}.op" value="{op}" class="{Html.Cx("m-grid-tool", active ? "m-grid-tool-active" : null)}" aria-label="{label}" title="{label}"{(active ? " aria-pressed=\"true\"" : "")}{(disabled ? " disabled" : "")}>{Rotated(icon, rotate)}</button>""";
    }

    private static string Rotated(string icon, int rotate) => rotate == 0
        ? AntIcons.Span(icon)
        : AntIcons.Span(icon).Replace("<svg ", $"<svg style=\"transform: rotate({rotate}deg);\" ", StringComparison.Ordinal);

    // One floating menu for every column header: a menu inside the scrolling table would be clipped.
    private string ColumnMenu()
    {
        var n = Html.E(Name);
        string Item(string op, string icon, string label, string show = "true", string extra = "") =>
            $"""<li role="none" class="{Html.Cx("ant-dropdown-menu-item", extra)}" x-show="{show}"><button type="submit" role="menuitem" name="{n}.op" :value="{op}" :disabled="!menu.key"><span class="ant-dropdown-menu-title-content">{AntIcons.Span(icon)}<span>{label}</span></span></button></li>""";
        return """<div class="ant-dropdown ant-dropdown-placement-bottomRight m-grid-menu" x-show="menu.key" x-transition.opacity :style="menu.style" style="display: none;" @click.outside="closeMenu()">""" +
               """<ul class="ant-dropdown-menu ant-dropdown-menu-root ant-dropdown-menu-vertical ant-dropdown-menu-light" role="menu">""" +
               Item("'pin:' + menu.key + ':left'", "pushpin", "Pin left", "menu.fixed !== 'left'") +
               Item("'pin:' + menu.key + ':right'", "pushpin", "Pin right", "menu.fixed !== 'right'") +
               Item("'pin:' + menu.key + ':none'", "close", "Unpin", "menu.fixed !== 'none'") +
               """<li class="ant-dropdown-menu-item-divider" role="separator"></li>""" +
               Item("'move:' + menu.key + ':-1'", "arrow-left", "Move left", "!menu.first") +
               Item("'move:' + menu.key + ':1'", "arrow-right", "Move right", "!menu.last") +
               Item("'hide:' + menu.key", "eye-invisible", "Hide column") +
               """<li class="ant-dropdown-menu-item-divider" role="separator"></li>""" +
               Item("'remove:' + menu.key", "delete", "Remove column", "true", "ant-dropdown-menu-item-danger") +
               "</ul></div>";
    }

    private string ColumnDialog(string gridId)
    {
        var n = Html.E(Name);
        var titleId = $"{gridId}-add-column";
        var types = string.Concat(Enum.GetValues<GridCellType>().Select(t =>
            $"""<option value="{t}">{(t switch { GridCellType.MultiSelect => "Multi-select", GridCellType.Tag => "Tag (status)", _ => t.ToString() })}</option>"""));
        string Radio(string value, string label, bool on) =>
            $$"""<label class="{{Html.Cx("ant-radio-button-wrapper", on ? "ant-radio-button-wrapper-checked" : null)}}" :class="{ 'ant-radio-button-wrapper-checked': newFixed === '{{value}}' }"><span class="{{Html.Cx("ant-radio-button", on ? "ant-radio-button-checked" : null)}}" :class="{ 'ant-radio-button-checked': newFixed === '{{value}}' }"><input type="radio" class="ant-radio-button-input" name="{{n}}.new.fixed" value="{{value}}"{{(on ? " checked" : "")}} x-model="newFixed" /><span class="ant-radio-button-inner"></span></span><span>{{label}}</span></label>""";
        return $$"""<div class="m-modal" x-show="columnOpen" x-cloak style="display: none;" @keydown.escape.window="columnOpen = false">""" +
               """<div class="ant-modal-mask" x-show="columnOpen" x-transition.opacity></div>""" +
               $$"""<div class="ant-modal-wrap" role="dialog" aria-modal="true" aria-labelledby="{{titleId}}" @click.self="columnOpen = false"><div class="ant-modal" style="width: 480px;"><div class="ant-modal-content">""" +
               $$"""<button type="button" class="ant-modal-close" aria-label="Close" @click="columnOpen = false"><span class="ant-modal-close-x">{{AntIcons.Span("close", IconThemeType.Outline, "ant-modal-close-icon")}}</span></button>""" +
               $$"""<div class="ant-modal-header"><div class="ant-modal-title" id="{{titleId}}">Add column</div></div><div class="ant-modal-body"><div class="ant-form ant-form-vertical">""" +
               $$"""<div class="ant-form-item"><div class="ant-form-item-label"><label for="{{titleId}}-title" class="ant-form-item-required">Title</label></div><input type="text" id="{{titleId}}-title" class="ant-input" name="{{n}}.new.title" maxlength="60" placeholder="e.g. Region" x-model="newTitle" x-ref="newTitle" @keydown.enter.prevent="newTitle.trim() && submitOp('add-column')" /></div>""" +
               $$"""<div class="ant-form-item"><div class="ant-form-item-label"><label for="{{titleId}}-type">Type</label></div><select id="{{titleId}}-type" class="m-select" name="{{n}}.new.type" x-model="newType">{{types}}</select></div>""" +
               $$"""<div class="ant-form-item" x-show="newType === 'Select' || newType === 'MultiSelect' || newType === 'Tag'" style="display: none;"><div class="ant-form-item-label"><label for="{{titleId}}-options">Options</label></div><textarea id="{{titleId}}-options" class="ant-input" name="{{n}}.new.options" rows="3" placeholder="One per line, or comma-separated"></textarea></div>""" +
               $$"""<div class="ant-form-item"><div class="ant-form-item-label"><label>Pin</label></div><div><div class="ant-radio-group ant-radio-group-solid">{{Radio("None", "None", true)}}{{Radio("Left", "Left", false)}}{{Radio("Right", "Right", false)}}</div></div></div>""" +
               $$"""<div class="ant-form-item" style="margin-bottom: 0"><label class="ant-checkbox-wrapper" :class="{ 'ant-checkbox-wrapper-checked': newRequired }"><span class="ant-checkbox" :class="{ 'ant-checkbox-checked': newRequired }"><input type="checkbox" class="ant-checkbox-input" name="{{n}}.new.required" value="true" x-model="newRequired" /><span class="ant-checkbox-inner"></span></span><span>Required</span></label></div>""" +
               $$"""</div></div><div class="ant-modal-footer"><button type="button" class="ant-btn ant-btn-default" @click="columnOpen = false"><span>Cancel</span></button><button type="submit" name="{{n}}.op" value="add-column" class="ant-btn ant-btn-primary" :disabled="!newTitle.trim()"><span>Add column</span></button></div>""" +
               "</div></div></div></div>";
    }

    // ---- Client config ----

    // The data table's pager, run in the browser by modulusGrid (pageItems, goPage, setPageSize): the range, the page
    // window with ••• jumps (Paging.Window), and rows per page.
    private string Pager()
    {
        var sizes = string.Concat(Paging.SizesWith(PageSize).Select(s =>
            $"""<option value="{s}"{(s == PageSize ? " selected" : "")}>{s} / page</option>"""));
        return """<ul class="ant-pagination ant-pagination-mini ant-table-pagination ant-table-pagination-right m-grid-pager" aria-label="Pagination" x-show="matched > 0">""" +
               """<li class="ant-pagination-total-text" x-text="rangeText()"></li>""" +
               $"""<li class="ant-pagination-prev" :class="page <= 1 && 'ant-pagination-disabled'" :aria-disabled="page <= 1"><button type="button" class="ant-pagination-item-link" aria-label="Previous page" :disabled="page <= 1" @click="goPage(page - 1)">{AntIcons.Span("left")}</button></li>""" +
               """<template x-for="item in pageItems()" :key="item.key">""" +
               """<li :class="item.cls" :aria-current="item.active ? 'page' : null" :title="item.title">""" +
               """<a :class="item.page ? '' : 'ant-pagination-item-link'" href="#" role="button" :aria-label="item.title" @click.prevent="goPage(item.to)">""" +
               """<span x-show="item.page" x-text="item.page"></span>""" +
               """<span class="ant-pagination-item-container" x-show="!item.page"><span class="ant-pagination-item-ellipsis">•••</span></span></a></li></template>""" +
               $"""<li class="ant-pagination-next" :class="page >= pages() && 'ant-pagination-disabled'" :aria-disabled="page >= pages()"><button type="button" class="ant-pagination-item-link" aria-label="Next page" :disabled="page >= pages()" @click="goPage(page + 1)">{AntIcons.Span("right")}</button></li>""" +
               $"""<li class="ant-pagination-options"><select class="m-select m-select-sm m-pager-size" aria-label="Rows per page" @change="setPageSize($el.value)">{sizes}</select></li></ul>""";
    }

    private object ClientConfig(CultureInfo culture)
    {
        string currency;
        try { currency = new RegionInfo(culture.Name).ISOCurrencySymbol; }
        catch (ArgumentException) { currency = "USD"; }
        return new
        {
            name = Name,
            lang = string.IsNullOrEmpty(culture.Name) ? "en-US" : culture.Name,
            currency,
            datePattern = culture.DateTimeFormat.ShortDatePattern,
            dirty = Dirty,
            paged = Paged,
            pageSize = Math.Max(1, PageSize),
            dragLocked = !RowDraggable || ServerSorted,
            check = AntIcons.Span("check-circle", IconThemeType.Fill),
            file = $"{ExportFileName ?? HeaderTitle ?? "grid"}.csv",
            columns = Columns.ToDictionary(c => c.Key, c => new
            {
                t = c.Title,
                type = c.Type.ToString(),
                req = c.Required,
                min = c.Min,
                max = c.Max,
                right = c.AlignRight,
                avatar = c.Avatar,
                sub = c.SubtitleKey,
                o = c.Options.ToDictionary(o => o.Value, o => new[] { o.Label, o.Color, o.Icon is null ? null : AntIcons.Span(o.Icon) }),
            }),
        };
    }

    private static string Js(bool value) => value ? "true" : "false";
}

/// <summary>Buttons and inputs shown right of the title, before the grid's own actions.</summary>
[HtmlTargetElement("grid-toolbar", ParentTag = "app-editable-grid")]
public class GridToolbarTagHelper : GridSlotTagHelper { }

/// <summary>Content after the grid title (a count badge).</summary>
[HtmlTargetElement("grid-title-extra", ParentTag = "app-editable-grid")]
public class GridTitleExtraTagHelper : GridSlotTagHelper { }

/// <summary>Your own filter row (replaces the built-in one; the nested grid filters on the server this way).</summary>
[HtmlTargetElement("grid-filters", ParentTag = "app-editable-grid")]
public class GridFiltersTagHelper : GridSlotTagHelper { }

/// <summary>More actions in the batch bar (they act on the checked rows: <c>input[data-select-row]:checked</c>).</summary>
[HtmlTargetElement("grid-batch", ParentTag = "app-editable-grid")]
public class GridBatchTagHelper : GridSlotTagHelper { }

/// <summary>Content under the table (totals).</summary>
[HtmlTargetElement("grid-summary", ParentTag = "app-editable-grid")]
public class GridSummaryTagHelper : GridSlotTagHelper { }

public abstract class GridSlotTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(EditableGridTagHelper), out var slots))
            ((Dictionary<string, string>)slots)[context.TagName] = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}
