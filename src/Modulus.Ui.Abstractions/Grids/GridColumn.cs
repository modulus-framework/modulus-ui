using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;
/// <summary>
/// A column of an editable, schema-driven grid (Blazor <c>EditableGrid</c>, MVC <c>app-editable-grid</c>).
/// Columns are data, so an app can build them at runtime, store them, or let users add their own.
/// </summary>
public sealed class GridColumn
{
    public string Key { get; set; } = "";

    public string Title { get; set; } = "";

    public GridCellType Type { get; set; }

    /// <summary>Width in pixels; pinned columns need one so the next pinned column knows its offset.</summary>
    public int? Width { get; set; }

    public GridFixed Fixed { get; set; }

    public bool Hidden { get; set; }

    public bool Editable { get; set; } = true;

    public bool Required { get; set; }

    /// <summary>Lower bound for Number, Currency, Percent, Progress and Rating.</summary>
    public decimal? Min { get; set; }

    /// <summary>Upper bound for Number, Currency, Percent, Progress and Rating.</summary>
    public decimal? Max { get; set; }

    public IReadOnlyList<GridOption> Options { get; set; } = [];

    /// <summary>Show an initials avatar before the cell text (a person or company column).</summary>
    public bool Avatar { get; set; }

    /// <summary>Key of a row value shown as a muted second line under the cell text (an email under a name).</summary>
    public string? SubtitleKey { get; set; }

    /// <summary>
    /// Give the column a dropdown in the grid's filter row. Select, Tag, MultiSelect and Switch columns can be filtered.
    /// </summary>
    public bool Filterable { get; set; }

    /// <summary>Numbers and money align right.</summary>
    [JsonIgnore]
    public bool AlignRight => Type is GridCellType.Number or GridCellType.Currency or GridCellType.Percent;

    public GridColumn Clone() => (GridColumn)MemberwiseClone();
}

