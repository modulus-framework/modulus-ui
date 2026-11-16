using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;
public static partial class GridSchema
{
    // ---- Round trip (MVC keeps the schema in a hidden field between posts) ----

    public static string Serialize(IEnumerable<GridColumn> columns) => JsonSerializer.Serialize(columns, Json);

    /// <summary>Reads a schema posted back by the browser; returns null when it is missing or malformed.</summary>
    public static List<GridColumn>? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            var columns = JsonSerializer.Deserialize<List<GridColumn>>(json, Json);
            if (columns is null || columns.Count == 0 || columns.Count > MaxColumns) return null;
            // The schema came from the client: keep keys unique and well formed.
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var c in columns)
            {
                if (string.IsNullOrWhiteSpace(c.Key) || c.Key.Length > 40 || !c.Key.All(char.IsAsciiLetterOrDigit) || !keys.Add(c.Key)) return null;
                c.Title = string.IsNullOrWhiteSpace(c.Title) ? c.Key : c.Title.Length > 60 ? c.Title[..60] : c.Title;
                c.Width = c.Width is { } w ? Math.Clamp(w, 60, 600) : null;
                c.Options ??= [];
                if (c.SubtitleKey is { } sub && (sub.Length > 40 || !sub.All(char.IsAsciiLetterOrDigit))) c.SubtitleKey = null;
            }
            return columns;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>A new row with an id not used by <paramref name="rows"/> and each column's empty value.</summary>
    public static GridRecord NewRow(IEnumerable<GridColumn> columns, IEnumerable<GridRecord> rows)
    {
        var taken = rows.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var n = taken.Count + 1;
        while (taken.Contains("n" + n)) n++;
        var row = new GridRecord("n" + n);
        foreach (var c in columns) row[c.Key] = c.Type == GridCellType.Switch ? "false" : null;
        return row;
    }
}

