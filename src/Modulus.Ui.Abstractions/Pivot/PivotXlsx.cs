using System.Globalization;
using System.IO.Compression;
using System.Text;

namespace Modulus.Ui.Abstractions.Pivot;

/// <summary>
/// The pivot table as an Excel workbook (Odoo's Download button): the same headers, merged as the table spans them,
/// row labels indented by level, totals in bold. Plain SpreadsheetML in a zip, so it needs no library.
/// </summary>
public static class PivotXlsx
{
    public const string ContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    // Cell styles (cellXfs): 0 plain, 1 bold, 2 #,##0, 3 #,##0.00, 4 bold #,##0, 5 bold #,##0.00, 6 bold centred.
    private const int Plain = 0, Bold = 1, Whole = 2, Decimal = 3, BoldWhole = 4, BoldDecimal = 5, Heading = 6;

    /// <summary>The workbook's bytes.</summary>
    public static byte[] Write(PivotResult result, string sheetName = "Pivot")
    {
        using var stream = new MemoryStream();
        Write(result, stream, sheetName);
        return stream.ToArray();
    }

    /// <summary>Writes the workbook to a stream (left open).</summary>
    public static void Write(PivotResult result, Stream stream, string sheetName = "Pivot")
    {
        using var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true);
        Part(zip, "[Content_Types].xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types"><Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/><Default Extension="xml" ContentType="application/xml"/><Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/><Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/><Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/></Types>""");
        Part(zip, "_rels/.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/></Relationships>""");
        Part(zip, "xl/workbook.xml", $"""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships"><sheets><sheet name="{X(SheetName(sheetName))}" sheetId="1" r:id="rId1"/></sheets></workbook>""");
        Part(zip, "xl/_rels/workbook.xml.rels", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships"><Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/><Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/></Relationships>""");
        Part(zip, "xl/styles.xml", """<?xml version="1.0" encoding="UTF-8" standalone="yes"?><styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><sz val="11"/><name val="Calibri"/></font></fonts><fills count="2"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill></fills><borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders><cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs><cellXfs count="7"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1"/><xf numFmtId="3" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="4" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="3" fontId="1" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyFont="1"/><xf numFmtId="4" fontId="1" fillId="0" borderId="0" xfId="0" applyNumberFormat="1" applyFont="1"/><xf numFmtId="0" fontId="1" fillId="0" borderId="0" xfId="0" applyFont="1" applyAlignment="1"><alignment horizontal="center"/></xf></cellXfs><cellStyles count="1"><cellStyle name="Normal" xfId="0" builtinId="0"/></cellStyles></styleSheet>""");
        Part(zip, "xl/worksheets/sheet1.xml", Sheet(result));
    }

    private static string Sheet(PivotResult result)
    {
        var cells = new SortedDictionary<int, SortedDictionary<int, string>>();
        var merges = new List<string>();
        void Put(int row, int col, string xml)
        {
            if (!cells.TryGetValue(row, out var line)) cells[row] = line = new SortedDictionary<int, string>();
            line[col] = xml;
        }
        void Text(int row, int col, string text, int style) =>
            Put(row, col, $"""<c r="{Ref(row, col)}" t="inlineStr" s="{style.ToString(Inv)}"><is><t xml:space="preserve">{X(text)}</t></is></c>""");
        void Number(int row, int col, decimal value, int style) =>
            Put(row, col, $"""<c r="{Ref(row, col)}" s="{style.ToString(Inv)}"><v>{value.ToString(Inv)}</v></c>""");
        void Merge(int row, int col, int rows, int cols)
        {
            if (rows > 1 || cols > 1) merges.Add($"""<mergeCell ref="{Ref(row, col)}:{Ref(row + rows - 1, col + cols - 1)}"/>""");
        }

        var widths = "";
        if (result.Error is { } error) Text(0, 0, error, Plain);
        else if (result.IsEmpty) Text(0, 0, "No data to display", Plain);
        else
        {
            var measures = result.Measures;
            var columns = result.ValueColumns();
            var headerRows = result.ColumnHeaderRows();

            // Column headers: each cell takes the first free columns of its row, as a table lays out rowspans.
            var taken = new HashSet<(int, int)>();
            Merge(0, 0, headerRows.Count + 1, 1);
            for (var r = 0; r < headerRows.Count; r++)
            {
                var col = 1;
                foreach (var cell in headerRows[r])
                {
                    while (taken.Contains((r, col))) col++;
                    Text(r, col, cell.Total ? "Total" : cell.Header.Label, Heading);
                    Merge(r, col, cell.RowSpan, cell.ColSpan);
                    for (var i = 0; i < cell.RowSpan; i++)
                    for (var j = 0; j < cell.ColSpan; j++)
                        taken.Add((r + i, col + j));
                    col += cell.ColSpan;
                }
            }
            var measureRow = headerRows.Count;
            var c = 1;
            foreach (var _ in columns)
            foreach (var m in measures)
                Text(measureRow, c++, m.Title, Heading);

            var row = measureRow + 1;
            foreach (var header in result.BodyRows())
            {
                var total = header.Depth == 0;
                Text(row, 0, new string(' ', header.Depth * 4) + header.Label, total ? Bold : Plain);
                c = 1;
                foreach (var column in columns)
                foreach (var m in measures)
                {
                    var bold = total || column.Total;
                    if (result.Value(header.Path, column.Header.Path, m.Key) is { } v)
                        Number(row, c, v, m.Decimals == 0 ? (bold ? BoldWhole : Whole) : (bold ? BoldDecimal : Decimal));
                    c++;
                }
                row++;
            }
            widths = $"""<cols><col min="1" max="1" width="32" customWidth="1"/><col min="2" max="{Math.Max(2, c).ToString(Inv)}" width="14" customWidth="1"/></cols>""";
        }

        var sb = new StringBuilder("""<?xml version="1.0" encoding="UTF-8" standalone="yes"?><worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");
        sb.Append(widths).Append("<sheetData>");
        foreach (var (r, line) in cells)
        {
            sb.Append($"""<row r="{(r + 1).ToString(Inv)}">""");
            foreach (var xml in line.Values) sb.Append(xml);
            sb.Append("</row>");
        }
        sb.Append("</sheetData>");
        if (merges.Count > 0) sb.Append($"""<mergeCells count="{merges.Count.ToString(Inv)}">""").AppendJoin("", merges).Append("</mergeCells>");
        return sb.Append("</worksheet>").ToString();
    }

    /// <summary>An A1 reference from zero-based row and column.</summary>
    internal static string Ref(int row, int col)
    {
        var letters = "";
        for (var n = col + 1; n > 0; n = (n - 1) / 26) letters = (char)('A' + (n - 1) % 26) + letters;
        return letters + (row + 1).ToString(Inv);
    }

    /// <summary>A valid sheet name: at most 31 characters, none of <c>[]:*?/\</c>.</summary>
    private static string SheetName(string name)
    {
        var clean = new string(name.Where(ch => "[]:*?/\\".IndexOf(ch) < 0).ToArray()).Trim();
        return clean.Length == 0 ? "Pivot" : clean[..Math.Min(31, clean.Length)];
    }

    /// <summary>XML text: escaped, without the control characters XML 1.0 forbids.</summary>
    private static string X(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            switch (ch)
            {
                case '&': sb.Append("&amp;"); break;
                case '<': sb.Append("&lt;"); break;
                case '>': sb.Append("&gt;"); break;
                case '"': sb.Append("&quot;"); break;
                case < ' ' when ch is not ('\t' or '\n' or '\r'): break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }

    private static void Part(ZipArchive zip, string name, string xml)
    {
        using var writer = new StreamWriter(zip.CreateEntry(name, CompressionLevel.Fastest).Open(), new UTF8Encoding(false));
        writer.Write(xml);
    }
}
