using System.Net;
using System.Text.Json;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.Ui.Abstractions.Plugins;

public enum SortableLayout
{
    Vertical,
    Horizontal,
    Grid,
}

/// <summary>An item moved: <paramref name="Id"/> left list <paramref name="From"/> for position <paramref name="Index"/> of list <paramref name="To"/>.</summary>
public sealed record SortMove(string Id, string From, string To, int Index)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Reads what modulus-plugins.js writes to <c>[data-m-sort-result]</c>.</summary>
    public static bool TryParse(string? json, out SortMove move)
    {
        move = default!;
        if (string.IsNullOrWhiteSpace(json) || json[0] != '{') return false;
        try
        {
            var m = JsonSerializer.Deserialize<SortMove>(json, Json);
            if (m is null || string.IsNullOrEmpty(m.Id) || m.Index < 0) return false;
            move = m with { From = m.From ?? "", To = m.To ?? "" };
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Applies the move to lists held by name: takes the item out of <see cref="From"/> and inserts it into <see cref="To"/>.</summary>
    /// <returns>False when a list or the item is missing.</returns>
    public bool Apply<T>(IDictionary<string, List<T>> lists, Func<T, string> idOf)
    {
        if (!lists.TryGetValue(From, out var from) || !lists.TryGetValue(To, out var to)) return false;
        var at = from.FindIndex(x => idOf(x) == Id);
        if (at < 0) return false;
        var item = from[at];
        from.RemoveAt(at);
        to.Insert(Math.Clamp(Index, 0, to.Count), item);
        return true;
    }

    /// <summary>The ids in their new order once this item is moved to <see cref="Index"/> (the ids unchanged when it isn't among them).</summary>
    public List<string> Within(IEnumerable<string> ids)
    {
        var list = ids.ToList();
        var at = list.IndexOf(Id);
        if (at < 0) return list;
        list.RemoveAt(at);
        list.Insert(Math.Clamp(Index, 0, list.Count), Id);
        return list;
    }
}

/// <summary>
/// A table row dragged to a new place: row <paramref name="Id"/> moved to position <paramref name="Index"/> of the
/// page, and <paramref name="Order"/> is the page's row ids in their new order (store it with <see cref="GridSchema.Reorder"/>).
/// </summary>
public sealed record RowMove(string Id, int Index, IReadOnlyList<string> Order);

/// <summary>
/// Shared pieces of the sortable list (<c>AppSortable</c> in Blazor, <c>&lt;app-sortable&gt;</c> in MVC), run by
/// modulus-plugins.js ([data-m-sortable]).
/// </summary>
public static class SortableHtml
{
    /// <summary>What the handle's keys do, read to screen readers (aria-describedby).</summary>
    public const string KeyboardHelp = "Use the arrow keys to move the item, Home and End to move it to the start or end; Left and Right move it to the next list.";

    /// <summary>The Ant Design "drag" (move) icon of the drag handle; AntDesign 1.6 has no "holder" icon.</summary>
    public static string HandleIcon => AntIconSvg.Svg("drag") ?? AntIconSvg.Svg("holder") ?? "";

    /// <summary>The drag handle of an item named <paramref name="label"/>.</summary>
    public static string Handle(string label, string helpId, bool disabled = false) =>
        $"""<span class="m-sort-handle" data-m-sort-handle tabindex="{(disabled ? "-1" : "0")}" role="button" aria-label="Move {WebUtility.HtmlEncode(label)}" aria-roledescription="sortable handle" aria-describedby="{WebUtility.HtmlEncode(helpId)}">{HandleIcon}</span>""";

    /// <summary>What a row handle's keys do (the grids and the data table).</summary>
    public const string RowKeyboardHelp = "Use the Up and Down arrow keys to move the row, Home and End to move it to the top or bottom of the page.";

    /// <summary>Tooltip of a row handle while the rows are sorted by a column.</summary>
    public const string RowSortedText = "Clear the column sort to drag rows";

    /// <summary>Width of the drag handle column, in pixels.</summary>
    public const int RowHandleWidth = 40;

    /// <summary>
    /// The drag handle of a table row (<paramref name="id"/> is the row's key). The table's owner element carries
    /// <see cref="RowsAttributes"/>; the handle's row is the item modulus-plugins.js moves.
    /// </summary>
    public static string RowHandle(string id, string label, string helpId, bool disabled = false)
    {
        var name = WebUtility.HtmlEncode(label);
        var state = disabled ? $""" tabindex="-1" aria-disabled="true" title="{RowSortedText}" """.TrimEnd() : """ tabindex="0" title="Drag to reorder" """.TrimEnd();
        return $"""<span class="m-sort-handle m-row-handle" data-m-sort-handle data-m-sort-row="{WebUtility.HtmlEncode(id)}" data-m-sort-label="{name}"{state} role="button" aria-label="Move {name}" aria-roledescription="sortable handle" aria-describedby="{WebUtility.HtmlEncode(helpId)}">{HandleIcon}</span>""";
    }

    /// <summary>
    /// Attributes of the element around a table whose rows drag (<c>data-m-sort-rows</c>). <paramref name="eventMode"/>
    /// (Blazor) leaves the DOM to the component; otherwise the row moves and a <c>modulus:sort</c> event follows.
    /// </summary>
    public static string RowsAttributes(string name, string? title, bool disabled, bool eventMode) =>
        $""" data-m-sort-rows="{WebUtility.HtmlEncode(name)}" data-m-sort-root{(eventMode ? " data-m-sort-mode=\"event\"" : "")}{(disabled ? " data-m-sort-disabled" : "")}{(title is null ? "" : $" data-m-sort-title=\"{WebUtility.HtmlEncode(title)}\"")}""";

    /// <summary>The live region and keyboard help that go inside the owner element (see <see cref="RowsAttributes"/>).</summary>
    public static string RowsHelp(string helpId) =>
        $"""<span class="m-sort-live" data-m-sort-live aria-live="assertive"></span><span class="m-sort-live" id="{WebUtility.HtmlEncode(helpId)}">{RowKeyboardHelp}</span>""";

    public static string LayoutClass(SortableLayout layout) => layout switch
    {
        SortableLayout.Grid => " m-sortable-grid",
        SortableLayout.Horizontal => " m-sortable-horizontal",
        _ => "",
    };

    public static string? LayoutAttribute(SortableLayout layout) =>
        layout == SortableLayout.Vertical ? null : layout.ToString().ToLowerInvariant();
}
