using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Modulus.Ui.Abstractions.Pivot;

/// <summary>One group of a level, as the database returns it: its key, record count and one value per measure.</summary>
public sealed class PivotLeaf
{
    public PivotKey Key { get; set; } = new();
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
