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
    // ---- Headers: sorting and column search, in the browser or (the nested grid) on the server ----

    // The band rows over the shown columns: the drag handle, checkbox and expand columns lead, the delete column ends.
    // The pinned columns: the leading helpers and pinned-left columns, and the trailing pinned-right columns and delete column.
    private string BandRows(IReadOnlyList<GridColumn> visible, bool detail)
    {
        var left = (RowDraggable ? 1 : 0) + (Selectable ? 1 : 0) + (detail ? 1 : 0) + visible.TakeWhile(c => c.Fixed == GridFixed.Left).Count();
        var right = (AllowDeleteRow ? 1 : 0) + visible.Reverse().TakeWhile(c => c.Fixed == GridFixed.Right).Count();
        return Modulus.Ui.Abstractions.HeaderGroups.Rows(
            [.. Enumerable.Repeat<string?>(null, (RowDraggable ? 1 : 0) + (Selectable ? 1 : 0) + (detail ? 1 : 0)),
             .. visible.Select(c => (string?)c.Key),
             .. (AllowDeleteRow ? new string?[] { null } : [])],
            HeaderGroups, left, right);
    }

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
}
