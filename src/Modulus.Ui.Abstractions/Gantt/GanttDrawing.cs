using System.Globalization;
using System.Text.Json;

namespace Modulus.Ui.Abstractions.Gantt;

/// <summary>What both front ends draw the timeline's dependency arrows and drag moves with: the same geometry and the same parsing.</summary>
public static class GanttDrawing
{
    /// <summary>The height of a row or a group heading, in pixels (the stylesheet uses the same value).</summary>
    public const int RowHeight = 52;

    /// <summary>The SVG viewBox that covers every slot: x in percent of the timeline, y in pixels.</summary>
    public static string ViewBox(GanttView view) => $"0 0 100 {view.Slots * RowHeight}";

    /// <summary>The path of one dependency: out of the end of the earlier task, across, and into the start of the later one.</summary>
    public static string LinkPath(GanttLink link)
    {
        var y1 = link.Slot1 * RowHeight + RowHeight / 2.0;
        var y2 = link.Slot2 * RowHeight + RowHeight / 2.0;
        var mid = (link.X1Pct + link.X2Pct) / 2;
        return string.Create(CultureInfo.InvariantCulture, $"M{link.X1Pct:0.###} {y1:0.###} H{mid:0.###} V{y2:0.###} H{link.X2Pct:0.###}");
    }

    /// <summary>The moves a timeline editor posts: a JSON array of {"id", "start", "end"} with dates as yyyy-MM-dd. Bad entries are skipped.</summary>
    public static IReadOnlyList<GanttMove> ParseMoves(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            using var doc = JsonDocument.Parse(json);
            var moves = new List<GanttMove>();
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                if (!item.TryGetProperty("id", out var id) || id.ValueKind != JsonValueKind.Number) continue;
                if (!item.TryGetProperty("start", out var start) || !DateTime.TryParseExact(start.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var s)) continue;
                if (!item.TryGetProperty("end", out var end) || !DateTime.TryParseExact(end.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var e)) continue;
                if (e < s) continue;
                moves.Add(new GanttMove(id.GetInt32(), s, e));
            }
            return moves;
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>A date as the editor writes it (yyyy-MM-dd).</summary>
    public static string Iso(DateTime day) => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
