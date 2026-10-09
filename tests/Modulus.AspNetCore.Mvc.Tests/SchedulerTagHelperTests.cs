using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Scheduler;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>app-scheduler: the month grid or agenda, its boosted links and filter form, and the refresh that a save fires.</summary>
public class SchedulerTagHelperTests
{
    private static readonly SchedulerCategory[] Categories = [new("Business", "red"), new("Holiday", "green")];

    private static async Task<string> RenderAsync(SchedulerView view)
    {
        var helper = new SchedulerTagHelper { View = view, Url = "/apps/calendar" };
        var output = new TagHelperOutput("app-scheduler", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext("app-scheduler", [], new Dictionary<object, object>(), "s1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private static SchedulerView View(bool list = false, params SchedulerEvent[] events) =>
        SchedulerView.Build(SchedulerModel.Parse("2026-10", list ? "list" : null, null, false, Categories, new DateTime(2026, 10, 9, 10, 0, 0)),
            Categories, events, new DateTime(2026, 10, 9, 10, 0, 0));

    [Fact]
    public async Task DrawsTheGrid_WithTheRefreshAndTheDrawerLinks()
    {
        var html = await RenderAsync(View(events: new SchedulerEvent(1, "Design <review>", new DateTime(2026, 10, 5, 9, 0, 0), new DateTime(2026, 10, 5, 10, 0, 0), false, "Business")));

        Assert.Equal(42, CountOf(html, "class=\"m-cal-cell"));
        Assert.Contains("class=\"m-cal-cell today\"", html);
        Assert.Contains("hx-trigger=\"scheduler:refresh from:body\"", html);
        Assert.Contains("hx-get=\"/apps/calendar?month=2026-10\"", html);
        Assert.Contains("href=\"/apps/calendar?month=2026-11\"", html);
        Assert.Contains("$store.drawer.load('/apps/calendar/form/1', 'Edit event')", html);
        Assert.Contains("Design &lt;review&gt;", html);
        Assert.DoesNotContain("<review>", html);
    }

    [Fact]
    public async Task TheAgenda_ListsTheDaysWithEvents_AndMarksTheListView()
    {
        var html = await RenderAsync(View(list: true, new SchedulerEvent(2, "Off", new DateTime(2026, 10, 12), new DateTime(2026, 10, 12, 23, 0, 0), true, "Holiday")));

        Assert.Contains("hx-get=\"/apps/calendar?month=2026-10&amp;view=list", html);
        Assert.Equal(1, CountOf(html, "class=\"m-cal-agenda-day"));
        Assert.DoesNotContain("class=\"m-cal-grid\"", html);
        Assert.Contains("aria-current=\"page\"", html);
    }

    [Fact]
    public async Task TheFilter_ChecksEachCategoryThatShows()
    {
        var view = SchedulerView.Build(SchedulerModel.Parse("2026-10", null, ["Business"], true, Categories, new DateTime(2026, 10, 9)), Categories, [], new DateTime(2026, 10, 9));

        var html = await RenderAsync(view);

        Assert.Contains("name=\"cat\" value=\"Business\" checked", html);
        Assert.DoesNotContain("name=\"cat\" value=\"Holiday\" checked", html);
    }

    private static int CountOf(string text, string needle) => (text.Length - text.Replace(needle, "", StringComparison.Ordinal).Length) / needle.Length;
}
