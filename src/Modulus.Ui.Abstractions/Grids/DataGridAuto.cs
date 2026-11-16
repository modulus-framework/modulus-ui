using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Modulus.Ui.Abstractions.Data;

namespace Modulus.Ui.Abstractions;

/// <summary>
/// The columns, row key and query a data grid derives from a row type's public readable properties:
/// simple properties become columns, a complex object's simple properties are flattened one level (<c>Customer.Name</c>
/// becomes a "Customer name" column that stays null-safe), and the row key is the <c>Id</c>, <c>{T}Id</c> or
/// <c>Key</c> property (the first simple property when there is none). Discovered once per type, then cached.
/// <para>
/// The same discovery feeds the schema-driven grids: <see cref="SchemaColumns{T}"/> maps the columns to
/// <see cref="GridColumn"/>s and <see cref="Records{T}"/> the rows to <see cref="GridRecord"/>s, so a nested level
/// is zero-config too:
/// <code>
/// new NestedGridLevel { Title = "Lines", Columns = DataGridAuto.SchemaColumns&lt;PurchaseLine&gt;() }
/// </code>
/// </para>
/// </summary>
public static partial class DataGridAuto
{
    /// <summary>Columns beyond this are ignored: a runaway type must not build an unusable table.</summary>
    public const int MaxColumns = 40;

    /// <summary>The columns in display order (the row key property is not among them when it is Id-like).</summary>
    public static IReadOnlyList<DataGridAutoColumn<T>> Columns<T>() => Cache<T>.Model.Columns;

    /// <summary>The key of the row key property: "Id", "{T}Id", "Key", or the first simple property's name.</summary>
    public static string IdKey<T>() => Cache<T>.Model.IdKey;

    /// <summary>The row key of a row, boxed (an int Id becomes the boxed int).</summary>
    public static Func<T, object> RowKeyOf<T>() => Cache<T>.Model.RowKey;

    /// <summary>
    /// A fresh <see cref="TableQuery{T}"/> over <typeparamref name="T"/> with every discovered column sorted and
    /// searched (the <see cref="ColumnSearch"/> grammar: contains, =exact, !exclude, and &gt;100, &lt;=5, 10..20 for
    /// numbers and dates), and <paramref name="defaultSort"/> as the default when it is one of them.
    /// </summary>
    /// <param name="ignoreCase">Compare text case-insensitively (an in-memory source; the database collation decides otherwise).</param>
    public static TableQuery<T> Query<T>(string? defaultSort = null, bool descending = false, bool ignoreCase = false)
    {
        var query = Cache<T>.Model.Start();
        foreach (var column in Cache<T>.Model.Columns) column.Register(query, ignoreCase);
        if (defaultSort is { Length: > 0 } field && query.CanSort(field)) query.DefaultSort(field, descending);
        return query;
    }

    /// <summary>The columns as <see cref="GridColumn"/>s (new instances, safe to change), for the schema-driven grids.</summary>
    public static List<GridColumn> SchemaColumns<T>() =>
        [.. Columns<T>().Select(c => new GridColumn { Key = c.Key, Title = c.Title, Type = c.CellType, Width = c.Width ?? GridSchema.DefaultWidth(c.CellType), Options = c.Options })];

    /// <summary>The rows as <see cref="GridRecord"/>s (invariant values, keyed by column), for the schema-driven grids.</summary>
    public static List<GridRecord> Records<T>(IEnumerable<T> rows)
    {
        var key = RowKeyOf<T>();
        var columns = Columns<T>();
        return [.. rows.Select(row =>
        {
            var record = new GridRecord(Convert.ToString(key(row), CultureInfo.InvariantCulture) ?? "");
            foreach (var c in columns) record[c.Key] = c.Invariant(row);
            return record;
        })];
    }
}
