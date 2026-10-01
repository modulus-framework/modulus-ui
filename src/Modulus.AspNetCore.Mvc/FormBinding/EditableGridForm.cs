using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc;

/// <summary>The state of an <c>app-editable-grid</c> posted back by the browser, and the button that posted it.</summary>
public sealed class EditableGridPost
{
    public required List<GridColumn> Columns { get; init; }

    public required List<GridRecord> Rows { get; init; }

    /// <summary>The value of the clicked <c>{name}.op</c> button: <c>save</c>, <c>pin:budget:left</c>, <c>add-column</c> …</summary>
    public string? Operation { get; init; }

    /// <summary>The verb of <see cref="Operation"/> (<c>save</c>, <c>pin</c>, <c>add-column</c> …).</summary>
    public string Verb => Operation?.Split(':')[0] ?? "";

    /// <summary>Why <see cref="EditableGridForm.Apply"/> couldn't do the operation (for example a duplicate option), or null.</summary>
    public string? Error { get; set; }
}

/// <summary>
/// Reads and applies the form an <c>app-editable-grid</c> posts. The grid keeps its schema in a hidden
/// <c>{name}.schema</c> field and its rows in <c>{name}.index</c> + <c>{name}[{id}].{key}</c>, so every post
/// carries the whole grid, edits included, and the server can re-render it after any operation.
/// </summary>
public static partial class EditableGridForm
{
    public const int MaxRows = 500;

    [GeneratedRegex("^[A-Za-z0-9_-]{1,40}$")]
    private static partial Regex RowIdPattern();

    /// <summary>
    /// Reads the posted grid. <paramref name="fallbackColumns"/> is used when the schema field is missing or
    /// invalid (a tampered post); rows keep only known columns and normalised values.
    /// </summary>
    public static EditableGridPost Read(IFormCollection form, IReadOnlyList<GridColumn> fallbackColumns, string name = "grid")
    {
        var columns = GridSchema.Deserialize(form[$"{name}.schema"]) ?? fallbackColumns.Select(c => c.Clone()).ToList();
        var rows = new List<GridRecord>();
        foreach (var id in form[$"{name}.index"].Distinct().Take(MaxRows))
        {
            if (id is null || !RowIdPattern().IsMatch(id)) continue;
            var row = new GridRecord(id);
            foreach (var c in columns)
            {
                var values = form[$"{name}[{id}].{c.Key}"];
                var raw = c.Type switch
                {
                    // A switch posts a hidden "false" and, when on, "true".
                    GridCellType.Switch => values.Any(v => GridSchema.Flag(v)) ? "true" : "false",
                    GridCellType.MultiSelect => string.Join(',', values.Where(v => !string.IsNullOrEmpty(v))),
                    _ => values.LastOrDefault(),
                };
                row[c.Key] = GridSchema.Normalize(c, raw);
            }
            rows.Add(row);
        }
        return new EditableGridPost { Columns = columns, Rows = rows, Operation = form[$"{name}.op"].FirstOrDefault() };
    }

    /// <summary>
    /// Applies a structural operation (rows and columns) to <paramref name="post"/>. Returns false for the
    /// operations the app handles itself (<c>save</c>, <c>discard</c>, <c>reset</c> or anything unknown).
    /// </summary>
    public static bool Apply(EditableGridPost post, IFormCollection form, string name = "grid")
    {
        var parts = (post.Operation ?? "").Split(':');
        var key = parts.ElementAtOrDefault(1) ?? "";
        switch (parts[0])
        {
            case "add-row":
                post.Rows.Insert(0, GridSchema.NewRow(post.Columns, post.Rows));
                return true;
            case "delete-row":
                post.Rows.RemoveAll(r => r.Id == key);
                return true;
            case "pin":
                GridSchema.Pin(post.Columns, key, parts.ElementAtOrDefault(2) switch { "left" => GridFixed.Left, "right" => GridFixed.Right, _ => GridFixed.None });
                return true;
            case "hide":
                GridSchema.SetHidden(post.Columns, key, true);
                return true;
            case "show":
                GridSchema.SetHidden(post.Columns, key, false);
                return true;
            case "toggle":
                if (GridSchema.Find(post.Columns, key) is { } column) GridSchema.SetHidden(post.Columns, key, !column.Hidden);
                return true;
            case "move":
                GridSchema.Move(post.Columns, key, parts.ElementAtOrDefault(2) == "-1" ? -1 : 1);
                return true;
            case "remove":
                GridSchema.Remove(post.Columns, post.Rows, key);
                return true;
            case "add-column":
                var type = Enum.TryParse<GridCellType>(form[$"{name}.new.type"], ignoreCase: true, out var t) ? t : GridCellType.Text;
                var pin = Enum.TryParse<GridFixed>(form[$"{name}.new.fixed"], ignoreCase: true, out var f) ? f : GridFixed.None;
                var added = GridSchema.Add(post.Columns, form[$"{name}.new.title"], type, form[$"{name}.new.options"], pin, GridSchema.Flag(form[$"{name}.new.required"]));
                if (added is not null)
                    foreach (var row in post.Rows) row[added.Key] = added.Type == GridCellType.Switch ? "false" : null;
                return true;
            case "add-option":
                AddOption(post, form, name, key, parts.ElementAtOrDefault(2) ?? "");
                return true;
            default:
                return false;
        }
    }

    /// <summary>Form field of the option name typed in a cell's editor (<c>{name}.option[{row}].{key}</c>).</summary>
    public static string OptionField(string name, string rowId, string key) => $"{name}.option[{rowId}].{key}";

    /// <summary>Form field of the colour picked for a new option (<c>{name}.color[{row}].{key}</c>).</summary>
    public static string ColorField(string name, string rowId, string key) => $"{name}.color[{rowId}].{key}";

    // add-option:{key}:{row} adds the option typed in that cell's editor to the column and picks it for the cell.
    private static void AddOption(EditableGridPost post, IFormCollection form, string name, string key, string rowId)
    {
        if (GridSchema.Find(post.Columns, key) is not { Type: GridCellType.Select or GridCellType.MultiSelect or GridCellType.Tag } column) return;
        var (options, added, error) = GridSchema.AddOption(column.Options, form[OptionField(name, rowId, key)], form[ColorField(name, rowId, key)]);
        if (added is null) { post.Error = error; return; }
        column.Options = options;
        if (post.Rows.Find(r => r.Id == rowId) is not { } row) return;
        row[key] = column.Type == GridCellType.MultiSelect
            ? GridSchema.FromMany([.. GridSchema.Many(row[key]), added.Value])
            : added.Value;
    }
}
