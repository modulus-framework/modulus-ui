using System.Globalization;

namespace Modulus.Ui.Abstractions.Gantt;

/// <summary>
/// A task on the timeline: its id and name, the days it runs (<see cref="Start"/> and <see cref="End"/> both count), its
/// progress from 0 to 100, the group it sits under (null for none), its bar's tone and its owner.
/// </summary>
/// <param name="DependsOn">The ids of the tasks that must end before this one starts (finish to start). Null for none.</param>
public sealed record GanttTask(int Id, string Name, DateTime Start, DateTime End, int Progress = 0, string? Group = null, string Tone = "primary", string? Owner = null, IReadOnlyList<int>? DependsOn = null);

/// <summary>A dependency drawn from the end of one task to the start of the next: the points as percentages of the timeline and vertical slots (rows, group headings included).</summary>
/// <param name="Conflict">True when the next task starts on or before the one it depends on ends.</param>
public sealed record GanttLink(int From, int To, double X1Pct, double Slot1, double X2Pct, double Slot2, bool Conflict);

/// <summary>A new date range for a task, as the timeline's editor posts it (both days count).</summary>
public sealed record GanttMove(int Id, DateTime Start, DateTime End);

/// <summary>A week tick on the header: its label and where it sits, as a percentage from the timeline's left edge.</summary>
public sealed record GanttTick(string Label, double LeftPct);

/// <summary>
/// A row ready to draw: the task, whether it opens a new group, its bar (left edge and width as percentages of the
/// timeline), its progress, whether it is late (it ended before today and is not finished), and whether the bar runs past
/// either edge of the timeline.
/// </summary>
/// <param name="Slot">The row's vertical position: rows and group headings each take one slot of the same height.</param>
public sealed record GanttRowView(GanttTask Task, bool NewGroup, double LeftPct, double WidthPct, int Progress, bool Late, bool ClippedStart, bool ClippedEnd, int Slot = 0);

/// <summary>The timeline ready to draw: its first and last day, the week ticks, the rows, and where today falls (null when outside).</summary>
/// <param name="Links">The dependencies between rows on the timeline (only those whose two tasks are shown).</param>
/// <param name="Slots">How many slots the rows and group headings take.</param>
public sealed record GanttView(DateTime From, DateTime To, IReadOnlyList<GanttTick> Ticks, IReadOnlyList<GanttRowView> Rows, double? TodayPct, IReadOnlyList<GanttLink>? Links = null, int Slots = 0);

/// <summary>
/// The timeline's model, shared by the Blazor <c>Gantt</c> and the MVC <c>app-gantt</c>: the tasks that reach into the range,
/// grouped in the order their groups first appear, each bar placed on the range's days, weekly ticks on the Mondays, and the
/// late and clipped flags. Both front ends draw from this view.
/// </summary>
public static class GanttModel
{
    /// <summary>The longest range a timeline may show.</summary>
    public const int MaxDays = 366;

    /// <summary>
    /// Builds the timeline for <paramref name="from"/> to <paramref name="to"/> (both days count). Tasks that do not reach into the
    /// range are left out. Throws when the range is empty or longer than <see cref="MaxDays"/>, or when a task ends before it starts.
    /// </summary>
    public static GanttView Build(IEnumerable<GanttTask> tasks, DateTime from, DateTime to, DateTime today)
    {
        from = from.Date;
        to = to.Date;
        var days = (to - from).Days + 1;
        if (days < 1 || days > MaxDays) throw new ArgumentOutOfRangeException(nameof(to), $"A timeline spans 1 to {MaxDays} days.");

        var all = tasks.ToList();
        foreach (var task in all)
            if (task.End.Date < task.Start.Date) throw new ArgumentException($"Task \"{task.Name}\" ends before it starts.", nameof(tasks));

        var shown = all.Where(t => t.End.Date >= from && t.Start.Date <= to).ToList();
        var ordered = shown.GroupBy(t => t.Group ?? "")
            .SelectMany(g => g.OrderBy(t => t.Start).ThenBy(t => t.Id))
            .ToList();

        var rows = new List<GanttRowView>();
        var slotOf = new Dictionary<int, int>();
        string? previous = null;
        var slot = 0;
        foreach (var task in ordered)
        {
            var group = task.Group ?? "";
            var start = task.Start.Date < from ? from : task.Start.Date;
            var end = task.End.Date > to ? to : task.End.Date;
            var progress = Math.Clamp(task.Progress, 0, 100);
            var width = ((end - start).Days + 1) / (double)days * 100;
            var newGroup = group != previous;
            if (newGroup) slot++; // the group heading takes a slot of its own
            slot++;
            slotOf[task.Id] = slot - 1;
            rows.Add(new GanttRowView(task, newGroup,
                Pct(start, from, days), width,
                progress, task.End.Date < today.Date && progress < 100,
                task.Start.Date < from, task.End.Date > to, slot - 1));
            previous = group;
        }

        var byId = shown.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());
        var links = new List<GanttLink>();
        foreach (var task in ordered)
        {
            if (task.DependsOn is null) continue;
            foreach (var fromId in task.DependsOn.Distinct())
            {
                if (!byId.TryGetValue(fromId, out var before) || !slotOf.ContainsKey(fromId) || !slotOf.ContainsKey(task.Id)) continue;
                var beforeEnd = before.End.Date > to ? to : before.End.Date;
                var taskStart = task.Start.Date < from ? from : task.Start.Date;
                links.Add(new GanttLink(fromId, task.Id,
                    Math.Min(100, Pct(beforeEnd, from, days) + 100.0 / days), slotOf[fromId], Pct(taskStart, from, days), slotOf[task.Id],
                    task.Start.Date <= before.End.Date));
            }
        }

        var ticks = new List<GanttTick>();
        for (var day = from; day <= to; day = day.AddDays(1))
            if (day.DayOfWeek == DayOfWeek.Monday)
                ticks.Add(new GanttTick(day.ToString("MMM d", CultureInfo.InvariantCulture), Pct(day, from, days)));

        double? todayPct = today.Date >= from && today.Date <= to ? Pct(today.Date, from, days) + 50.0 / days : null;
        return new GanttView(from, to, ticks, rows, todayPct, links, slot);
    }

    /// <summary>The task moved by <paramref name="days"/> (both its dates; its length is kept).</summary>
    public static GanttTask Shift(GanttTask task, int days) => task with { Start = task.Start.AddDays(days), End = task.End.AddDays(days) };

    /// <summary>
    /// The dependencies a schedule breaks: a task that starts on or before a task it depends on ends. Each is a sentence for the page.
    /// </summary>
    public static IReadOnlyList<string> Conflicts(IEnumerable<GanttTask> tasks)
    {
        var all = tasks.ToList();
        var byId = all.GroupBy(t => t.Id).ToDictionary(g => g.Key, g => g.First());
        var found = new List<string>();
        foreach (var task in all)
            foreach (var id in (task.DependsOn ?? []).Distinct())
                if (byId.TryGetValue(id, out var before) && task.Start.Date <= before.End.Date)
                    found.Add($"{task.Name} starts on or before {before.Name} ends.");
        return found;
    }

    /// <summary>The left edge of a day as a percentage of the range (the day's own start).</summary>
    private static double Pct(DateTime day, DateTime from, int days) => (day - from).Days / (double)days * 100;
}
