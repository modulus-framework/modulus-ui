using Modulus.Ui.Abstractions.Gantt;
using Xunit;

namespace Modulus.Blazor.Tests;

public class GanttDependencyTests
{
    private static readonly DateTime Mon = new(2026, 11, 2);
    private static GanttTask T(int id, string name, int startDay, int endDay, params int[] after) => new(id, name, Mon.AddDays(startDay), Mon.AddDays(endDay), 0, "Build", "primary", null, after.Length == 0 ? null : after);

    [Fact]
    public void Dependencies_BecomeLinks_BetweenTheEndAndTheStart()
    {
        var view = GanttModel.Build([T(1, "Design", 0, 4), T(2, "Build", 5, 9, 1)], Mon, Mon.AddDays(13), Mon);

        var link = Assert.Single(view.Links!);
        Assert.Equal((1, 2), (link.From, link.To));
        Assert.False(link.Conflict);
        Assert.True(link.X1Pct <= link.X2Pct); // back to back: the arrow is a short step
        Assert.Equal(view.Rows[0].Slot, (int)link.Slot1);
        Assert.Equal(view.Rows[1].Slot, (int)link.Slot2);
    }

    [Fact]
    public void AStartOnOrBeforeTheEnd_IsAConflict_AndIsNamed()
    {
        var tasks = new[] { T(1, "Design", 0, 4), T(2, "Build", 4, 9, 1) };
        var view = GanttModel.Build(tasks, Mon, Mon.AddDays(13), Mon);

        Assert.True(Assert.Single(view.Links!).Conflict);
        Assert.Equal(["Build starts on or before Design ends."], GanttModel.Conflicts(tasks));
    }

    [Fact]
    public void GroupHeadings_TakeSlots_SoRowsStayAligned()
    {
        var tasks = new[] { new GanttTask(1, "A", Mon, Mon, 0, "One"), new GanttTask(2, "B", Mon, Mon, 0, "Two") };
        var view = GanttModel.Build(tasks, Mon, Mon.AddDays(6), Mon);

        // Heading, row, heading, row.
        Assert.Equal([1, 3], view.Rows.Select(r => r.Slot));
        Assert.Equal(4, view.Slots);
        Assert.Equal("0 0 100 208", GanttDrawing.ViewBox(view));
    }

    [Fact]
    public void Links_OutsideTheRange_AreLeftOut()
    {
        var view = GanttModel.Build([T(1, "Early", 0, 1), T(2, "Later", 20, 25, 1)], Mon.AddDays(10), Mon.AddDays(30), Mon);
        Assert.Empty(view.Links!);
    }

    [Fact]
    public void Shift_MovesBothDates_AndKeepsTheLength()
    {
        var moved = GanttModel.Shift(T(1, "A", 2, 6), 3);
        Assert.Equal(Mon.AddDays(5), moved.Start);
        Assert.Equal(Mon.AddDays(9), moved.End);
    }

    [Fact]
    public void LinkPath_RunsOutOfTheEarlierEndAndIntoTheLaterStart()
    {
        var path = GanttDrawing.LinkPath(new GanttLink(1, 2, 40, 1, 60, 3, false));
        Assert.Equal("M40 78 H50 V182 H60", path);
    }

    [Fact]
    public void Moves_ParseFromTheEditor_AndSkipBadEntries()
    {
        var moves = GanttDrawing.ParseMoves("[{\"id\":4,\"start\":\"2026-11-09\",\"end\":\"2026-11-20\"},{\"id\":\"x\"},{\"id\":5,\"start\":\"2026-11-20\",\"end\":\"2026-11-01\"}]");
        var move = Assert.Single(moves);
        Assert.Equal(4, move.Id);
        Assert.Equal(new DateTime(2026, 11, 9), move.Start);
        Assert.Empty(GanttDrawing.ParseMoves("not json"));
        Assert.Empty(GanttDrawing.ParseMoves(null));
    }
}
