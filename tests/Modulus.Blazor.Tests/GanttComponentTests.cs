using Bunit;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions.Gantt;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The Blazor Gantt: a row per task, group headings, bars sized and filled, late marks and the today line.</summary>
public class GanttComponentTests : BunitContext
{
    private static readonly DateTime From = new(2026, 10, 5);
    private static readonly DateTime To = new(2026, 10, 18);
    private static readonly DateTime Today = new(2026, 10, 9);

    private static readonly GanttTask[] Tasks =
    [
        new(1, "Requirements", new DateTime(2026, 10, 5), new DateTime(2026, 10, 7), 100, "Discovery", "success", "Ada Lovelace"),
        new(2, "Frontend", new DateTime(2026, 10, 8), new DateTime(2026, 10, 14), 60, "Build", "primary"),
        new(3, "Testing", new DateTime(2026, 10, 12), new DateTime(2026, 10, 16), 0, "Build", "warning"),
    ];

    private IRenderedComponent<Gantt> Mount() =>
        Render<Gantt>(p => p.Add(x => x.Tasks, Tasks).Add(x => x.From, From).Add(x => x.To, To).Add(x => x.Today, Today));

    [Fact]
    public void DrawsARowPerTask_UnderItsGroupHeading()
    {
        var cut = Mount();

        Assert.Equal(3, cut.FindAll(".m-gantt-row").Count);
        Assert.Equal(["Discovery", "Build"], cut.FindAll(".m-gantt-group").Select(g => g.TextContent.Trim()));
        Assert.Contains("Ada Lovelace", cut.Markup);
    }

    [Fact]
    public void EachBarIsSizedToItsDays_AndFilledToItsProgress()
    {
        var cut = Mount();

        var first = cut.Find(".m-gantt-bar[aria-label^='Requirements']");
        Assert.Contains("left:0%", first.GetAttribute("style"));
        Assert.Contains("width:21.429%", first.GetAttribute("style"));
        Assert.Contains("width:100%", cut.Find(".m-gantt-bar[aria-label^='Requirements'] .m-gantt-fill").GetAttribute("style"));
        Assert.Contains("m-gantt-success", first.ClassName);
    }

    [Fact]
    public void ALateTask_IsMarked()
    {
        var late = new[] { new GanttTask(9, "Overdue", new DateTime(2026, 10, 5), new DateTime(2026, 10, 6), 10) };
        var cut = Render<Gantt>(p => p.Add(x => x.Tasks, late).Add(x => x.From, From).Add(x => x.To, To).Add(x => x.Today, Today));

        Assert.Contains("late", cut.Find(".m-gantt-bar").ClassName);
        Assert.Contains("late", cut.Find(".m-gantt-bar").GetAttribute("aria-label"));
    }

    [Fact]
    public void TodayIsDrawnInEachRow()
    {
        var cut = Mount();

        Assert.Equal(3, cut.FindAll(".m-gantt-today").Count);
    }

    [Fact]
    public void TheWeekTicks_AreInTheHeader()
    {
        var cut = Mount();

        Assert.Equal(["Oct 5", "Oct 12"], cut.FindAll(".m-gantt-tick").Select(t => t.TextContent));
    }
}
