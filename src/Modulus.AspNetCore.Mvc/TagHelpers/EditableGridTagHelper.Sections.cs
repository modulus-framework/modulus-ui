// The sections of the editable grid's markup, one method each. ProcessAsync lays them out in order; the markup is the same as
// before the split (the MVC tests check it).
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

public partial class EditableGridTagHelper : TagHelper
{
    /// <summary>What the grid's sections share: the id and culture, the visible columns with their sticky offsets, the row errors and the widths.</summary>
    private sealed record GridFrame(string Id, CultureInfo Culture, bool FilterRowOpen, IReadOnlyList<GridColumn> Visible,
        IReadOnlyDictionary<string, int> Offsets, Dictionary<(string RowId, string Key), string> Errors, bool Detail,
        bool HasLeft, bool HasRight, int Width, int Colspan);

    /// <summary>The grid's own element: its id, classes, Alpine state and the listeners of the grid.</summary>
    private void ConfigureTag(TagHelperOutput output, GridFrame frame)
    {
        var id = frame.Id;
        var culture = frame.Culture;
        var filterRowOpen = frame.FilterRowOpen;
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("id", id);
        output.AddClasses("m-table-card", "m-grid", "ant-card");
        if (Resizable) output.AddClasses("m-resizable");
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
    }

    /// <summary>The toolbar above the grid: title, unsaved changes, search, the icon buttons and the More menu.</summary>
    private void Toolbar(StringBuilder sb, Dictionary<string, string> slots)
    {
        var n = Name;
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
    }

    /// <summary>The filter row: the app's own (grid-filters), or a dropdown per filterable column.</summary>
    private void FilterRow(StringBuilder sb, Dictionary<string, string> slots)
    {
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
    }

    /// <summary>The alert that summarises the row errors.</summary>
    private string ErrorAlert(Dictionary<(string RowId, string Key), string> errors) =>
        $"""<div class="ant-alert ant-alert-error m-table-alert" role="alert">{AntIcons.Span("close-circle", IconThemeType.Fill, "ant-alert-icon")}<div class="ant-alert-content"><div class="ant-alert-message">{Html.E(GridSchema.ErrorSummary(errors.Select(e => new GridError(e.Key.Item1, e.Key.Item2, e.Value))))}</div></div></div>""";

    /// <summary>The table: the column widths, the header and the rows (the placeholder when there are none).</summary>
    private void Table(StringBuilder sb, GridFrame frame)
    {
        var (id, culture, filterRowOpen, visible, offsets, errors, detail, hasLeft, hasRight, width, colspan) = frame;
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
        foreach (var row in Rows) sb.Append(Row(row, visible, offsets, errors, detail, colspan, culture, newRow: false, id));
        if (Rows.Count == 0)
            sb.Append($"""<tr class="ant-table-placeholder" x-ref="empty"><td class="ant-table-cell" colspan="{colspan}"><div class="m-table-empty">No rows yet. Use Add row to start.</div></td></tr>""");
        sb.Append("</tbody></table></div></div></div></div></div></div></div>");
    }

    /// <summary>The row the browser clones for "Add row" (__id__ is replaced by a fresh id).</summary>
    private string RowTemplate(GridFrame frame)
    {
        var (id, culture, _, visible, offsets, _, detail, _, _, _, colspan) = frame;
        var blank = new GridRecord("__id__");
        foreach (var c in Columns) blank[c.Key] = c.Type == GridCellType.Switch ? "false" : null;
        return $"""<template x-ref="rowTemplate">{Row(blank, visible, offsets, new(), detail, colspan, culture, newRow: true, id)}</template>""";
    }
}
