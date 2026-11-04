using Modulus.Ui.Abstractions.Gantt;

namespace Modulus.Samples.Shared;

/// <summary>
/// The Gantt sample: a website relaunch planned in weeks. The tasks are laid out from this week's Monday, so the sample
/// always shows the same shape: one late task, some done, some under way, and a launch at the end.
/// </summary>
public static class GanttPage
{
    /// <summary>The timeline's first day: the Monday a week before this one.</summary>
    public static DateTime From(DateTime today) => Monday(today).AddDays(-7);

    /// <summary>The timeline's last day: eight weeks from <see cref="From"/>.</summary>
    public static DateTime To(DateTime today) => From(today).AddDays(55);

    /// <summary>
    /// The sample's tasks, in their groups, with the dates they were moved to on this server (kept in memory; an app keeps its plan in its
    /// own store). Each task depends on the ones that must finish first.
    /// </summary>
    public static IReadOnlyList<GanttTask> Tasks(DateTime today)
    {
        var monday = Monday(today);
        lock (Lock)
        {
            return Plan(monday).Select(t => _moved.TryGetValue(t.Id, out var days) ? GanttModel.Shift(t, days) : t).ToList();
        }
    }

    private static readonly object Lock = new();
    private static readonly Dictionary<int, int> _moved = new();

    private static IReadOnlyList<GanttTask> Plan(DateTime monday)
    {
        GanttTask Task(int id, string name, string group, int startDay, int endDay, int progress, string tone, string owner, params int[] after) =>
            new(id, name, monday.AddDays(startDay), monday.AddDays(endDay), progress, group, tone, owner, after.Length == 0 ? null : after);

        return
        [
            Task(1, "Hosting setup", "Discovery", -7, -2, 40, "warning", "Linus Torvalds"),
            Task(2, "Requirements", "Discovery", 0, 4, 100, "success", "Ada Lovelace", 1),
            Task(3, "Design review", "Discovery", 5, 7, 100, "success", "Grace Hopper", 2),
            Task(4, "Frontend", "Build", 7, 21, 60, "primary", "Linus Torvalds", 3),
            Task(5, "API", "Build", 7, 18, 80, "primary", "Margaret Hamilton", 2),
            Task(6, "Data migration", "Build", 14, 28, 20, "primary", "Ada Lovelace", 5),
            Task(7, "Testing", "Launch", 25, 35, 0, "warning", "Grace Hopper", 6, 4),
            Task(8, "Documentation", "Launch", 30, 40, 0, "warning", "Margaret Hamilton", 4),
            Task(9, "Launch day", "Launch", 42, 42, 0, "danger", "Linus Torvalds", 7, 8),
        ];
    }

    /// <summary>Moves a task to new dates (the start decides the shift; the length is kept). Refused when a task it depends on would end after it starts.</summary>
    public static bool Move(GanttMove move, DateTime today)
    {
        var monday = Monday(today);
        lock (Lock)
        {
            var original = Plan(monday).FirstOrDefault(t => t.Id == move.Id);
            if (original is null) return false;
            var days = (move.Start.Date - original.Start.Date).Days;
            var trial = Plan(monday).Select(t => t.Id == move.Id ? GanttModel.Shift(t, days) : (_moved.TryGetValue(t.Id, out var d) ? GanttModel.Shift(t, d) : t)).ToList();
            var byId = trial.ToDictionary(t => t.Id);
            // Refused when the moved task is the one that starts too early, or the one something starts too early after.
            var breaks = trial.Any(t => (t.DependsOn ?? []).Any(d =>
                (t.Id == move.Id || d == move.Id) && byId.TryGetValue(d, out var before) && t.Start.Date <= before.End.Date));
            if (breaks) return false;
            _moved[move.Id] = days;
            return true;
        }
    }

    /// <summary>Applies the moves an editor posted (a JSON array); returns how many were applied.</summary>
    public static int ApplyMoves(string? json, DateTime today) =>
        GanttDrawing.ParseMoves(json).Count(m => Move(m, today));

    /// <summary>The timeline to draw for <paramref name="today"/>.</summary>
    public static GanttView Board(DateTime today) => GanttModel.Build(Tasks(today), From(today), To(today), today);

    /// <summary>The dependencies the current dates break, for the page to say.</summary>
    public static IReadOnlyList<string> Conflicts(DateTime today) => GanttModel.Conflicts(Tasks(today));

    private static DateTime Monday(DateTime day) => day.Date.AddDays(-(((int)day.DayOfWeek + 6) % 7));
}
