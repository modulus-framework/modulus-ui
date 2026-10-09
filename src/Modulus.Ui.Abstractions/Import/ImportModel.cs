using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Query;

namespace Modulus.Ui.Abstractions.Import;

/// <summary>The limits of an import file. A file over a limit is refused with the reason, before any row is read.</summary>
public static class ImportLimits
{
    /// <summary>The largest file, in bytes (the form posts and the Blazor input both stop there).</summary>
    public const int MaxBytes = 1_000_000;

    /// <summary>The most data rows one import takes.</summary>
    public const int MaxRows = 2000;

    /// <summary>The most columns a file may have.</summary>
    public const int MaxColumns = 100;

    /// <summary>The longest text value a cell may hold.</summary>
    public const int MaxTextLength = 500;
}

/// <summary>A column the import maps to: its key (the value's name in the result), title, kind and whether a row needs it.</summary>
/// <param name="Options">The allowed values of a choice (<see cref="QueryFieldKind.Choice"/>); a cell matches a value or a label.</param>
/// <param name="Aliases">Other header names the column is known by, for suggesting a mapping ("E-mail" for Email).</param>
public sealed record ImportField(
    string Key,
    string Title,
    QueryFieldKind Kind,
    bool Required = false,
    IReadOnlyList<GridOption>? Options = null,
    IReadOnlyList<string>? Aliases = null);

/// <summary>Which file column feeds each field: the field's key to the column's index. A field with no column is unmapped.</summary>
public sealed record ImportMapping(IReadOnlyDictionary<string, int> Columns)
{
    /// <summary>The prefix of the mapping's field names in a form (<c>m.{key}</c>).</summary>
    public const string FieldPrefix = "m.";

    /// <summary>A mapping with no columns.</summary>
    public static ImportMapping None { get; } = new(new Dictionary<string, int>());

    /// <summary>The column index for a field, or null when it is unmapped.</summary>
    public int? Column(string key) => Columns.TryGetValue(key, out var index) ? index : null;

    /// <summary>
    /// Maps each field to the header that names it (the title, the key or an alias, ignoring case, spaces and
    /// punctuation). A title or key beats an alias, and each column feeds at most one field; a field with no matching
    /// header stays unmapped.
    /// </summary>
    public static ImportMapping Suggest(IReadOnlyList<string> headers, IReadOnlyList<ImportField> fields)
    {
        var columns = new Dictionary<string, int>();
        var taken = new HashSet<int>();
        foreach (var field in fields)
        {
            var names = new[] { field.Title, field.Key }.Concat(field.Aliases ?? []).Select(Normalize);
            var column = names
                .SelectMany(name => Enumerable.Range(0, headers.Count).Where(c => !taken.Contains(c) && Normalize(headers[c]) == name))
                .FirstOrDefault(-1);
            if (column < 0) continue;
            columns[field.Key] = column;
            taken.Add(column);
        }
        return new ImportMapping(columns);
    }

    /// <summary>
    /// Reads the mapping a form posted (<c>m.{key}</c> holds a column index, or nothing for an unmapped field). An
    /// index outside the file's columns counts as unmapped.
    /// </summary>
    public static ImportMapping Read(IReadOnlyList<ImportField> fields, Func<string, string?> form, int columnCount)
    {
        var columns = new Dictionary<string, int>();
        foreach (var field in fields)
            if (int.TryParse(form(FieldPrefix + field.Key), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var c) && c >= 0 && c < columnCount)
                columns[field.Key] = c;
        return new ImportMapping(columns);
    }

    private static string Normalize(string text) => new string(text.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
}

/// <summary>A problem with one cell: the field it is in and what is wrong.</summary>
public sealed record ImportIssue(string Field, string Message);

/// <summary>
/// One data row after checking: its number in the file (the first data row is 1), its values by field key (null when
/// empty, the text when it did not parse), and its problems.
/// </summary>
public sealed record ImportRow(int Row, IReadOnlyDictionary<string, object?> Values, IReadOnlyList<ImportIssue> Issues)
{
    /// <summary>Whether the row has no problems.</summary>
    public bool Valid => Issues.Count == 0;

    /// <summary>The value of a field as <typeparamref name="T"/> (decimal, DateOnly or string), or the default when it is empty.</summary>
    public T? Get<T>(string key) => Values.TryGetValue(key, out var value) && value is T typed ? typed : default;
}

/// <summary>The result of checking a file against a mapping: problems that stop the import, and the checked rows.</summary>
public sealed record ImportPreview(IReadOnlyList<string> Problems, IReadOnlyList<ImportRow> Rows)
{
    /// <summary>The rows with no problems.</summary>
    public int ValidCount => Rows.Count(r => r.Valid);

    /// <summary>The rows with a problem.</summary>
    public int InvalidCount => Rows.Count - ValidCount;

    /// <summary>
    /// Whether the file may be imported: the mapping and the file have no problems, there is at least one row, and every
    /// row is valid. A file with a bad row is fixed and uploaded again; nothing is imported half way.
    /// </summary>
    public bool CanCommit => Problems.Count == 0 && Rows.Count > 0 && InvalidCount == 0;
}
