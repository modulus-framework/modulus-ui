using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;
/// <summary>How far a grid row can be changed (<see cref="GridRecord.State"/>).</summary>
public enum GridRowState
{
    /// <summary>Its editable cells change; it can be moved and deleted.</summary>
    Editable,

    /// <summary>Its cells don't change (an approved or computed line); it can still be moved and deleted.</summary>
    ReadOnly,

    /// <summary>Nothing changes: no edits, no delete, no move (a posted document, a row someone else holds).</summary>
    Locked,
}

