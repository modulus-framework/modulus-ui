using System.Globalization;
using System.Net;
using System.Text;

namespace Modulus.Ui.Abstractions.Printing;

/// <summary>A printed table's column: its heading and whether its cells are numbers (right-aligned).</summary>
public sealed record PrintColumn(string Title, bool Right = false);

/// <summary>
/// A table for a printed page: its header row repeats at the top of every page it runs onto (the
/// <c>m-print-table</c> class sets <c>thead { display: table-header-group }</c> in the print CSS), rows do not split
/// across pages, and an optional total row sits under the last row.
/// </summary>
public static class PrintTableHtml
{
    /// <summary>Renders the table. Every cell is HTML-encoded.</summary>
    public static string Render(IReadOnlyList<PrintColumn> columns, IReadOnlyList<IReadOnlyList<string>> rows, IReadOnlyList<string>? total = null)
    {
        var sb = new StringBuilder();
        sb.Append("""<table class="m-print-table"><thead><tr>""");
        foreach (var column in columns) sb.Append($"""<th{Align(column)}>{E(column.Title)}</th>""");
        sb.Append("</tr></thead><tbody>");
        foreach (var row in rows)
        {
            sb.Append("<tr>");
            for (var c = 0; c < columns.Count; c++)
                sb.Append($"<td{Align(columns[c])}>{E(c < row.Count ? row[c] : "")}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody>");
        if (total is not null)
        {
            sb.Append("<tfoot><tr>");
            for (var c = 0; c < columns.Count; c++)
                sb.Append($"<th{Align(columns[c])}>{E(c < total.Count ? total[c] : "")}</th>");
            sb.Append("</tr></tfoot>");
        }
        return sb.Append("</table>").ToString();
    }

    private static string Align(PrintColumn column) => column.Right ? " class=\"n\"" : "";

    private static string E(string? text) => WebUtility.HtmlEncode(text ?? "");
}
