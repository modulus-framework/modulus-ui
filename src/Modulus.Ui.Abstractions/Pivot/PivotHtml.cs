using System.Globalization;
using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.Ui.Abstractions.Pivot;

/// <summary>
/// Markup of the pivot table (<c>AppPivotGrid</c> in Blazor, <c>&lt;app-pivot-grid&gt;</c> in MVC), in the manner of
/// Odoo's pivot view: a toolbar (Measures, Flip axis, Expand all, Download) and a bordered table whose "Total" headers
/// open into groups.
/// </summary>
/// <remarks>
/// Every control is a link to the next state (<see cref="PivotState"/>), so the table needs no script of its own: the
/// page follows the link (htmx boost or Blazor enhanced navigation) and renders again. The header dropdowns are
/// <c>details.m-dropdown</c>, opened and closed by modulus-shell.js like every other menu.
/// </remarks>
public static class PivotHtml
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>The pivot table.</summary>
    /// <param name="href">The URL of a state (the page's own URL with the state's query; see <see cref="PivotState.ToQuery"/>).</param>
    /// <param name="cellHref">The drill-down link of a value cell (the list of its records), or null for none.</param>
    /// <param name="downloadHref">The xlsx download of the current state (<see cref="PivotXlsx"/>), or null to hide the button.</param>
    /// <param name="id">Element id of the pivot.</param>
    /// <param name="label">Accessible name of the table.</param>
    public static string Render(PivotResult result, Func<PivotState, string> href, Func<PivotCell, string?>? cellHref = null,
        string? downloadHref = null, string? id = null, string? label = null)
    {
        var sb = new StringBuilder($"""<div class="m-pivot"{(id is { Length: > 0 } ? $" id=\"{E(id)}\"" : "")}>""");
        Toolbar(sb, result, href, downloadHref);

        if (result.Error is { } error)
        {
            sb.Append($"""<div class="ant-alert ant-alert-warning ant-alert-no-icon m-pivot-alert" role="alert"><div class="ant-alert-content"><div class="ant-alert-message">{E(error)}</div></div></div>""");
        }
        else if (result.IsEmpty)
        {
            sb.Append("""<div class="m-table-empty m-pivot-empty">No data to display</div>""");
        }
        else
        {
            Table(sb, result, href, cellHref, label);
        }
        return sb.Append("</div>").ToString();
    }

    // Toolbar --------------------------------------------------------------------------------------------------

    private static void Toolbar(StringBuilder sb, PivotResult result, Func<PivotState, string> href, string? downloadHref)
    {
        var state = result.State;
        sb.Append("""<div class="m-pivot-toolbar">""");

        // Measures: every measure, the ones shown checked; Count last, under a divider.
        sb.Append("""<details class="m-dropdown m-pivot-measures"><summary class="ant-btn ant-btn-primary m-dropdown-trigger" aria-haspopup="menu">""")
          .Append("<span>Measures</span>").Append(ShellRenderer.Icon("down"))
          .Append("""</summary><div class="ant-dropdown m-dropdown-panel m-dropdown-left"><ul class="ant-dropdown-menu ant-dropdown-menu-root ant-dropdown-menu-vertical ant-dropdown-menu-light" role="menu">""");
        foreach (var m in result.AllMeasures)
        {
            if (m.Key == PivotQuery<object>.Count && result.AllMeasures.Count > 1)
                sb.Append("""<li class="ant-dropdown-menu-item-divider" role="separator"></li>""");
            var on = state.Measures.Contains(m.Key);
            sb.Append($"""<li class="ant-dropdown-menu-item{(on ? " ant-dropdown-menu-item-selected" : "")}" role="none">""")
              .Append($"""<a role="menuitemcheckbox" aria-checked="{(on ? "true" : "false")}" href="{E(href(state.ToggleMeasure(m.Key)))}">""")
              .Append(on ? ShellRenderer.Icon("check", "m-pivot-check") : """<span class="m-pivot-check"></span>""")
              .Append($"<span>{E(m.Title)}</span></a></li>");
        }
        sb.Append("</ul></div></details>");

        // Flip axis, Expand all and Download side by side as one group, as in Odoo.
        sb.Append("""<div class="ant-btn-group m-pivot-actions" role="group" aria-label="Pivot actions">""")
          .Append($"""<a class="ant-btn ant-btn-default ant-btn-icon-only" href="{E(href(state.Flip()))}" title="Flip axis" aria-label="Flip axis">{ShellRenderer.Icon("swap")}</a>""")
          .Append(state.CanExpandAll()
              ? $"""<a class="ant-btn ant-btn-default ant-btn-icon-only" href="{E(href(state.ExpandAll()))}" title="Expand all" aria-label="Expand all">{ShellRenderer.Icon("expand")}</a>"""
              : $"""<button type="button" class="ant-btn ant-btn-default ant-btn-icon-only" disabled title="Every header is expanded" aria-label="Expand all">{ShellRenderer.Icon("expand")}</button>""");
        if (downloadHref is not null)
            // A file, not a page: neither htmx nor Blazor's enhanced navigation may follow it.
            sb.Append($"""<a class="ant-btn ant-btn-default ant-btn-icon-only" href="{E(downloadHref)}" download hx-boost="false" data-enhance-nav="false" title="Download xlsx" aria-label="Download xlsx">{ShellRenderer.Icon("download")}</a>""");
        sb.Append("</div></div>");
    }

    // Table ----------------------------------------------------------------------------------------------------

    private static void Table(StringBuilder sb, PivotResult result, Func<PivotState, string> href, Func<PivotCell, string?>? cellHref, string? label)
    {
        var state = result.State;
        var measures = result.Measures;
        var columns = result.ValueColumns();
        var headerRows = result.ColumnHeaderRows();

        sb.Append("""<div class="ant-table-wrapper m-pivot-table"><div class="ant-table ant-table-bordered ant-table-small"><div class="ant-table-container"><div class="ant-table-content">""")
          .Append($"""<table aria-label="{E(label ?? "Pivot table")}"><thead class="ant-table-thead">""");

        for (var r = 0; r < headerRows.Count; r++)
        {
            sb.Append("<tr>");
            if (r == 0) sb.Append($"""<th class="ant-table-cell m-pivot-corner" rowspan="{(headerRows.Count + 1).ToString(Inv)}"></th>""");
            foreach (var cell in headerRows[r])
            {
                sb.Append($"""<th class="ant-table-cell m-pivot-col{(cell.Total ? " m-pivot-total" : "")}" scope="colgroup" colspan="{cell.ColSpan.ToString(Inv)}" rowspan="{cell.RowSpan.ToString(Inv)}">""");
                if (cell.Total) sb.Append("""<span class="m-pivot-header">Total</span>""");
                else Header(sb, result, PivotAxis.Columns, cell.Header, href);
                sb.Append("</th>");
            }
            sb.Append("</tr>");
        }

        // The measure row: a sort link per value column.
        sb.Append("<tr>");
        foreach (var column in columns)
            foreach (var m in measures)
            {
                var sorted = state.SortMeasure == m.Key && state.SortColumn == column.Header.Path;
                var sort = sorted ? (state.SortDescending ? "descending" : "ascending") : null;
                sb.Append($"""<th class="ant-table-cell m-pivot-measure{(column.Total ? " m-pivot-total" : "")}" scope="col"{(sort is null ? "" : $" aria-sort=\"{sort}\"")}>""")
                  .Append($"""<a href="{E(href(state.SortBy(m.Key, column.Header.Path)))}" title="Sort by {E(m.Title)}">{E(m.Title)}""")
                  .Append(sorted ? ShellRenderer.Icon(state.SortDescending ? "caret-down" : "caret-up", "m-pivot-sort") : "")
                  .Append("</a></th>");
            }
        sb.Append("</tr></thead><tbody class=\"ant-table-tbody\">");

        foreach (var row in result.BodyRows())
        {
            var totalRow = row.Depth == 0;
            sb.Append($"""<tr class="ant-table-row{(totalRow ? " m-pivot-total-row" : "")}">""")
              .Append($"""<th class="ant-table-cell m-pivot-row" scope="row" style="--m-depth:{row.Depth.ToString(Inv)}">""");
            Header(sb, result, PivotAxis.Rows, row, href);
            sb.Append("</th>");
            foreach (var column in columns)
                foreach (var m in measures)
                {
                    var value = result.Value(row.Path, column.Header.Path, m.Key);
                    var text = E(result.Format(value, m));
                    var link = value is null || cellHref is null ? null : cellHref(new PivotCell(row, column, m.Key));
                    sb.Append($"""<td class="ant-table-cell m-pivot-value{(column.Total ? " m-pivot-total" : "")}">""")
                      .Append(link is null ? text : $"""<a href="{E(link)}">{text}</a>""")
                      .Append("</td>");
                }
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table></div></div></div></div>");
    }

    /// <summary>
    /// A row or column header: an open one collapses; a closed one opens on its axis's next group-by, or, at the last
    /// level, offers the group-bys left in a dropdown.
    /// </summary>
    private static void Header(StringBuilder sb, PivotResult result, PivotAxis axis, PivotHeader header, Func<PivotState, string> href)
    {
        var state = result.State;
        var text = $"""<span class="m-pivot-label">{E(header.Label)}</span>""";
        var title = header.GroupBy is null ? "" : $" title=\"{E(result.GroupByTitle(header.GroupBy))}\"";

        if (header.Expanded && header.Children.Count > 0)
        {
            sb.Append($"""<a class="m-pivot-header m-pivot-open" href="{E(href(state.Collapse(axis, header.Path)))}" aria-expanded="true"{title}>""")
              .Append(ShellRenderer.Icon("minus-square")).Append(text).Append("</a>");
            return;
        }
        if (header.Depth < state.GroupBys(axis).Count)
        {
            sb.Append($"""<a class="m-pivot-header" href="{E(href(state.Expand(axis, header.Path)))}" aria-expanded="false"{title}>""")
              .Append(ShellRenderer.Icon("plus-square")).Append(text).Append("</a>");
            return;
        }
        var choices = result.Choices();
        if (choices.Count == 0)
        {
            sb.Append($"""<span class="m-pivot-header"{title}>""").Append(text).Append("</span>");
            return;
        }

        sb.Append("""<details class="m-dropdown m-pivot-groupby">""")
          .Append($"""<summary class="m-pivot-header m-dropdown-trigger" aria-haspopup="menu" aria-expanded="false"{title}>""")
          .Append(ShellRenderer.Icon("plus-square")).Append(text).Append("</summary>")
          .Append("""<div class="ant-dropdown m-dropdown-panel m-dropdown-left"><ul class="ant-dropdown-menu ant-dropdown-menu-root ant-dropdown-menu-vertical ant-dropdown-menu-light" role="menu">""");
        foreach (var (field, groupBys) in choices)
        {
            if (!field.IsDate)
            {
                sb.Append($"""<li class="ant-dropdown-menu-item" role="none"><a role="menuitem" href="{E(href(state.ExpandBy(axis, header.Path, groupBys[0])))}">{E(field.Title)}</a></li>""");
                continue;
            }
            // A date opens a submenu of its intervals (Year, Quarter, Month, Week, Day).
            sb.Append("""<li class="ant-dropdown-menu-submenu" role="none"><details class="m-pivot-sub">""")
              .Append($"""<summary class="ant-dropdown-menu-submenu-title" role="menuitem" aria-haspopup="menu"><span>{E(field.Title)}</span>{ShellRenderer.Icon("right", "m-pivot-sub-arrow")}</summary>""")
              .Append("""<ul class="ant-dropdown-menu ant-dropdown-menu-sub ant-dropdown-menu-vertical" role="menu">""");
            foreach (var g in groupBys)
            {
                var interval = g[(g.IndexOf(':') + 1)..];
                var name = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(interval);
                sb.Append($"""<li class="ant-dropdown-menu-item" role="none"><a role="menuitem" href="{E(href(state.ExpandBy(axis, header.Path, g)))}">{E(name)}</a></li>""");
            }
            sb.Append("</ul></details></li>");
        }
        sb.Append("</ul></div></details>");
    }
}
