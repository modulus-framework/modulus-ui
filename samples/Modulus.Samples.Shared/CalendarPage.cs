using Modulus.Ui.Abstractions.Scheduler;

namespace Modulus.Samples.Shared;

/// <summary>
/// The sample calendar: its categories (one per <see cref="EventCategory"/>, in the series colours) and the store's events
/// as the library's scheduler reads them. Shared by the Blazor page, the MVC controller and the Razor Pages model.
/// </summary>
public static class CalendarPage
{
    public static readonly IReadOnlyList<SchedulerCategory> Categories =
        Enum.GetValues<EventCategory>().Select(c => new SchedulerCategory(c.ToString(), SampleUi.EventColor(c))).ToList();

    /// <summary>The query from the URL: the month, the agenda or the grid, and the category filter.</summary>
    public static SchedulerQuery Query(string? month, string? view, IEnumerable<string>? cats, bool filtered, DateTime today) =>
        SchedulerModel.Parse(month, view, cats, filtered, Categories, today);

    /// <summary>The calendar for a query: the store's events in its six weeks, drawn at <paramref name="now"/>.</summary>
    public static SchedulerView Load(SampleStore store, SchedulerQuery query, DateTime now)
    {
        var start = SchedulerModel.GridStart(query.Month);
        var events = store.Events(start, start.AddDays(SchedulerModel.GridDays))
            .Select(e => new SchedulerEvent(e.Id, e.Title, e.Start, e.End, e.AllDay, e.Category.ToString(), e.Location))
            .ToList();
        return SchedulerView.Build(query, Categories, events, now);
    }
}
