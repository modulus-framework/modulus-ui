using AngleSharp.Dom;
using AntDesign;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions.Scheduler;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The Blazor Scheduler: the grid and agenda draw, and its month, view, filter, day and event clicks report what was asked.</summary>
public class SchedulerComponentTests : AsyncBunitContext
{
    private static readonly SchedulerCategory[] Categories = [new("Business", "red"), new("Holiday", "green")];
    private static readonly DateTime Now = new(2026, 10, 9, 10, 0, 0);

    private static readonly SchedulerEvent Standup = new(1, "Standup", new DateTime(2026, 10, 9, 9, 0, 0), new DateTime(2026, 10, 9, 9, 30, 0), false, "Business");

    public SchedulerComponentTests()
    {
        // The AntDesign checkbox measures itself after render; loose interop answers with empty results.
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<AntDesign.JsInterop.HtmlElement>(_ => true).SetResult(new AntDesign.JsInterop.HtmlElement());
        JSInterop.Setup<AntDesign.JsInterop.DomRect>(_ => true).SetResult(new AntDesign.JsInterop.DomRect());
        Services.AddAntDesign();
    }

    private IRenderedComponent<Scheduler> Mount(string? month = "2026-10", bool list = false, Func<SchedulerQuery, Task>? onQuery = null,
        Func<DateTime, Task>? onAdd = null, Func<SchedulerEvent, Task>? onEdit = null)
    {
        var query = SchedulerModel.Parse(month, list ? "list" : null, null, false, Categories, Now);
        var view = SchedulerView.Build(query, Categories, [Standup], Now);
        return Render<Scheduler>(p => p
            .Add(x => x.View, view)
            .Add(x => x.OnQuery, EventCallback.Factory.Create(this, onQuery ?? (_ => Task.CompletedTask)))
            .Add(x => x.OnAdd, EventCallback.Factory.Create(this, onAdd ?? (_ => Task.CompletedTask)))
            .Add(x => x.OnEdit, EventCallback.Factory.Create(this, onEdit ?? (_ => Task.CompletedTask))));
    }

    [Fact]
    public void DrawsTheSixWeekGrid_WithTheMonthEventsAndTheTodayMarked()
    {
        var cut = Mount();

        Assert.Equal(42, cut.FindAll(".m-cal-cell").Count);
        Assert.Single(cut.FindAll(".m-cal-cell.today"));
        Assert.Equal("October 2026", cut.Find(".m-cal-title").TextContent);
        Assert.Contains("Standup", cut.Markup);
    }

    [Fact]
    public void NextMonth_AsksForTheFollowingMonth()
    {
        SchedulerQuery? asked = null;
        var cut = Mount(onQuery: q =>
        {
            asked = q;
            return Task.CompletedTask;
        });

        cut.Find("button[aria-label='Next month']").Click();

        Assert.Equal(new DateTime(2026, 11, 1), asked!.Month);
        Assert.False(asked.List);
    }

    [Fact]
    public void TheAgendaButton_AsksForTheListView()
    {
        SchedulerQuery? asked = null;
        var cut = Mount(onQuery: q =>
        {
            asked = q;
            return Task.CompletedTask;
        });

        cut.FindAll(".ant-segmented-item")[1].Click();

        Assert.True(asked!.List);
        Assert.Equal(new DateTime(2026, 10, 1), asked.Month);
    }

    [Fact]
    public void TheAgendaView_ListsTheDayWithItsEvents()
    {
        var cut = Mount(list: true);

        Assert.Single(cut.FindAll(".m-cal-agenda-day"));
        Assert.Empty(cut.FindAll(".m-cal-grid"));
    }

    [Fact]
    public void ClickingADay_AsksToAddOnThatDay()
    {
        DateTime? added = null;
        var cut = Mount(onAdd: d =>
        {
            added = d;
            return Task.CompletedTask;
        });

        cut.Find("button[aria-label='Add event on October 14']").Click();

        Assert.Equal(new DateTime(2026, 10, 14), added);
    }

    [Fact]
    public void ClickingAnEvent_AsksToEditIt()
    {
        SchedulerEvent? edited = null;
        var cut = Mount(onEdit: e =>
        {
            edited = e;
            return Task.CompletedTask;
        });

        cut.Find(".m-cal-event").Click();

        Assert.Equal(Standup, edited);
    }

    [Fact]
    public void Unchecking_ACategory_AsksForTheQueryWithoutIt()
    {
        SchedulerQuery? asked = null;
        var cut = Mount(onQuery: q =>
        {
            asked = q;
            return Task.CompletedTask;
        });

        cut.FindAll("input.ant-checkbox-input")[0].Change(false);

        Assert.NotNull(asked);
        Assert.Equal(new[] { "Holiday" }, asked!.Categories);
    }
}
