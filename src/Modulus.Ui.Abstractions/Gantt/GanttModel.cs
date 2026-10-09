using System.Globalization;

namespace Modulus.Ui.Abstractions.Gantt;

/// <summary>
/// A task on the timeline: its id and name, the days it runs (<see cref="Start"/> and <see cref="End"/> both count), its
/// progress from 0 to 100, the group it sits under (null for none), its bar's tone and its owner.
/// </summary>
public sealed record GanttTask(int Id, string Name, DateTime Start, DateTime End, int Progress = 0, string? Group = null, string Tone = "primary", string? Owner = null);

/// <summary>A week tick on the header: its label and where it sits, as a percentage from the timeline's left edge.</summary>
public sealed record GanttTick(string Label, double LeftPct);

/// <summary>
/// A row ready to draw: the task, whether it opens a new group, its bar (left edge and width as percentages of the
/// timeline), its progress, whether it is late (it ended before today and is not finished), and whether the bar runs past
/// either edge of the timeline.
/// </summary>
public sealed record GanttRowView(GanttTask Task, bool NewGroup, double LeftPct, double WidthPct, int Progress, bool Late, bool ClippedStart, bool ClippedEnd);

/// <summary>The timeline ready to draw: its first and last day, the week ticks, the rows, and where today falls (null when outside).</summary>
public sealed record GanttView(DateTime From, DateTime To, IReadOnlyList<GanttTick> Ticks, IReadOnlyList<GanttRowView> Rows, double? TodayPct);

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
        string? previous = null;
        foreach (var task in ordered)
        {
            var group = task.Group ?? "";
            var start = task.Start.Date < from ? from : task.Start.Date;
            var end = task.End.Date > to ? to : task.End.Date;
            var progress = Math.Clamp(task.Progress, 0, 100);
            var width = ((end - start).Days + 1) / (double)days * 100;
            rows.Add(new GanttRowView(task, group != previous,
                Pct(start, from, days), width,
                progress, task.End.Date < today.Date && progress < 100,
                task.Start.Date < from, task.End.Date > to));
            previous = group;
        }

        var ticks = new List<GanttTick>();
        for (var day = from; day <= to; day = day.AddDays(1))
            if (day.DayOfWeek == DayOfWeek.Monday)
                ticks.Add(new GanttTick(day.ToString("MMM d", CultureInfo.InvariantCulture), Pct(day, from, days)));

        double? todayPct = today.Date >= from && today.Date <= to ? Pct(today.Date, from, days) + 50.0 / days : null;
        return new GanttView(from, to, ticks, rows, todayPct);
    }

    /// <summary>The left edge of a day as a percentage of the range (the day's own start).</summary>
    private static double Pct(DateTime day, DateTime from, int days) => (day - from).Days / (double)days * 100;
}
