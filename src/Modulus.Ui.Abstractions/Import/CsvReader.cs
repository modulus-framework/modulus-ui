using System.Text;

namespace Modulus.Ui.Abstractions.Import;

/// <summary>A parsed file: the header names and the data rows, each cell as text (trimmed).</summary>
public sealed record CsvTable(IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows);

/// <summary>
/// Reads CSV text (RFC 4180: quoted cells with <c>""</c> escapes, line breaks inside quotes, CRLF or LF). The delimiter
/// is the one the header line uses most: comma or semicolon (the usual spreadsheet export), or tab. Blank lines are
/// skipped; a row shorter than the header is padded by the validator, a longer one has its extra cells ignored.
/// </summary>
public static class CsvReader
{
    public static CsvTable Parse(string text)
    {
        if (text.Length > 0 && text[0] == '﻿') text = text[1..];
        var delimiter = DetectDelimiter(text);
        var records = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        void EndRow()
        {
            row.Add(cell.ToString().Trim());
            cell.Clear();
            if (row.Any(c => c.Length > 0)) records.Add(row.ToArray());
            row.Clear();
        }

        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (inQuotes)
            {
                if (c != '"') cell.Append(c);
                else if (i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                else inQuotes = false;
            }
            else if (c == '"') inQuotes = true;
            else if (c == delimiter) { row.Add(cell.ToString().Trim()); cell.Clear(); }
            else if (c is '\r' or '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                EndRow();
            }
            else cell.Append(c);
        }
        if (cell.Length > 0 || row.Count > 0) EndRow();

        if (records.Count == 0) return new CsvTable([], []);
        return new CsvTable(records[0], records.Skip(1).ToArray());
    }

    // The delimiter the first line uses most, outside quotes; a comma when the line has none of them.
    private static char DetectDelimiter(string text)
    {
        var counts = new Dictionary<char, int> { [','] = 0, [';'] = 0, ['\t'] = 0 };
        var quoted = false;
        foreach (var c in text)
        {
            if (c == '"') quoted = !quoted;
            else if (c is '\r' or '\n') break;
            else if (!quoted && counts.ContainsKey(c)) counts[c]++;
        }
        var best = counts.MaxBy(pair => pair.Value);
        return best.Value == 0 ? ',' : best.Key;
    }
}
