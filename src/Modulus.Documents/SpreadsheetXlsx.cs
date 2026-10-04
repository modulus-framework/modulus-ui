using System.Text.Json;
using ClosedXML.Excel;
using Modulus.Ui.Abstractions.Plugins;

namespace Modulus.Documents;

/// <summary>
/// xlsx in and out of the spreadsheet plugin: <see cref="SpreadsheetSheet"/> lists (the JSON the sheet posts) become a workbook,
/// and an uploaded workbook becomes sheets again. Cells are numbers, text, bools or "=SUM(B2:E2)" formulas; dates and anything
/// else read back as text. Posted sheets carry their cells as <see cref="JsonElement"/>s, which write the same as the CLR values.
/// Frozen rows and columns become freeze panes (frozen rows also bold, as the grid shows them) and column widths travel in pixels.
/// </summary>
public static class SpreadsheetXlsx
{
    /// <summary>The MIME type of an xlsx workbook, for download endpoints.</summary>
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    /// <summary>The sheets as an xlsx workbook; sheet names are shortened to Excel's 31 characters and its invalid ones replaced.</summary>
    public static byte[] Write(IReadOnlyList<SpreadsheetSheet> sheets)
    {
        using var workbook = new XLWorkbook();
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sheet in sheets)
            WriteSheet(workbook.Worksheets.Add(SheetName(sheet.Name, used, workbook.Worksheets.Count + 1)), sheet);
        using var bytes = new MemoryStream();
        workbook.SaveAs(bytes);
        return bytes.ToArray();
    }

    /// <summary>An xlsx workbook (an upload) as sheets; empty cells read as null, formulas as "=SUM(B2:B2)" strings.</summary>
    public static IReadOnlyList<SpreadsheetSheet> Read(Stream xlsx)
    {
        using var workbook = new XLWorkbook(xlsx);
        var sheets = new List<SpreadsheetSheet>();
        foreach (var sheet in workbook.Worksheets)
        {
            var last = sheet.LastRowUsed();
            var rows = new List<IReadOnlyList<object?>>();
            for (var r = 1; last is not null && r <= last.RowNumber(); r++)
            {
                var end = sheet.Row(r).LastCellUsed();
                var values = new object?[end is not null ? end.Address.ColumnNumber : 0];
                for (var c = 1; c <= values.Length; c++) values[c - 1] = Cell(sheet.Row(r).Cell(c));
                rows.Add(values);
            }
            var widths = Widths(sheet, rows.Count == 0 ? 0 : rows.Max(r => r.Count));
            sheets.Add(new SpreadsheetSheet(sheet.Name, rows, sheet.SheetView.SplitRow, sheet.SheetView.SplitColumn, widths));
        }
        return sheets;
    }

    private static void WriteSheet(IXLWorksheet sheet, SpreadsheetSheet source)
    {
        var rows = source.Rows ?? [];
        for (var r = 0; r < rows.Count; r++)
            for (var c = 0; c < (rows[r]?.Count ?? 0); c++)
                Cell(sheet.Cell(r + 1, c + 1), rows[r][c]);
        var frozenRows = Math.Max(0, source.FrozenRows);
        var frozenColumns = Math.Max(0, source.FrozenColumns);
        if (frozenRows > 0 || frozenColumns > 0) sheet.SheetView.Freeze(frozenRows, frozenColumns);
        for (var r = 1; r <= frozenRows; r++) sheet.Row(r).Style.Font.Bold = true; // header rows, as in the grid
        var widths = source.ColumnWidths ?? [];
        for (var c = 0; c < widths.Count; c++)
            if (widths[c] > 0) sheet.Column(c + 1).Width = Math.Round((widths[c] - 5) / PixelsPerChar, 2);
    }

    // Excel measures columns in characters of the default font (about 7 px each, plus 5 px of padding).
    private const double PixelsPerChar = 7;

    private static IReadOnlyList<int>? Widths(IXLWorksheet sheet, int columns)
    {
        var widths = new int[columns];
        var any = false;
        for (var c = 1; c <= columns; c++)
        {
            var column = sheet.Column(c);
            if (Math.Abs(column.Width - sheet.ColumnWidth) < 0.01) continue;
            widths[c - 1] = (int)Math.Round(column.Width * PixelsPerChar + 5);
            any = true;
        }
        return any ? widths : null;
    }

    private static void Cell(IXLCell cell, object? value)
    {
        switch (value)
        {
            case null: return;
            case JsonElement element: Cell(cell, element); break;
            case bool b: cell.SetValue(b); break;
            case string text: Text(cell, text); break;
            default: cell.SetValue(Convert.ToDouble(value)); break; // int, long, double, decimal…
        }
    }

    private static void Cell(IXLCell cell, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Number: cell.SetValue(value.GetDouble()); break;
            case JsonValueKind.True: cell.SetValue(true); break;
            case JsonValueKind.False: cell.SetValue(false); break;
            case JsonValueKind.String: Text(cell, value.GetString() ?? ""); break;
        }
    }

    private static void Text(IXLCell cell, string text)
    {
        // "=SUM(B2:E2)" becomes a live formula; other text stays text.
        if (text.StartsWith("=", StringComparison.Ordinal)) cell.FormulaA1 = text[1..];
        else cell.SetValue(text);
    }

    private static object? Cell(IXLCell cell)
    {
        if (cell.IsEmpty()) return null;
        if (cell.HasFormula) return "=" + cell.FormulaA1;
        return cell.DataType switch
        {
            XLDataType.Number => Number(cell.GetDouble()),
            XLDataType.Boolean => cell.GetBoolean(),
            _ => cell.GetString(), // text, dates and timespans as text
        };
    }

    // Whole numbers read back as long (as JSON posts them); a plain ?: would widen the long back to double.
    private static object Number(double value) =>
        value == Math.Truncate(value) && Math.Abs(value) <= long.MaxValue ? (object)(long)value : value;

    private static string SheetName(string? name, HashSet<string> used, int index)
    {
        // Excel allows 31 characters and none of : \ / ? * [ ] or a leading/trailing apostrophe.
        var chars = (name ?? "").Select(c => c is ':' or '\\' or '/' or '?' or '*' or '[' or ']' ? ' ' : c).ToArray();
        var text = new string(chars).Trim().Trim('\'');
        if (text.Length > 31) text = text[..31].TrimEnd('\'');
        if (text.Length == 0) text = "Sheet" + index;
        var unique = text;
        for (var n = 2; !used.Add(unique); n++)
            unique = (text.Length > 27 ? text[..27] : text) + " (" + n + ")";
        return unique;
    }
}
