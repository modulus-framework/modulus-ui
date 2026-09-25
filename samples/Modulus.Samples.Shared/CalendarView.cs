using System.Globalization;

namespace Modulus.Samples.Shared;

/// <summary>
/// One calendar screen (month grid or agenda list) with the category filter applied. Shared by the
/// Blazor page and the MVC / Razor Pages view so all three draw the same weeks.
/// </summary>
public sealed record CalendarView(DateTime Month, bool List, IReadOnlySet<EventCategory> Categories, IReadOnlyList<CalendarEvent> Events)
{
    public static readonly EventCategory[] AllCategories = Enum.GetValues<EventCategory>();

    /// <summary>Sunday on or before the 1st; the grid always shows six weeks.</summary>
    public DateTime GridStart => Month.AddDays(-(int)Month.DayOfWeek);

    public IEnumerable<DateTime> Days => Enumerable.Range(0, 42).Select(i => GridStart.AddDays(i));

    public string Title => Month.ToString("MMMM yyyy", CultureInfo.InvariantCulture);

    /// <summary>Events touching a day: all-day first, then by start time.</summary>
    public IEnumerable<CalendarEvent> On(DateTime day) =>
        Events.Where(e => e.Start.Date <= day && e.End.Date >= day).OrderBy(e => !e.AllDay).ThenBy(e => e.Start);

    /// <summary>Agenda: days of the month that have events.</summary>
    public IEnumerable<(DateTime Day, IReadOnlyList<CalendarEvent> Events)> Agenda =>
        Enumerable.Range(0, DateTime.DaysInMonth(Month.Year, Month.Month)).Select(i => Month.AddDays(i))
            .Select(d => (d, (IReadOnlyList<CalendarEvent>)On(d).ToList())).Where(x => x.Item2.Count > 0);

    /// <summary>Query string for another month / view with the current filter.</summary>
    public string Query(DateTime? month = null, bool? list = null)
    {
        var parts = new List<string> { $"month={(month ?? Month):yyyy-MM}" };
        if (list ?? List) parts.Add("view=list");
        if (Categories.Count < AllCategories.Length)
        {
            parts.Add("filtered=true");
            parts.AddRange(Categories.Select(c => $"cat={c}"));
        }
        return "?" + string.Join("&", parts);
    }

    public static DateTime ParseMonth(string? month) =>
        DateTime.TryParseExact(month, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var m) ? m : new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

    /// <summary>Categories from the query: all unless the filter form was submitted (filtered=true).</summary>
    public static IReadOnlySet<EventCategory> ParseCategories(IEnumerable<string>? cats, bool filtered) =>
        filtered
            ? (cats ?? []).Select(c => Enum.TryParse<EventCategory>(c, true, out var e) ? e : (EventCategory?)null).OfType<EventCategory>().ToHashSet()
            : AllCategories.ToHashSet();

    public static CalendarView Load(SampleStore store, DateTime month, bool list, IReadOnlySet<EventCategory> categories)
    {
        var start = month.AddDays(-(int)month.DayOfWeek);
        var events = store.Events(start, start.AddDays(42)).Where(e => categories.Contains(e.Category)).ToList();
        return new(month, list, categories, events);
    }

    /// <summary>Time label for a chip or agenda row.</summary>
    public static string TimeLabel(CalendarEvent e) =>
        e.AllDay ? "All day" : $"{e.Start.ToString("h:mm tt", CultureInfo.InvariantCulture).ToLowerInvariant().Replace(":00", "")}";
}
