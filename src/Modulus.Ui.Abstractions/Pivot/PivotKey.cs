using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Modulus.Ui.Abstractions.Pivot;

/// <summary>
/// The group key of a pivot query: one slot per group-by level, text or number. A record, so in-memory sources group
/// by value; database providers only see the slots that are set.
/// </summary>
public sealed record PivotKey
{
    public string? S0 { get; init; }
    public string? S1 { get; init; }
    public string? S2 { get; init; }
    public string? S3 { get; init; }
    public string? S4 { get; init; }
    public string? S5 { get; init; }
    public string? S6 { get; init; }
    public string? S7 { get; init; }
    public long? N0 { get; init; }
    public long? N1 { get; init; }
    public long? N2 { get; init; }
    public long? N3 { get; init; }
    public long? N4 { get; init; }
    public long? N5 { get; init; }
    public long? N6 { get; init; }
    public long? N7 { get; init; }
}
