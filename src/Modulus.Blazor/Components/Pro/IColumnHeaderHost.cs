using Modulus.Ui.Abstractions;

namespace Modulus.Blazor.Components.Pro;

/// <summary>
/// The table a <see cref="ColumnHeader"/> sorts and searches (ProTable and EditableGrid cascade themselves as one):
/// a header click cycles the sort ascending, descending, unsorted; the search box sets that column's search.
/// </summary>
public interface IColumnHeaderHost
{
    Modulus.Ui.Abstractions.SortDirection SortOf(string key);

    Task SortAsync(string key);

    /// <summary>
    /// A header click with Shift (or Ctrl/Cmd) held when <paramref name="add"/>: the column joins the other sorts
    /// (<see cref="SortKeys.Click"/>). A table without <see cref="MultiSort"/> sorts by the one column.
    /// </summary>
    Task SortAsync(string key, bool add) => SortAsync(key);

    /// <summary>Whether Shift+click adds a sort (the header says so in its tooltip).</summary>
    bool MultiSort => false;

    /// <summary>The column's place among several sorts (a small number by its carets), or 0.</summary>
    int SortRank(string key) => 0;

    string? SearchOf(string key);

    /// <summary>Sets (or, when <paramref name="value"/> is empty, clears) a column's search.</summary>
    Task SearchAsync(string key, string? value);
}

/// <summary>
/// What a table cascades to its <see cref="ColumnHeader"/>s. The table hands down a new scope whenever its sort,
/// searches or filter row change, which re-renders the headers. With <paramref name="FilterRow"/> each searchable
/// header shows its search as a box under the title (the table's filter row) instead of the search icon. With
/// <paramref name="Locked"/> (unsaved edits in a table that searches on the server) the search boxes are disabled.
/// </summary>
public sealed record ColumnHeaderScope(IColumnHeaderHost Host, int Version, bool FilterRow = false, bool Locked = false);
