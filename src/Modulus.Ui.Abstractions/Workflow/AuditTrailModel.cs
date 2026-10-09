using System.Globalization;

namespace Modulus.Ui.Abstractions.Workflow;

/// <summary>
/// One entry of a record's audit trail: when it happened, who did it, what they did (an action such as "Edited" or
/// "Approved"), and for a field change the field and its values before and after (null when there was none).
/// </summary>
public sealed record AuditEntry(int Id, DateTime At, string Actor, string Action, string? Field = null, string? Before = null, string? After = null, string? Note = null);

/// <summary>An entry ready to draw: the entry, its time (HH:mm) and its change as one line of text.</summary>
public sealed record AuditRow(AuditEntry Entry, string TimeText, string ChangeText);

/// <summary>The entries of one day, newest first.</summary>
public sealed record AuditDay(DateTime Day, IReadOnlyList<AuditRow> Rows);

/// <summary>The trail ready to draw: the actions to filter by, the filter in force (null for all), and the days, newest first.</summary>
public sealed record AuditTrailView(IReadOnlyList<string> Actions, string? Action, IReadOnlyList<AuditDay> Days);

/// <summary>
/// The audit trail's model, shared by the Blazor <c>AuditTrail</c> and the MVC <c>app-audit-trail</c>: the entries grouped
/// by day (newest day first, newest entry first within a day), filtered by action when one is given (case ignored).
/// </summary>
public static class AuditTrailModel
{
    /// <summary>Builds the trail. A blank <paramref name="action"/> shows every entry.</summary>
    public static AuditTrailView Build(IEnumerable<AuditEntry> entries, string? action = null)
    {
        var all = entries.ToList();
        var actions = all.Select(e => e.Action).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(a => a, StringComparer.OrdinalIgnoreCase).ToList();
        var filter = string.IsNullOrWhiteSpace(action) ? null : action.Trim();
        var shown = filter is null ? all : all.Where(e => string.Equals(e.Action, filter, StringComparison.OrdinalIgnoreCase)).ToList();

        var days = shown.GroupBy(e => e.At.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new AuditDay(g.Key, g.OrderByDescending(e => e.At).ThenByDescending(e => e.Id)
                .Select(e => new AuditRow(e, e.At.ToString("HH:mm", CultureInfo.InvariantCulture), ChangeOf(e))).ToList()))
            .ToList();
        return new AuditTrailView(actions, filter, days);
    }

    /// <summary>The change as one line: "Field: before → after" (an empty side reads "none"), or the note, or empty.</summary>
    public static string ChangeOf(AuditEntry entry)
    {
        if (entry.Field is { Length: > 0 })
            return $"{entry.Field}: {entry.Before ?? "none"} → {entry.After ?? "none"}";
        return entry.Note ?? "";
    }
}
