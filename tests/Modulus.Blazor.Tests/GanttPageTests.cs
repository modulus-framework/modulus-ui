using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Gantt;
using Xunit;

namespace Modulus.Blazor.Tests;

public class GanttPageTests
{
    // Monday 2 November 2026 is the timeline's day 0 for this "today".
    private static readonly DateTime Today = new(2026, 11, 4);
    private static readonly DateTime Mon = new(2026, 11, 2);

    [Fact]
    public void Move_refuses_a_task_that_starts_before_its_dependency_ends()
    {
        // Requirements (2) ends on day 4 and Design review (3) depends on it, so 2 may not slide to day 2.
        Assert.False(GanttPage.Move(new GanttMove(2, Mon.AddDays(2), Mon.AddDays(6)), Today));
    }

    [Fact]
    public void Move_refuses_a_predecessor_that_would_end_after_its_dependent_starts()
    {
        // Design review (3) depends on Requirements (2): sliding 3 to day 3 starts it before 2 ends.
        Assert.False(GanttPage.Move(new GanttMove(3, Mon.AddDays(3), Mon.AddDays(5)), Today));
    }

    [Fact]
    public void Move_accepts_a_task_nothing_depends_on()
    {
        // Launch day (9) has no dependents, so moving it a day later is fine.
        Assert.True(GanttPage.Move(new GanttMove(9, Mon.AddDays(43), Mon.AddDays(43)), Today));
        var launch = Assert.Single(GanttPage.Tasks(Today), t => t.Id == 9);
        Assert.Equal(Mon.AddDays(43), launch.Start);
    }

    [Fact]
    public void Move_of_an_unknown_task_is_refused()
    {
        Assert.False(GanttPage.Move(new GanttMove(999, Mon, Mon), Today));
    }
}
