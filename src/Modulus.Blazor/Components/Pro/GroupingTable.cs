using AntDesign;

namespace Modulus.Blazor.Components.Pro;

/// <summary>
/// Ant Design's table, telling how many columns it has: <see cref="ProTable{TItem}"/>'s group header and footer rows
/// span them all.
/// </summary>
public class GroupingTable<TItem> : Table<TItem>
{
    /// <summary>The columns the table has registered (selection, drag handle and data columns), at least one.</summary>
    public int ColumnCount => Math.Max(1, ColumnContext?.Columns.Count ?? 1);
}
