using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;
/// <summary>Column operations, formatting, parsing and validation for schema-driven grids.</summary>
public static partial class GridSchema
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public const string DateFormat = "yyyy-MM-dd";

    /// <summary>Upper bound on columns (a posted schema with more is rejected).</summary>
    public const int MaxColumns = 50;

    /// <summary>Visible columns in display order: left-pinned first, right-pinned last (Ant Design requires pinned columns at the edges).</summary>
    public static IReadOnlyList<GridColumn> Visible(IEnumerable<GridColumn> columns)
    {
        var shown = columns.Where(c => !c.Hidden).ToList();
        return [.. shown.Where(c => c.Fixed == GridFixed.Left), .. shown.Where(c => c.Fixed == GridFixed.None), .. shown.Where(c => c.Fixed == GridFixed.Right)];
    }

    /// <summary>
    /// The columns <paramref name="allow"/> lets the user see (<see cref="State.FieldAccess.For"/>), for rendering and for
    /// reading posted rows: apply a post against this list, so a column the user can't see can't be written either.
    /// </summary>
    public static List<GridColumn> Allowed(IEnumerable<GridColumn> columns, Func<string, bool> allow) =>
        columns.Where(c => allow(c.Key)).ToList();

    /// <summary>Sticky offsets (px) for the pinned columns of <paramref name="visible"/>, keyed by column key.</summary>
    public static IReadOnlyDictionary<string, int> StickyOffsets(IReadOnlyList<GridColumn> visible, int leadingWidth = 0)
    {
        var offsets = new Dictionary<string, int>(StringComparer.Ordinal);
        var left = leadingWidth;
        foreach (var c in visible.Where(c => c.Fixed == GridFixed.Left)) { offsets[c.Key] = left; left += c.Width ?? 150; }
        var right = 0;
        foreach (var c in visible.Where(c => c.Fixed == GridFixed.Right).Reverse()) { offsets[c.Key] = right; right += c.Width ?? 150; }
        return offsets;
    }

    /// <summary>Total width of the visible columns, for the table's horizontal scroll (<c>scroll.x</c>).</summary>
    public static int ScrollWidth(IEnumerable<GridColumn> columns, int extra = 0) =>
        Visible(columns).Sum(c => c.Width ?? 150) + extra;
}

