using Modulus.Ui.Abstractions.Gantt;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The timeline's model: bars placed on the range's days, groups, clipping, progress, late tasks, ticks and today.</summary>
public class GanttModelTests
{
    // A two-week range from Monday 5 October 2026 to Sunday 18 October.
    private static readonly DateTime From = new(2026, 10, 5);
    private static readonly DateTime To = new(2026, 10, 18);
    private static readonly DateTime Today = new(2026, 10, 9);

    private static GanttTask Task(int id, string name, DateTime start, DateTime end, int progress = 0, string? group = null) =>
        new(id, name, start, end, progress, group);

    [Fact]
    public void ABarIsPlacedOnTheDaysItRuns_AsPercentagesOfTheRange()
    {
        var view = GanttModel.Build([Task(1, "Design", new DateTime(2026, 10, 5), new DateTime(2026, 10, 11))], From, To, Today);

        var row = Assert.Single(view.Rows);
        Assert.Equal(0, row.LeftPct);
        Assert.Equal(50, row.WidthPct, 6);
        Assert.False(row.ClippedStart);
        Assert.False(row.ClippedEnd);
    }

    [Fact]
    public void ATaskThatRunsPastAnEdge_IsClippedAtTheRange_AndMarkedAsSo()
    {
        var view = GanttModel.Build([Task(1, "Long", new DateTime(2026, 9, 28), new DateTime(2026, 10, 7))], From, To, Today);

        var row = Assert.Single(view.Rows);
        Assert.Equal(0, row.LeftPct);
        Assert.Equal(3.0 / 14 * 100, row.WidthPct, 6);
        Assert.True(row.ClippedStart);
        Assert.False(row.ClippedEnd);
    }

    [Fact]
    public void TasksOutsideTheRange_AreLeftOut()
    {
        var view = GanttModel.Build(
        [
            Task(1, "Before", new DateTime(2026, 9, 1), new DateTime(2026, 9, 30)),
            Task(2, "After", new DateTime(2026, 11, 1), new DateTime(2026, 11, 5)),
        ], From, To, Today);

        Assert.Empty(view.Rows);
    }

    [Fact]
    public void Groups_KeepTheOrderTheirFirstTaskAppears_AndOpenOnTheirFirstRow()
    {
        var view = GanttModel.Build(
        [
            Task(1, "Ship", new DateTime(2026, 10, 8), new DateTime(2026, 10, 8), group: "Launch"),
            Task(2, "Plan", new DateTime(2026, 10, 5), new DateTime(2026, 10, 6), group: "Discovery"),
            Task(3, "Build", new DateTime(2026, 10, 7), new DateTime(2026, 10, 9), group: "Discovery"),
        ], From, To, Today);

        Assert.Equal([1, 2, 3], view.Rows.Select(r => r.Task.Id));
        Assert.Equal([true, true, false], view.Rows.Select(r => r.NewGroup));
    }

    [Fact]
    public void Progress_IsKeptBetween0And100()
    {
        var view = GanttModel.Build([Task(1, "Over", From, From, 150), Task(2, "Under", From, From, -5)], From, To, Today);

        Assert.Equal(100, view.Rows[0].Progress);
        Assert.Equal(0, view.Rows[1].Progress);
    }

    [Fact]
    public void ATaskThatEndedBeforeToday_IsLateUnlessDone()
    {
        var view = GanttModel.Build(
        [
            Task(1, "Overdue", new DateTime(2026, 10, 5), new DateTime(2026, 10, 7), 40),
            Task(2, "Finished", new DateTime(2026, 10, 5), new DateTime(2026, 10, 7), 100),
            Task(3, "Due today", new DateTime(2026, 10, 9), new DateTime(2026, 10, 9), 0),
        ], From, To, Today);

        Assert.Equal([true, false, false], view.Rows.Select(r => r.Late));
    }

    [Fact]
    public void Ticks_FallOnTheMondaysOfTheRange()
    {
        var view = GanttModel.Build([], From, To, Today);

        Assert.Equal(["Oct 5", "Oct 12"], view.Ticks.Select(t => t.Label));
        Assert.Equal([0.0, 50.0], view.Ticks.Select(t => t.LeftPct));
    }

    [Fact]
    public void Today_IsDrawnInTheMiddleOfItsDay_OrNotAtAllOutsideTheRange()
    {
        var inside = GanttModel.Build([], From, To, Today);
        var outside = GanttModel.Build([], From, To, new DateTime(2026, 12, 1));

        Assert.Equal(4.0 / 14 * 100 + 50.0 / 14, inside.TodayPct!.Value, 6);
        Assert.Null(outside.TodayPct);
    }

    [Fact]
    public void AnEmptyOrTooLongRange_IsRefused()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GanttModel.Build([], To, From, Today));
        Assert.Throws<ArgumentOutOfRangeException>(() => GanttModel.Build([], From, From.AddDays(GanttModel.MaxDays), Today));
    }

    [Fact]
    public void ATaskThatEndsBeforeItStarts_IsRefused()
    {
        var ex = Assert.Throws<ArgumentException>(() => GanttModel.Build([Task(1, "Backwards", To, From)], From, To, Today));

        Assert.Contains("Backwards", ex.Message);
    }
}
