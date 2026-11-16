using System.ComponentModel;
using System.Globalization;
using System.Linq.Expressions;
using Modulus.Ui.Abstractions.Query;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Modulus.Ui.Abstractions.Pivot;

namespace Modulus.Ui.Abstractions.Data;

/// <summary>One page of rows from <see cref="TableQuery{T}"/>.</summary>
/// <param name="Items">The rows of the page.</param>
/// <param name="Offset">Position of the first row among all matching rows (for row numbers and <c>aria-rowindex</c>).</param>
/// <param name="PageSize">Rows asked for.</param>
/// <param name="Total">Every matching row, or null when not counted (<see cref="CountMode.None"/>).</param>
/// <param name="HasMore">Whether rows follow this page.</param>
/// <param name="NextCursor">Opaque cursor for the next page (<see cref="ProTableRequest.Cursor"/>), or null on the last page.</param>
public sealed record TablePage<T>(IReadOnlyList<T> Items, int Offset, int PageSize, int? Total, bool HasMore, string? NextCursor)
{
    /// <summary>With <see cref="ProTableRequest.GroupBy"/> on a field that groups: the page's groups in row order; otherwise null.</summary>
    public IReadOnlyList<TableGroup>? Groups { get; init; }

    /// <summary>The registered totals over every matching row; null when none are registered.</summary>
    public IReadOnlyList<TableTotal>? Totals { get; init; }

    /// <summary>The page as a ProTable response (rows, count, groups, totals).</summary>
    public ProTableResponse<T> ToResponse() => new() { Data = Items, Total = Total ?? Offset + Items.Count + (HasMore ? 1 : 0), Groups = Groups, Totals = Totals };
}
