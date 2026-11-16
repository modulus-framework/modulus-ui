using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;
/// <summary>A batch button was clicked (and confirmed): the action and the selected rows it acts on (<see cref="GridSchema.BatchRows"/>).</summary>
public sealed record GridBatchRequest(GridBatchAction Action, IReadOnlyList<GridRecord> Rows);

