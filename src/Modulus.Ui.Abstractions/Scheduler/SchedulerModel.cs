using System.Globalization;

namespace Modulus.Ui.Abstractions.Scheduler;

/// <summary>An event on the calendar: its id, title, when it runs, whether it is all day, its category (by name) and where it is.</summary>
public sealed record SchedulerEvent(int Id, string Title, DateTime Start, DateTime End, bool AllDay, string Category, string Location = "");

/// <summary>A category an event can take, with its colour (any CSS colour value; the filter and the event chips use it).</summary>
public sealed record SchedulerCategory(string Name, string Color);

/// <summary>What the calendar shows: the month, the month grid or the agenda list, and the categories whose events show.</summary>
public sealed record SchedulerQuery(DateTime Month, bool List, IReadOnlySet<string> Categories);

/// <summary>
/// The calendar ready to draw: its query, the categories, the events in the six-week grid that pass the filter, and the
/// time it is drawn at (today and "upcoming" are measured from it).
/// </summary>
public sealed record SchedulerView(SchedulerQuery Query, IReadOnlyList<SchedulerCategory> Categories, IReadOnlyList<SchedulerEvent> Events, DateTime Now)
{
    /// <summary>The first of the month shown.</summary>
    public DateTime Month => Query.Month;

    /// <summary>True for the agenda list, false for the month grid.</summary>
    public bool List => Query.List;

    /// <summary>The day <see cref="Now"/> falls on.</summary>
    public DateTime Today => Now.Date;

    /// <summary>The heading: the month and year.</summary>
    public string Title => Month.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>The six weeks the grid shows, from the Sunday on or before the 1st.</summary>
    public IEnumerable<DateTime> Days => Enumerable.Range(0, SchedulerModel.GridDays).Select(i => SchedulerModel.GridStart(Month).AddDays(i));

    /// <summary>Whether a category's events show.</summary>
    public bool IsOn(string category) => Query.Categories.Contains(category);

    /// <summary>The colour of a category, or a neutral one for a name that is not in the list.</summary>
    public string ColorOf(string category) => Categories.FirstOrDefault(c => c.Name == category)?.Color ?? "var(--m-text-tertiary)";

    /// <summary>The events touching a day: all-day first, then by start time.</summary>
    public IEnumerable<SchedulerEvent> On(DateTime day) =>
        Events.Where(e => e.Start.Date <= day && e.End.Date >= day).OrderBy(e => !e.AllDay).ThenBy(e => e.Start);

    /// <summary>The agenda: each day of the month that has events, with those events.</summary>
    public IEnumerable<(DateTime Day, IReadOnlyList<SchedulerEvent> Events)> Agenda =>
        Enumerable.Range(0, DateTime.DaysInMonth(Month.Year, Month.Month)).Select(i => Month.AddDays(i))
            .Select(d => (d, (IReadOnlyList<SchedulerEvent>)On(d).ToList())).Where(x => x.Item2.Count > 0);

    /// <summary>The next events that have not ended yet, soonest first.</summary>
    public IReadOnlyList<SchedulerEvent> Upcoming(int count = 4) =>
        Events.Where(e => e.End >= Now).OrderBy(e => e.Start).Take(count).ToList();

    /// <summary>
    /// Draws the calendar from the events it was given: the events are kept when they reach into the six weeks the query
    /// shows and their category is on.
    /// </summary>
    public static SchedulerView Build(SchedulerQuery query, IReadOnlyList<SchedulerCategory> categories, IEnumerable<SchedulerEvent> events, DateTime now)
    {
        var start = SchedulerModel.GridStart(query.Month);
        var end = start.AddDays(SchedulerModel.GridDays);
        var shown = events.Where(e => e.End >= start && e.Start < end && query.Categories.Contains(e.Category)).ToList();
        return new SchedulerView(query, categories, shown, now);
    }
}

/// <summary>
/// The calendar's model, shared by the Blazor <c>Scheduler</c> and the MVC <c>app-scheduler</c>: the query from the URL
/// (month, view, categories), the grid's first day, and the links between months and views.
/// </summary>
public static class SchedulerModel
{
    /// <summary>The days the month grid shows: six weeks, so every month fits.</summary>
    public const int GridDays = 42;

    /// <summary>Sunday on or before the 1st of the month.</summary>
    public static DateTime GridStart(DateTime month) => month.AddDays(-(int)month.DayOfWeek);

    /// <summary>The month from its <c>yyyy-MM</c> text; this month when the text is missing or wrong.</summary>
    public static DateTime ParseMonth(string? month, DateTime today) =>
        DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m)
            ? m
            : new DateTime(today.Year, today.Month, 1);

    /// <summary>
    /// The query from the URL. Every category shows unless the filter form was submitted (<paramref name="filtered"/>), in
    /// which case only the named categories show. Names match the categories ignoring case.
    /// </summary>
    public static SchedulerQuery Parse(string? month, string? view, IEnumerable<string>? names, bool filtered, IReadOnlyList<SchedulerCategory> categories, DateTime today)
    {
        var all = categories.Select(c => c.Name).ToHashSet();
        var chosen = filtered
            ? categories.Select(c => c.Name).Where(name => (names ?? Array.Empty<string>()).Contains(name, StringComparer.OrdinalIgnoreCase)).ToHashSet()
            : all;
        return new SchedulerQuery(ParseMonth(month, today), view == "list", chosen);
    }

    /// <summary>
    /// The query string for a query: <c>?month=yyyy-MM</c>, <c>&amp;view=list</c> for the agenda, and when some categories are
    /// off, <c>&amp;filtered=true</c> with the ones on as <c>cat</c> values. The categories are sorted so the link is stable.
    /// </summary>
    public static string QueryString(SchedulerQuery query, IReadOnlyCollection<string> allCategories)
    {
        var parts = new List<string> { $"month={query.Month.ToString("yyyy-MM", CultureInfo.InvariantCulture)}" };
        if (query.List) parts.Add("view=list");
        if (query.Categories.Count < allCategories.Count)
        {
            parts.Add("filtered=true");
            parts.AddRange(query.Categories.OrderBy(c => c, StringComparer.Ordinal).Select(c => $"cat={Uri.EscapeDataString(c)}"));
        }
        return "?" + string.Join('&', parts);
    }

    /// <summary>The time label for a chip or agenda row: "All day", or the start time as "9am" / "2:30pm".</summary>
    public static string TimeLabel(SchedulerEvent e) =>
        e.AllDay ? "All day" : e.Start.ToString("h:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant().Replace(":00", "");
}
