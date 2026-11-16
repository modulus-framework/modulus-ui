using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions;
public static partial class GridSchema
{
    // ---- The batch bar (GridBatchAction) ----

    /// <summary>The rows of <paramref name="selected"/> that <paramref name="action"/> acts on: not locked, and <see cref="GridBatchAction.When"/>.</summary>
    public static List<GridRecord> BatchRows(GridBatchAction action, IEnumerable<GridRecord> selected) =>
        [.. selected.Where(r => Applies(action, r))];

    /// <summary>Whether <paramref name="action"/> acts on <paramref name="row"/> (a print takes every row).</summary>
    public static bool Applies(GridBatchAction action, GridRecord row) =>
        action.Print || (row.State != GridRowState.Locked && (action.When?.Invoke(row) ?? true));

    /// <summary>The keys of the actions that act on <paramref name="row"/>, space-separated (MVC: the row's <c>data-m-batch</c>).</summary>
    public static string BatchKeys(IEnumerable<GridBatchAction> actions, GridRecord row) =>
        string.Join(' ', actions.Where(a => !a.Print && Applies(a, row)).Select(a => a.Key));

    /// <summary>"Approve 3 rows?", and " 2 selected rows are skipped." when some don't apply: the confirm.</summary>
    public static string BatchConfirm(GridBatchAction action, int count, int selected)
    {
        var skipped = selected - count;
        return $"{action.Label} {count} {(count == 1 ? "row" : "rows")}?" +
            (skipped > 0 ? $" {skipped} selected {(skipped == 1 ? "row is" : "rows are")} skipped." : "");
    }

    /// <summary>The batch button's tooltip when none of the selected rows apply.</summary>
    public static string BatchNone(GridBatchAction action) => $"{action.Label} doesn't apply to the selected rows.";

    /// <summary>A valid <see cref="GridBatchAction.Key"/> (it travels in a post).</summary>
    public static bool IsBatchKey(string? key) =>
        !string.IsNullOrEmpty(key) && key.Length <= 40 && key.All(ch => char.IsAsciiLetterOrDigit(ch) || ch is '-' or '_');

    /// <summary>
    /// Keeps a posted grid (MVC: <c>EditableGridForm.Read</c>) to the row states of the <paramref name="stored"/> rows,
    /// since a browser can post anything: read-only and locked rows get their stored values, state and reason back, and
    /// a locked row missing from the post (deleted in the browser) is put back where it was. Call it before applying
    /// the post's operation.
    /// </summary>
    public static void Protect(IEnumerable<GridColumn> columns, List<GridRecord> posted, IReadOnlyList<GridRecord> stored)
    {
        var cols = columns.ToList();
        for (var i = 0; i < stored.Count; i++)
        {
            var s = stored[i];
            if (s.Editable) continue;
            var p = posted.Find(r => r.Id == s.Id);
            if (p is null)
            {
                if (s.State != GridRowState.Locked) continue;
                p = new GridRecord(s.Id);
                foreach (var c in cols) p[c.Key] = c.Type == GridCellType.Switch ? "false" : null;
                posted.Insert(Math.Min(i, posted.Count), p);
            }
            // The posted columns: one removed in this session stays removed, one added keeps its empty value.
            foreach (var c in cols)
                if (s.Values.TryGetValue(c.Key, out var value)) p[c.Key] = value;
            p.State = s.State;
            p.StateReason = s.StateReason;
        }
    }

    public static string? ValidateCell(GridColumn c, string? value)
    {
        if (string.IsNullOrEmpty(value))
            return c.Required && c.Type != GridCellType.Switch ? $"{c.Title} is required." : null;
        switch (c.Type)
        {
            case GridCellType.Number or GridCellType.Currency or GridCellType.Percent or GridCellType.Progress or GridCellType.Rating:
                if (Number(value) is not { } d) return $"{c.Title} must be a number.";
                if (c.Min is { } min && d < min) return $"{c.Title} must be at least {min.ToString(CultureInfo.CurrentCulture)}.";
                if (c.Max is { } max && d > max) return $"{c.Title} must be at most {max.ToString(CultureInfo.CurrentCulture)}.";
                return null;
            case GridCellType.Date:
                return Date(value) is null ? $"{c.Title} must be a date." : null;
            case GridCellType.Select or GridCellType.Tag:
                return Option(c, value) is null ? $"Choose a valid {c.Title.ToLowerInvariant()}." : null;
            case GridCellType.MultiSelect:
                return Many(value).All(v => Option(c, v) is not null) ? null : $"Choose valid {c.Title.ToLowerInvariant()}.";
            default:
                return null;
        }
    }
}

