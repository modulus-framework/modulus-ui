using AntDesign;

namespace Modulus.Blazor.Components.Pro;

/// <summary>
/// Ant Design's table, telling how many columns it has (<see cref="ProTable{TItem}"/>'s group header and footer rows
/// span them all) and which it shows (the bands of a multi-level header sit over them).
/// </summary>
public class GroupingTable<TItem> : Table<TItem>
{
    /// <summary>The columns the table has registered (selection, drag handle and data columns), at least one.</summary>
    public int ColumnCount => Math.Max(1, ColumnContext?.Columns.Count ?? 1);

    /// <summary>
    /// The shown columns in order: a field column's name (a <c>PropertyColumn</c>'s property, a <c>DataIndex</c>), null for
    /// the expand, selection and other columns.
    /// </summary>
    public IReadOnlyList<string?> ShownColumns()
    {
        var keys = new List<string?>();
        if (ColumnContext is null) return keys;
        if (ExpandTemplate is not null) keys.Add(null);
        // The column definitions know what is hidden; their header cells know the field names (set as the header renders).
        var names = ColumnContext.HeaderColumns.OfType<IFieldColumn>().GroupBy(c => c.ColIndex).ToDictionary(g => g.Key, g => g.Last().FieldName);
        foreach (var column in ColumnContext.Columns)
            if (!column.Hidden) keys.Add(names.GetValueOrDefault(column.ColIndex));
        return keys;
    }
}
