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

    /// <summary>The sample's tasks, in their groups.</summary>
    public static IReadOnlyList<GanttTask> Tasks(DateTime today)
    {
        var monday = Monday(today);
        GanttTask Task(int id, string name, string group, int startDay, int endDay, int progress, string tone, string owner) =>
            new(id, name, monday.AddDays(startDay), monday.AddDays(endDay), progress, group, tone, owner);

        return
        [
            Task(1, "Hosting setup", "Discovery", -7, -2, 40, "warning", "Linus Torvalds"),
            Task(2, "Requirements", "Discovery", 0, 4, 100, "success", "Ada Lovelace"),
            Task(3, "Design review", "Discovery", 5, 7, 100, "success", "Grace Hopper"),
            Task(4, "Frontend", "Build", 7, 21, 60, "primary", "Linus Torvalds"),
            Task(5, "API", "Build", 7, 18, 80, "primary", "Margaret Hamilton"),
            Task(6, "Data migration", "Build", 14, 28, 20, "primary", "Ada Lovelace"),
            Task(7, "Testing", "Launch", 25, 35, 0, "warning", "Grace Hopper"),
            Task(8, "Documentation", "Launch", 30, 40, 0, "warning", "Margaret Hamilton"),
            Task(9, "Launch day", "Launch", 42, 42, 0, "danger", "Linus Torvalds"),
        ];
    }

    /// <summary>The timeline to draw for <paramref name="today"/>.</summary>
    public static GanttView Board(DateTime today) => GanttModel.Build(Tasks(today), From(today), To(today), today);

    private static DateTime Monday(DateTime day) => day.Date.AddDays(-(((int)day.DayOfWeek + 6) % 7));
}
