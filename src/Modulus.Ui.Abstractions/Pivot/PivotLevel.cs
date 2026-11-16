using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;

namespace Modulus.Ui.Abstractions.Pivot;

/// <summary>
/// One grouped query of a pivot: the groups of the first <paramref name="RowDepth"/> row group-bys and the first
/// <paramref name="ColumnDepth"/> column group-bys, inside the headers that are open at those depths.
/// </summary>
public sealed record PivotLevel(int RowDepth, int ColumnDepth, IQueryable<PivotLeaf> Groups);
