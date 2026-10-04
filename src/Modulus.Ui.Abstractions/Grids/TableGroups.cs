using System.Globalization;
using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Pivot;

namespace Modulus.Ui.Abstractions;

/// <summary>A column's total over a group of rows, or over every matching row (the table's footer).</summary>
/// <param name="Field">The column's key.</param>
/// <param name="Title">What the total is called ("Total salary", "Average salary").</param>
/// <param name="Aggregate">How the values combine (one fixed aggregate per total, as in the pivot).</param>
/// <param name="Value">The total, or null when no row had a value.</param>
/// <param name="Decimals">Decimal places shown.</param>
public sealed record TableTotal(string Field, string Title, PivotAggregate Aggregate, decimal? Value, int Decimals = 2)
{
    /// <summary>The value in <paramref name="culture"/> (the current culture by default); a dash when there is none.</summary>
    public string Text(CultureInfo? culture = null) =>
        Value?.ToString("N" + Math.Clamp(Decimals, 0, 6).ToString(CultureInfo.InvariantCulture), culture ?? CultureInfo.CurrentCulture) ?? "–";
}

/// <summary>
/// A group of rows on a grouped table page (<see cref="ProTableRequest.GroupBy"/>): the rows of one value, which the
/// server keeps together by sorting on the group field first. <see cref="Count"/> and <see cref="Totals"/> cover the
/// whole group, also the rows on other pages.
/// </summary>
/// <param name="Key">The value as invariant text (empty for none): what a collapsed group is remembered by.</param>
/// <param name="Label">The value as people read it ("Engineering", "On leave", "(none)").</param>
/// <param name="Count">Rows in the whole group.</param>
/// <param name="Totals">The group's totals, in the order they were registered.</param>
public sealed record TableGroup(string Key, string Label, int Count, IReadOnlyList<TableTotal> Totals)
{
    /// <summary>Rows of the group on this page; the page's groups follow each other in row order.</summary>
    public int Rows { get; init; }

    /// <summary>The group started on an earlier page.</summary>
    public bool ContinuesBefore { get; init; }

    /// <summary>The group goes on to the next page (its footer shows there).</summary>
    public bool ContinuesAfter { get; init; }
}

/// <summary>Where a group lies on the page: its first and last row (indexes into the page's rows).</summary>
public readonly record struct TableGroupSpan(TableGroup Group, int First, int Last);

/// <summary>
/// The group rows of a grouped table, shared by the Blazor <c>ProTable</c> and the MVC grids so both render the same
/// markup: a header row before each group (the toggle, the value, its row count and totals), an optional footer row
/// after it (its subtotals), and the totals of every matching row under the table.
/// </summary>
public static class TableGroups
{
    /// <summary>The query key of the group field (<c>?groupBy=department</c>).</summary>
    public const string QueryKey = "groupBy";

    /// <summary>Where each group lies on the page, from the groups' <see cref="TableGroup.Rows"/> (clamped to <paramref name="rowCount"/>).</summary>
    public static IReadOnlyList<TableGroupSpan> Spans(IReadOnlyList<TableGroup>? groups, int rowCount)
    {
        var spans = new List<TableGroupSpan>();
        var at = 0;
        foreach (var group in groups ?? [])
        {
            if (group.Rows <= 0 || at >= rowCount) continue;
            var last = Math.Min(rowCount, at + group.Rows) - 1;
            spans.Add(new TableGroupSpan(group, at, last));
            at = last + 1;
        }
        return spans;
    }

    /// <summary>"12 rows", "1 row".</summary>
    public static string RowsText(int count, CultureInfo? culture = null) =>
        $"{count.ToString("N0", culture ?? CultureInfo.CurrentCulture)} {(count == 1 ? "row" : "rows")}";

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string TotalsHtml(IReadOnlyList<TableTotal> totals)
    {
        if (totals.Count == 0) return "";
        var sb = new StringBuilder("<span class=\"m-group-totals\">");
        foreach (var t in totals)
            sb.Append("<span class=\"m-group-total\" data-field=\"").Append(E(t.Field)).Append("\"><span class=\"m-group-total-title\">")
              .Append(E(t.Title)).Append("</span> <span class=\"m-group-total-value\">").Append(E(t.Text())).Append("</span></span>");
        return sb.Append("</span>").ToString();
    }

    /// <summary>The inside of a group's header cell: the field and value, the row count, where it continues, its totals.</summary>
    /// <param name="fieldTitle">The title of the group field ("Department"), or null to show the value alone.</param>
    public static string HeaderContent(TableGroup group, string? fieldTitle)
    {
        var sb = new StringBuilder("<span class=\"m-group-label\">");
        if (!string.IsNullOrEmpty(fieldTitle)) sb.Append("<span class=\"m-group-field\">").Append(E(fieldTitle)).Append(":</span> ");
        sb.Append("<strong>").Append(E(group.Label)).Append("</strong></span>");
        sb.Append("<span class=\"m-group-count\">").Append(E(RowsText(group.Count))).Append("</span>");
        if (group.ContinuesBefore) sb.Append("<span class=\"m-group-more\">continued</span>");
        return sb.Append(TotalsHtml(group.Totals)).ToString();
    }

    /// <summary>The inside of a group's footer cell: "Subtotal: Engineering" and the totals.</summary>
    public static string FooterContent(TableGroup group) =>
        $"<span class=\"m-group-label\"><span class=\"m-group-field\">Subtotal:</span> <strong>{E(group.Label)}</strong></span>{TotalsHtml(group.Totals)}";

    /// <summary>Whether a group gets a footer row on this page: it ends here and has totals.</summary>
    public static bool HasFooter(TableGroup group) => !group.ContinuesAfter && group.Totals.Count > 0;

    /// <summary>The accessible name of a group's toggle ("Collapse Engineering").</summary>
    public static string ToggleLabel(TableGroup group, bool collapsed) => $"{(collapsed ? "Expand" : "Collapse")} {group.Label}";

    /// <summary>
    /// A group's header row for a server-rendered table. The toggle shows and hides the rows marked
    /// <c>data-m-group="{key}"</c> (modulus-shell.js); give each of the group's rows that attribute (<see cref="RowAttribute"/>).
    /// </summary>
    /// <param name="columns">The number of columns the row spans.</param>
    public static string HeaderRow(TableGroup group, string? fieldTitle, int columns) =>
        $"<tr class=\"ant-table-row m-group-row\" data-m-group-head=\"{E(group.Key)}\"><td class=\"ant-table-cell\" colspan=\"{Math.Max(1, columns).ToString(CultureInfo.InvariantCulture)}\">"
        + $"<button type=\"button\" class=\"m-group-toggle\" aria-expanded=\"true\" aria-label=\"{E(ToggleLabel(group, false))}\" data-m-group-toggle=\"{E(group.Key)}\">"
        + "<span class=\"ant-table-row-expand-icon ant-table-row-expand-icon-expanded\" aria-hidden=\"true\"></span></button>"
        + HeaderContent(group, fieldTitle) + "</td></tr>";

    /// <summary>A group's footer row for a server-rendered table (empty when <see cref="HasFooter"/> is false).</summary>
    public static string FooterRow(TableGroup group, int columns) => !HasFooter(group) ? "" :
        $"<tr class=\"m-group-footer\" data-m-group=\"{E(group.Key)}\"><td class=\"ant-table-cell\" colspan=\"{Math.Max(1, columns).ToString(CultureInfo.InvariantCulture)}\">{FooterContent(group)}</td></tr>";

    /// <summary>The attribute that ties a server-rendered row to its group's toggle: <c> data-m-group="…"</c> (with a leading space).</summary>
    public static string RowAttribute(TableGroup group) => $" data-m-group=\"{E(group.Key)}\"";

    /// <summary>The totals of every matching row, for under the table (empty when there are none).</summary>
    public static string Totals(IReadOnlyList<TableTotal>? totals, int? rows = null)
    {
        if (totals is not { Count: > 0 }) return "";
        var count = rows is { } n ? $" <span class=\"m-group-count\">{E(RowsText(n))}</span>" : "";
        return $"<div class=\"m-table-totals\" role=\"status\"><span class=\"m-group-label\"><strong>Total</strong></span>{count}{TotalsHtml(totals)}</div>";
    }
}
