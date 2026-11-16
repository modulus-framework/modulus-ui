using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Modulus.Ui.Abstractions.Data;

namespace Modulus.Ui.Abstractions;

/// <summary>
/// One column <see cref="DataGridAuto"/> discovers on a CLR type: its key, title and kind, how it formats a cell,
/// and how it registers with a <see cref="TableQuery{T}"/> so the database sorts and searches it.
/// </summary>
public abstract class DataGridAutoColumn<T>
{
    private protected DataGridAutoColumn(string key, string title, ColumnKind kind, GridCellType cellType, int? width, IReadOnlyList<GridOption> options)
    {
        Key = key;
        Title = title;
        Kind = kind;
        CellType = cellType;
        Width = width;
        Options = options;
    }

    /// <summary>The column's key: what the header sorts and searches by (a camelCase path, "customerName").</summary>
    public string Key { get; }

    public string Title { get; }

    public ColumnKind Kind { get; }

    /// <summary>The column as a <see cref="GridCellType"/> of the schema-driven grids.</summary>
    public GridCellType CellType { get; }

    /// <summary>A good width for the column, in pixels, or null to let the table size it.</summary>
    public int? Width { get; }

    /// <summary>Choices of a <see cref="GridCellType.Select"/> column (an enum); empty for the rest.</summary>
    public IReadOnlyList<GridOption> Options { get; }

    public bool AlignRight => Kind == ColumnKind.Number;

    /// <summary>The cell as text in <paramref name="culture"/> (the current culture by default).</summary>
    public abstract string? Text(T row, CultureInfo? culture = null);

    /// <summary>The cell as an invariant string, as a <see cref="GridRecord"/> value stores it.</summary>
    public abstract string? Invariant(T row);

    /// <summary>The cell's raw value, boxed (null when the property, or its parent object, is null).</summary>
    public abstract object? Value(T row);

    /// <summary>The column as <c>x =&gt; (object?)x.Prop</c>, for a boxed property column.</summary>
    public abstract Expression<Func<T, object?>> Boxed { get; }

    /// <summary>Registers the column's sort and its column search with <paramref name="query"/>.</summary>
    /// <param name="ignoreCase">Compare text case-insensitively (an in-memory source; the database collation decides otherwise).</param>
    internal abstract void Register(TableQuery<T> query, bool ignoreCase);

    /// <summary>The cell of an enum value: its option's label, or the name.</summary>
    private protected string EnumText(string name) => Options.FirstOrDefault(o => o.Value == name)?.Label ?? name;
}
