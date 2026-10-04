using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions.Plugins;

/// <summary>
/// A sheet of a spreadsheet: a title and rows of cells (numbers, text, bools, or "=SUM(A1:B2)"). The first
/// <paramref name="FrozenRows"/> rows stay in view and show as bold header rows, the first <paramref name="FrozenColumns"/>
/// columns stay in view, and <paramref name="ColumnWidths"/> sets widths in pixels from column A (0 keeps the default).
/// All three come back from the grid as the user left them and travel through xlsx.
/// </summary>
public sealed record SpreadsheetSheet(
    string Name,
    IReadOnlyList<IReadOnlyList<object?>> Rows,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int FrozenRows = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] int FrozenColumns = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<int>? ColumnWidths = null);

/// <summary>
/// Markup of the spreadsheet (<c>AppSpreadsheet</c> in Blazor, <c>&lt;app-spreadsheet&gt;</c> in MVC).
/// modulus-plugins.js starts Univer on <c>[data-m-sheet]</c> when it appears; edits land in the hidden
/// <c>[data-m-sheet-value]</c> input as the same JSON, so the sheet posts with its form.
/// </summary>
public static class SpreadsheetHtml
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>A spreadsheet. Cells may be numbers, text, bools or formula strings starting with "=".</summary>
    /// <param name="id">Element id of the sheet.</param>
    /// <param name="sheets">The sheets to show; the first one is active.</param>
    /// <param name="editable">True (default) shows the toolbar, formula bar and context menu; false shows a bare grid.</param>
    /// <param name="height">CSS height, e.g. "420px" or "100%".</param>
    /// <param name="name">Form field name; the hidden input posts the edited sheets as JSON under it.</param>
    public static string Render(string id, IReadOnlyList<SpreadsheetSheet> sheets, bool editable = true,
        string height = "420px", string? name = null, string? label = null, string? cssClass = null)
    {
        var data = JsonSerializer.Serialize(sheets, Json);
        var sb = new StringBuilder($"""<div class="m-sheet{(cssClass is { Length: > 0 } c ? " " + E(c) : "")}" id="{E(id)}" data-m-sheet data-sheets="{E(data)}" """);
        if (!editable) sb.Append("data-readonly ");
        sb.Append($"""role="region" aria-label="{E(label ?? "Spreadsheet")}" style="height: {E(height)}">""");
        sb.Append("""<div class="m-sheet-loading">Loading spreadsheet…</div>""");
        if (name is { Length: > 0 })
            sb.Append($"""<input type="hidden" name="{E(name)}" id="{E(id)}-value" value="{E(data)}" data-m-sheet-value>""");
        sb.Append("</div>");
        return sb.ToString();
    }
}
