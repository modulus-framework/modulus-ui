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

    string? SearchOf(string key);

    /// <summary>Sets (or, when <paramref name="value"/> is empty, clears) a column's search.</summary>
    Task SearchAsync(string key, string? value);
}

/// <summary>
/// What a table cascades to its <see cref="ColumnHeader"/>s. The table hands down a new scope whenever its sort or
/// searches change, which re-renders the headers.
/// </summary>
public sealed record ColumnHeaderScope(IColumnHeaderHost Host, int Version);
