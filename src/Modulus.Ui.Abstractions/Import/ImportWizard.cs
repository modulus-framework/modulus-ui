using System.Text;

namespace Modulus.Ui.Abstractions.Import;

/// <summary>The steps of the import wizard, in order. <see cref="Done"/> shows what was imported.</summary>
public enum ImportStep
{
    /// <summary>Choose a CSV file.</summary>
    Upload,

    /// <summary>Match each field to a column.</summary>
    Map,

    /// <summary>Check the rows and see what would be imported.</summary>
    Preview,

    /// <summary>The import ran; the message says what it did.</summary>
    Done,
}

/// <summary>
/// The wizard's state, for either front end: the step, the file's text and parsed table, the mapping, the checked rows,
/// and the message of the last step (an error to show, or the result of the import).
/// </summary>
public sealed record ImportWizardView(
    ImportStep Step,
    IReadOnlyList<ImportField> Fields,
    string? Csv = null,
    CsvTable? Table = null,
    ImportMapping? Mapping = null,
    ImportPreview? Preview = null,
    string? Error = null,
    string? Message = null)
{
    /// <summary>The first step, before any file.</summary>
    public static ImportWizardView Start(IReadOnlyList<ImportField> fields) => new(ImportStep.Upload, fields);
}

/// <summary>The steps' transitions that do not depend on the front end: reading a file and checking it.</summary>
public static class ImportFlow
{
    /// <summary>
    /// Reads a chosen file's text: the step after the upload (<see cref="ImportStep.Map"/>, with the columns suggested
    /// from the header names), or the upload step again with the reason the file was refused.
    /// </summary>
    public static ImportWizardView Load(string? text, IReadOnlyList<ImportField> fields)
    {
        if (string.IsNullOrEmpty(text)) return Refuse("Choose a CSV file.", fields);
        if (Encoding.UTF8.GetByteCount(text) > ImportLimits.MaxBytes) return Refuse(TooLarge, fields);
        var table = CsvReader.Parse(text);
        if (table.Headers.Count == 0) return Refuse("The file has no header row.", fields);
        if (table.Headers.Count > ImportLimits.MaxColumns)
            return Refuse($"The file has more than {ImportLimits.MaxColumns} columns.", fields);
        return new ImportWizardView(ImportStep.Map, fields, text, table, ImportMapping.Suggest(table.Headers, fields));
    }

    /// <summary>Checks the file against the mapping: the step's rows and problems.</summary>
    public static ImportWizardView Check(string csv, ImportMapping mapping, IReadOnlyList<ImportField> fields)
    {
        var table = CsvReader.Parse(csv);
        return new ImportWizardView(ImportStep.Preview, fields, csv, table, mapping, ImportValidator.Validate(table, mapping, fields));
    }

    /// <summary>The message for a file over <see cref="ImportLimits.MaxBytes"/>.</summary>
    public const string TooLarge = "The file is larger than 1 MB.";

    /// <summary>The upload step again, with the reason the file was refused.</summary>
    public static ImportWizardView Refuse(string error, IReadOnlyList<ImportField> fields) =>
        new(ImportStep.Upload, fields, Error: error);
}
