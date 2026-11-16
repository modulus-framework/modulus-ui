using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;
/// <summary>
/// A button in the editable grid's batch bar that acts on the selected rows: Approve, Reject, Post, Print … The grid
/// counts the rows it applies to ("Approve 3"), skips locked rows (except <see cref="Print"/>), asks first when
/// <see cref="Confirm"/> is set, and hands the app those rows; the app does the work (Blazor: <c>OnBatch</c>; MVC: the
/// form posts <c>{name}.op=batch:{key}</c>, read with <c>EditableGridForm.Batch</c>).
/// </summary>
public sealed class GridBatchAction
{
    /// <summary>Its id in a post (<c>batch:{key}</c>); letters, digits, <c>-</c> and <c>_</c>.</summary>
    public required string Key { get; init; }

    public required string Label { get; init; }

    /// <summary>An Ant Design icon name (<c>check</c>, <c>close</c>, <c>printer</c>).</summary>
    public string? Icon { get; init; }

    /// <summary>The primary button of the bar (one, at most).</summary>
    public bool Primary { get; init; }

    /// <summary>A red button (Reject, Void).</summary>
    public bool Danger { get; init; }

    /// <summary>Ask "Approve 3 rows?" first.</summary>
    public bool Confirm { get; init; }

    /// <summary>The rows it applies to (a pending document), or null for any row. Locked rows never apply.</summary>
    [JsonIgnore]
    public Func<GridRecord, bool>? When { get; init; }

    /// <summary>Prints the selected rows (the visible columns, every selected row, locked ones too) instead of calling the app.</summary>
    public bool Print { get; init; }

    /// <summary>A Print button: the selected rows as a table, in the browser's print dialog.</summary>
    public static GridBatchAction Printing(string label = "Print") => new() { Key = "print", Label = label, Icon = "printer", Print = true };
}

