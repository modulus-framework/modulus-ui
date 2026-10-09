using System.Globalization;
using Modulus.Ui.Abstractions.Query;

namespace Modulus.Ui.Abstractions.Import;

/// <summary>
/// Checks a file's rows against a mapping: a required field needs a column, every non-empty cell must parse as its
/// field's kind (a number, a date as <c>yyyy-MM-dd</c>, one of a choice's options, text up to
/// <see cref="ImportLimits.MaxTextLength"/>), and a file over <see cref="ImportLimits.MaxRows"/> is refused. A cell that
/// does not parse keeps its text as its value, so the preview can show what was in the file.
/// </summary>
public static class ImportValidator
{
    public static ImportPreview Validate(CsvTable table, ImportMapping mapping, IReadOnlyList<ImportField> fields)
    {
        var problems = new List<string>();
        foreach (var field in fields.Where(f => f.Required && mapping.Column(f.Key) is null))
            problems.Add($"Map a column to {field.Title}.");
        if (table.Rows.Count == 0) problems.Add("The file has no data rows.");
        if (table.Rows.Count > ImportLimits.MaxRows)
            problems.Add($"The file has {table.Rows.Count.ToString("N0", CultureInfo.InvariantCulture)} rows; the limit is {ImportLimits.MaxRows.ToString("N0", CultureInfo.InvariantCulture)}.");
        if (problems.Count > 0 || table.Rows.Count > ImportLimits.MaxRows) return new ImportPreview(problems, []);

        var rows = new List<ImportRow>(table.Rows.Count);
        for (var r = 0; r < table.Rows.Count; r++)
        {
            var cells = table.Rows[r];
            var values = new Dictionary<string, object?>();
            var issues = new List<ImportIssue>();
            foreach (var field in fields)
            {
                var text = mapping.Column(field.Key) is { } c && c < cells.Count ? cells[c] : "";
                if (text.Length == 0)
                {
                    values[field.Key] = null;
                    if (field.Required) issues.Add(new ImportIssue(field.Key, "Required"));
                    continue;
                }
                var (value, error) = Parse(field, text);
                values[field.Key] = error is null ? value : text;
                if (error is not null) issues.Add(new ImportIssue(field.Key, error));
            }
            rows.Add(new ImportRow(r + 1, values, issues));
        }
        return new ImportPreview([], rows);
    }

    /// <summary>Reads one non-empty cell as its field's kind: the value, or the reason it is not one.</summary>
    public static (object? Value, string? Error) Parse(ImportField field, string text) => field.Kind switch
    {
        QueryFieldKind.Number => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number)
            ? (number, null)
            : (null, "Not a number"),
        QueryFieldKind.Date => DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? (date, null)
            : (null, "Use a date as yyyy-MM-dd"),
        QueryFieldKind.Choice => ChoiceValue(field, text),
        _ => text.Length > ImportLimits.MaxTextLength
            ? (null, $"Longer than {ImportLimits.MaxTextLength} characters")
            : (text, null),
    };

    private static (object? Value, string? Error) ChoiceValue(ImportField field, string text)
    {
        var options = field.Options ?? [];
        var match = options.FirstOrDefault(o => string.Equals(o.Value, text, StringComparison.OrdinalIgnoreCase)
            || string.Equals(o.Label, text, StringComparison.OrdinalIgnoreCase));
        return match is not null
            ? (match.Value, null)
            : (null, "Not one of " + string.Join(", ", options.Select(o => o.Label)));
    }
}
