using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;
/// <summary>A validation error on one cell, or on a whole row when <see cref="Key"/> is empty (<see cref="ForRow"/>).</summary>
public sealed record GridError(string RowId, string Key, string Message)
{
    /// <summary>An error on a row rather than a cell: a rule over several cells, or the server refusing the row.</summary>
    public static GridError ForRow(string rowId, string message) => new(rowId, "", message);

    /// <summary>The error is on the whole row.</summary>
    public bool IsRow => Key.Length == 0;
}

