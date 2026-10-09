using Modulus.Ui.Abstractions.Scheduler;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The scheduler's model: the query from the URL, the six-week grid, the events it keeps, and the agenda and upcoming lists.</summary>
public class SchedulerModelTests
{
    private static readonly SchedulerCategory[] Categories = [new("Business", "red"), new("Personal", "blue"), new("Holiday", "green")];
    private static readonly DateTime Now = new(2026, 10, 9, 10, 0, 0);

    private static SchedulerEvent Event(int id, string title, DateTime start, string category = "Business", bool allDay = false, DateTime? end = null) =>
        new(id, title, start, end ?? start.AddHours(1), allDay, category);

    [Fact]
    public void Parse_ReadsTheMonthAndView_AndFallsBackToThisMonth()
    {
        var query = SchedulerModel.Parse("2026-03", "list", null, false, Categories, Now);
        Assert.Equal(new DateTime(2026, 3, 1), query.Month);
        Assert.True(query.List);

        var fallback = SchedulerModel.Parse("March", null, null, false, Categories, Now);
        Assert.Equal(new DateTime(2026, 10, 1), fallback.Month);
        Assert.False(fallback.List);
    }

    [Fact]
    public void Parse_ShowsEveryCategory_UnlessTheFilterWasSubmitted_AndMatchesNamesIgnoringCase()
    {
        Assert.Equal(3, SchedulerModel.Parse(null, null, ["Business"], false, Categories, Now).Categories.Count);

        var filtered = SchedulerModel.Parse(null, null, ["personal", "nope"], true, Categories, Now);

        Assert.Equal(["Personal"], filtered.Categories);
    }

    [Fact]
    public void Grid_IsSixWeeksFromTheSundayOnOrBeforeTheFirst()
    {
        var view = SchedulerView.Build(SchedulerModel.Parse("2026-10", null, null, false, Categories, Now), Categories, [], Now);

        var days = view.Days.ToList();
        Assert.Equal(42, days.Count);
        Assert.Equal(DayOfWeek.Sunday, days[0].DayOfWeek);
        Assert.Contains(new DateTime(2026, 10, 1), days);
        Assert.Equal(new DateTime(2026, 10, 9), view.Today);
    }

    [Fact]
    public void Build_KeepsOnlyTheEventsInTheGridAndInTheChosenCategories()
    {
        var query = SchedulerModel.Parse("2026-10", null, ["Business"], true, Categories, Now);
        var events = new[]
        {
            Event(1, "In the grid", new DateTime(2026, 10, 5, 9, 0, 0)),
            Event(2, "Long run in", new DateTime(2026, 9, 20), end: new DateTime(2026, 9, 28)),
            Event(3, "Last month", new DateTime(2026, 8, 1)),
            Event(4, "Other category", new DateTime(2026, 10, 5, 9, 0, 0), category: "Personal"),
        };

        var view = SchedulerView.Build(query, Categories, events, Now);

        Assert.Equal([1, 2], view.Events.Select(e => e.Id).OrderBy(id => id));
    }

    [Fact]
    public void On_ListsAllDayEventsFirst_ThenByStart()
    {
        var day = new DateTime(2026, 10, 5);
        var view = SchedulerView.Build(SchedulerModel.Parse("2026-10", null, null, false, Categories, Now), Categories,
        [
            Event(1, "Standup", day.AddHours(10)),
            Event(2, "Holiday", day, allDay: true, category: "Holiday"),
            Event(3, "Early", day.AddHours(8)),
        ], Now);

        Assert.Equal(["Holiday", "Early", "Standup"], view.On(day).Select(e => e.Title));
    }

    [Fact]
    public void Agenda_HasOnlyTheDaysThatHaveEvents_AndUpcomingIsWhatHasNotEnded()
    {
        var view = SchedulerView.Build(SchedulerModel.Parse("2026-10", "list", null, false, Categories, Now), Categories,
        [
            Event(1, "Past", new DateTime(2026, 10, 2, 9, 0, 0)),
            Event(2, "Today", new DateTime(2026, 10, 9, 15, 0, 0)),
            Event(3, "Later", new DateTime(2026, 10, 20, 9, 0, 0)),
        ], Now);

        Assert.Equal([new DateTime(2026, 10, 2), new DateTime(2026, 10, 9), new DateTime(2026, 10, 20)], view.Agenda.Select(a => a.Day));
        Assert.Equal(["Today", "Later"], view.Upcoming().Select(e => e.Title));
    }

    [Fact]
    public void QueryString_CarriesTheMonthTheViewAndTheFilter_InAStableOrder()
    {
        var all = Categories.Select(c => c.Name).ToList();

        Assert.Equal("?month=2026-10", SchedulerModel.QueryString(new SchedulerQuery(new DateTime(2026, 10, 1), false, new HashSet<string>(all)), all));
        Assert.Equal("?month=2026-11&view=list&filtered=true&cat=Business&cat=Holiday",
            SchedulerModel.QueryString(new SchedulerQuery(new DateTime(2026, 11, 1), true, new HashSet<string> { "Holiday", "Business" }), all));
    }

    [Theory]
    [InlineData(true, 9, 0, "All day")]
    [InlineData(false, 9, 0, "9 am")]
    [InlineData(false, 14, 30, "2:30 pm")]
    public void TimeLabel_IsAllDayOrTheStartTime(bool allDay, int hour, int minute, string expected) =>
        Assert.Equal(expected, SchedulerModel.TimeLabel(Event(1, "x", new DateTime(2026, 10, 5, hour, minute, 0), allDay: allDay)));
}
