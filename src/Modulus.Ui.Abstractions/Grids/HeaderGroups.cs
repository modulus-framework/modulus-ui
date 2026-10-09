using System.Globalization;
using System.Net;
using System.Text;

namespace Modulus.Ui.Abstractions;

/// <summary>
/// A band over columns in a table header (a multi-level header): its title spans the shown columns of
/// <paramref name="Keys"/> that sit next to each other. Bands of <see cref="Level"/> 1 sit above those of level 0, and so on.
/// </summary>
/// <param name="Title">The band's title ("Job", "Pay", "Q1").</param>
/// <param name="Keys">The column keys under the band (a <c>ColumnHeader</c>'s key, a <c>PropertyColumn</c>'s property name).</param>
public sealed record HeaderGroup(string Title, params string[] Keys)
{
    /// <summary>0 for the row just above the column titles, 1 for the row above that, and so on.</summary>
    public int Level { get; init; }
}

/// <summary>One cell of a band row: a band's title over <see cref="Span"/> columns, or an empty cell (no title).</summary>
public readonly record struct HeaderBand(string? Title, int Span);

/// <summary>
/// The band rows of a multi-level header, rendered the same way by the Blazor ProTable and the MVC tables: one
/// <c>&lt;tr class="m-header-bands"&gt;</c> per level, above the column titles, from the shown columns in order.
/// </summary>
public static class HeaderGroups
{
    /// <summary>
    /// The cells of one level's row over <paramref name="columns"/> (the shown columns in order; null for a checkbox,
    /// expand or drag-handle column). Keys match without regard to case; neighbours under one band merge into one cell.
    /// </summary>
    public static IReadOnlyList<HeaderBand> Bands(IEnumerable<string?> columns, IEnumerable<HeaderGroup>? groups, int level = 0)
    {
        var byKey = new Dictionary<string, HeaderGroup>(StringComparer.OrdinalIgnoreCase);
        foreach (var group in groups ?? [])
            if (group.Level == level)
                foreach (var key in group.Keys) byKey.TryAdd(key, group);

        var bands = new List<HeaderBand>();
        HeaderGroup? current = null;
        foreach (var column in columns)
        {
            var group = column is not null && byKey.TryGetValue(column, out var g) ? g : null;
            if (bands.Count > 0 && ReferenceEquals(group, current))
                bands[^1] = bands[^1] with { Span = bands[^1].Span + 1 };
            else
                bands.Add(new HeaderBand(group?.Title, 1));
            current = group;
        }
        return bands;
    }

    /// <summary>The levels in use, outermost first (empty without groups).</summary>
    public static IReadOnlyList<int> Levels(IEnumerable<HeaderGroup>? groups) =>
        (groups ?? []).Select(g => Math.Max(0, g.Level)).Distinct().OrderDescending().ToList();

    /// <summary>
    /// The band rows over <paramref name="columns"/>, outermost first, for the top of a <c>&lt;thead&gt;</c>; empty
    /// when no band covers a shown column. A band's cell carries <c>data-m-span</c> (its columns), so hiding a column
    /// in the browser can narrow it (modulus-shell.js).
    /// <para>
    /// The first <paramref name="pinnedLeft"/> and last <paramref name="pinnedRight"/> columns are pinned (sticky) in the
    /// header row. Their band cells are pinned too, one per column, so the bands that scroll under them stay under them;
    /// modulus-shell.js copies each pinned cell's offset from the header cell below it. The bands span only the columns
    /// between.
    /// </para>
    /// </summary>
    public static string Rows(IEnumerable<string?> columns, IEnumerable<HeaderGroup>? groups, int pinnedLeft = 0, int pinnedRight = 0)
    {
        var list = groups?.ToList() ?? [];
        if (list.Count == 0) return "";
        var shown = columns.ToList();
        var lead = Math.Clamp(pinnedLeft, 0, shown.Count);
        var trail = Math.Clamp(pinnedRight, 0, shown.Count - lead);
        var middle = shown.Skip(lead).Take(shown.Count - lead - trail).ToList();
        var html = new StringBuilder();
        foreach (var level in Levels(list))
        {
            var bands = Bands(middle, list, level);
            if (!bands.Any(b => b.Title is not null)) continue;
            html.Append("<tr class=\"m-header-bands\">");
            for (var i = 0; i < lead; i++)
                html.Append("<td class=\"ant-table-cell ant-table-cell-fix-left m-header-band m-header-band-empty m-header-band-pin\" data-m-pin=\"left\"></td>");
            foreach (var band in bands)
            {
                var span = band.Span.ToString(CultureInfo.InvariantCulture);
                if (band.Title is null)
                    html.Append("<td class=\"ant-table-cell m-header-band m-header-band-empty\" colspan=\"").Append(span).Append("\" data-m-span=\"").Append(span).Append("\"></td>");
                else
                    html.Append("<th class=\"ant-table-cell m-header-band\" scope=\"colgroup\" colspan=\"").Append(span).Append("\" data-m-span=\"").Append(span).Append("\">")
                        .Append(WebUtility.HtmlEncode(band.Title)).Append("</th>");
            }
            for (var i = 0; i < trail; i++)
                html.Append("<td class=\"ant-table-cell ant-table-cell-fix-right m-header-band m-header-band-empty m-header-band-pin\" data-m-pin=\"right\"></td>");
            html.Append("</tr>");
        }
        return html.ToString();
    }
}
