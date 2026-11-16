using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;
/// <summary>
/// A grid row. Values are stored as invariant strings keyed by column: numbers as <c>1234.5</c>, dates as
/// <c>yyyy-MM-dd</c>, switches as <c>true</c>/<c>false</c>, multi-selects as comma-separated values.
/// That is exactly what a form posts, so MVC and Blazor share one model and one validator.
/// </summary>
public sealed class GridRecord
{
    public GridRecord() { }

    public GridRecord(string id, IDictionary<string, string?>? values = null)
    {
        Id = id;
        if (values is not null) foreach (var (k, v) in values) Values[k] = v;
    }

    public string Id { get; set; } = "";

    public Dictionary<string, string?> Values { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Read-only or locked rows can't be edited in the grid. Set by the server and never read from a post: MVC apps
    /// call <see cref="GridSchema.Protect"/> to keep a posted grid to the stored states.
    /// </summary>
    public GridRowState State { get; set; }

    /// <summary>Why the row is read-only or locked ("Invoiced on 3 Oct"): its tooltip, and read out with the lock.</summary>
    public string? StateReason { get; set; }

    /// <summary>The row's cells can change.</summary>
    [JsonIgnore]
    public bool Editable => State == GridRowState.Editable;

    public string? this[string key]
    {
        get => Values.GetValueOrDefault(key);
        set => Values[key] = value;
    }
}

