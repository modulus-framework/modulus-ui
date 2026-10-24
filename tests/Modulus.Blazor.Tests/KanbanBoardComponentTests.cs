using AngleSharp.Dom;
using AntDesign;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions.Kanban;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The Blazor KanbanBoard: its cards show, a drag moves a card, and the add form reports what it adds.</summary>
public class KanbanBoardComponentTests : AsyncBunitContext
{
    private static readonly KanbanColumn[] Columns = [new("todo", "To do"), new("progress", "In progress"), new("done", "Done")];
    private static readonly string[] Labels = ["UX", "Code review"];

    private static readonly KanbanCard[] Cards =
    [
        new(1, "todo", 0, "Write the spec", "UX", null, ["Ada Lovelace"]),
        new(2, "todo", 1, "Review the PR", "Code review", null, []),
        new(3, "done", 0, "Ship it", "UX", null, []),
    ];

    public KanbanBoardComponentTests()
    {
        // The card menu's AntDesign dropdown measures itself after render; loose interop answers with empty results.
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<AntDesign.JsInterop.HtmlElement>(_ => true).SetResult(new AntDesign.JsInterop.HtmlElement());
        JSInterop.Setup<AntDesign.JsInterop.DomRect>(_ => true).SetResult(new AntDesign.JsInterop.DomRect());
        Services.AddAntDesign();
    }

    private IRenderedComponent<KanbanBoard> Mount(Func<KanbanMove, Task>? onMove = null, Func<KanbanNewCard, Task>? onAdd = null) =>
        Render<KanbanBoard>(p => p
            .Add(x => x.Columns, Columns)
            .Add(x => x.Cards, Cards)
            .Add(x => x.Labels, Labels)
            .Add(x => x.OnMove, EventCallback.Factory.Create(this, onMove ?? (_ => Task.CompletedTask)))
            .Add(x => x.OnAdd, EventCallback.Factory.Create(this, onAdd ?? (_ => Task.CompletedTask))));

    private static IElement Column(IRenderedComponent<KanbanBoard> cut, string title) =>
        cut.FindAll("section").Single(s => s.GetAttribute("aria-label") == title);

    [Fact]
    public void DrawsEachColumnWithItsCards()
    {
        var cut = Mount();

        Assert.Equal(3, cut.FindAll("section").Count);
        Assert.Equal(2, Column(cut, "To do").QuerySelectorAll(".m-kanban-card").Length);
        Assert.Equal(1, Column(cut, "Done").QuerySelectorAll(".m-kanban-card").Length);
        Assert.Contains("Write the spec", cut.Markup);
    }

    [Fact]
    public void DraggingACardIntoAColumn_MovesItToTheEnd()
    {
        KanbanMove? moved = null;
        var cut = Mount(onMove: m =>
        {
            moved = m;
            return Task.CompletedTask;
        });

        cut.Find("article[data-id='1']").TriggerEvent("ondragstart", new DragEventArgs());
        Column(cut, "In progress").TriggerEvent("ondrop", new DragEventArgs());

        Assert.Equal(new KanbanMove(1, "progress", 0), moved);
    }

    [Fact]
    public void AnAddedTask_ReportsItsColumnTitleAndLabel()
    {
        KanbanNewCard? added = null;
        var cut = Mount(onAdd: c =>
        {
            added = c;
            return Task.CompletedTask;
        });

        Column(cut, "To do").QuerySelector(".m-kanban-add button.ant-btn-text")!.Click();
        Column(cut, "To do").QuerySelector("input")!.Change("  Plan the sprint ");
        Column(cut, "To do").QuerySelector("select")!.Change("Code review");
        Column(cut, "To do").QuerySelector("form")!.TriggerEvent("onsubmit", new EventArgs());

        Assert.Equal(new KanbanNewCard("todo", "Plan the sprint", "Code review"), added);
    }

    [Fact]
    public void ABlankTitle_IsRefusedWithAMessage_AndNothingIsAdded()
    {
        var added = false;
        var cut = Mount(onAdd: _ =>
        {
            added = true;
            return Task.CompletedTask;
        });

        Column(cut, "To do").QuerySelector(".m-kanban-add button.ant-btn-text")!.Click();
        Column(cut, "To do").QuerySelector("form")!.TriggerEvent("onsubmit", new EventArgs());

        Assert.False(added);
        Assert.Contains("Give the task a title", cut.Markup);
    }
}
