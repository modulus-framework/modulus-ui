using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using Modulus.Ui.Abstractions.Query;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Modulus.Ui.Abstractions.Pivot;

namespace Modulus.Ui.Abstractions.Data;

/// <summary>One group's row count and totals, as <see cref="TableQuery{T}"/> reads them in a <c>GROUP BY</c> (not for direct use).</summary>
[EditorBrowsable(EditorBrowsableState.Never)]
public sealed class TableGroupLeaf<TKey>
{
    public TKey Key { get; set; } = default!;
    public int Count { get; set; }
    public decimal V0 { get; set; }
    public decimal V1 { get; set; }
    public decimal V2 { get; set; }
    public decimal V3 { get; set; }
    public decimal V4 { get; set; }
    public decimal V5 { get; set; }
    public decimal V6 { get; set; }
    public decimal V7 { get; set; }
}
