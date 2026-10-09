using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Kanban;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// A kanban board (<see cref="KanbanBoardView"/>), the MVC front end of the Blazor <c>KanbanBoard</c>:
/// <code>&lt;app-kanban board="@Model" url="/apps/kanban" /&gt;</code>
/// Cards drag between columns (Alpine's <c>kanbanBoard</c>) and their menus post over htmx to <c>{url}/move</c>,
/// <c>{url}/delete/{id}</c> and <c>{url}/add</c>; each change fires <c>kanban:refresh</c>, which reloads the board
/// (the controller answers with the page, and htmx takes <c>#kanban-board</c> from it).
/// </summary>
[HtmlTargetElement("app-kanban", TagStructure = TagStructure.WithoutEndTag)]
public class KanbanTagHelper : TagHelper
{
    /// <summary>The board to draw.</summary>
    public KanbanBoardView? Board { get; set; }

    /// <summary>The page the board posts to (its moves, adds and deletes are under this path).</summary>
    public string Url { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var board = Board ?? throw new InvalidOperationException("app-kanban needs a board.");
        var url = Html.E(Url);
        var sb = new StringBuilder();
        sb.Append($"""<div id="kanban-board" class="m-kanban" x-data="kanbanBoard('{url}/move')">""");
        sb.Append($"""<div hidden hx-get="{url}" hx-trigger="kanban:refresh from:body" hx-select="#kanban-board" hx-target="#kanban-board" hx-swap="outerHTML"></div>""");
        foreach (var column in board.Columns) Column(sb, board, column, url);
        sb.Append("</div>");
        output.TagName = null;
        output.Content.SetHtmlContent(sb.ToString());
    }

    private static void Column(StringBuilder sb, KanbanBoardView board, KanbanColumnView view, string url)
    {
        var id = Html.E(view.Column.Id);
        sb.Append($"""<section class="m-kanban-col" aria-label="{Html.E(view.Column.Title)}" :class="over === '{id}' && 'drop'" @dragover.prevent="over = '{id}'" @dragleave.self="over = null" @drop.prevent="drop($event, '{id}')">""");
        sb.Append($"""<div class="m-kanban-head"><h3>{Html.E(view.Column.Title)}</h3><span class="m-badge m-badge-gray-lt">{view.Cards.Count}</span></div>""");
        sb.Append("""<div class="m-kanban-list">""");
        foreach (var card in view.Cards) Card(sb, board, view.Column, card, url);
        sb.Append("</div>");
        Add(sb, view.Column, board.Labels, url);
        sb.Append("</section>");
    }

    private static void Card(StringBuilder sb, KanbanBoardView board, KanbanColumn column, KanbanCardView view, string url)
    {
        var card = view.Card;
        var id = card.Id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var title = Html.E(card.Title);
        sb.Append($"""<article class="m-kanban-card" draggable="true" data-id="{id}" @dragstart="start($event, {id})" @dragend="end($event)">""");
        sb.Append("""<div class="m-kanban-card-top">""");
        sb.Append($"""<span class="m-badge m-badge-{view.LabelTone}-lt">{Html.E(card.Label)}</span>""");
        sb.Append(DropdownTagHelper.Render(Trigger(card.Title), Menu(board, column, card, url), "bottomRight"));
        sb.Append("</div>");
        sb.Append($"""<div class="m-kanban-card-title">{title}</div>""");
        sb.Append("""<div class="m-kanban-meta">""");
        if (view.DueText.Length > 0)
            sb.Append($"""<span class="m-kanban-due{(view.Late ? " late" : "")}" title="{(view.Late ? "Overdue" : "Due")}">{ShellRenderer.Icon("calendar")}{Html.E(view.DueText)}</span>""");
        if (card.Comments > 0) sb.Append($"""<span title="Comments">{ShellRenderer.Icon("message")}{card.Comments}</span>""");
        if (card.Attachments > 0) sb.Append($"""<span title="Attachments">{ShellRenderer.Icon("paper-clip")}{card.Attachments}</span>""");
        sb.Append("""<span class="m-avatar-list">""");
        foreach (var who in view.Avatars)
            sb.Append($"""<span class="m-avatar m-avatar-xs m-av-{who.Tint}" title="{Html.E(who.Name)}">{Html.E(who.Initials)}</span>""");
        sb.Append("</span></div></article>");
    }

    private static string Trigger(string title) =>
        $"""<button type="button" class="ant-btn ant-btn-text ant-btn-sm ant-btn-icon-only" aria-label="Actions for {Html.E(title)}" title="Actions">{ShellRenderer.Icon("ellipsis")}</button>""";

    private static string Menu(KanbanBoardView board, KanbanColumn column, KanbanCard card, string url)
    {
        var sb = new StringBuilder();
        foreach (var target in board.Columns.Select(c => c.Column).Where(c => c.Id != column.Id))
        {
            var vals = JsonSerializer.Serialize(new { id = card.Id, column = target.Id, index = 999 });
            sb.Append(DropdownTagHelper.Item(Html.E($"Move to {target.Title}"), attributes: $" hx-post=\"{url}/move\" hx-vals=\"{Html.E(vals)}\" hx-swap=\"none\""));
        }
        var confirm = Html.E($"Delete \"{card.Title}\"?");
        sb.Append($"""<li role="none" class="ant-dropdown-menu-item ant-dropdown-menu-item-danger"><button type="button" role="menuitem" hx-post="{url}/delete/{card.Id.ToString(System.Globalization.CultureInfo.InvariantCulture)}" hx-swap="none" hx-confirm="{confirm}" data-confirm-danger data-confirm-ok="Delete"><span class="ant-dropdown-menu-title-content">Delete</span></button></li>""");
        return sb.ToString();
    }

    private static void Add(StringBuilder sb, KanbanColumn column, IReadOnlyList<string> labels, string url)
    {
        sb.Append("""<div class="m-kanban-add" x-data="{ open: false }">""");
        sb.Append($"""<button type="button" class="ant-btn ant-btn-text ant-btn-block" x-show="!open" @click="open = true; $nextTick(() => $refs.title.focus())">{ShellRenderer.Icon("plus")}<span>Add task</span></button>""");
        sb.Append($$"""<form x-show="open" x-cloak hx-post="{{url}}/add" hx-swap="none" @htmx:after-request="if (event.detail.successful) { $el.reset(); open = false }" class="m-kanban-add" style="padding:0">""");
        sb.Append($"""<input type="hidden" name="Column" value="{Html.E(column.Id)}" />""");
        sb.Append($"""<input class="ant-input" name="Title" x-ref="title" placeholder="Task title" aria-label="Task title in {Html.E(column.Title)}" required maxlength="{KanbanModel.MaxTitleLength}" @keydown.escape="open = false" />""");
        sb.Append("""<select class="m-select" name="Label" aria-label="Label">""");
        foreach (var label in labels) sb.Append($"""<option value="{Html.E(label)}">{Html.E(label)}</option>""");
        sb.Append("</select>");
        sb.Append("""<div class="m-flex m-gap-8"><button type="submit" class="ant-btn ant-btn-primary ant-btn-sm"><span>Add</span></button>""");
        sb.Append("""<button type="button" class="ant-btn ant-btn-default ant-btn-sm" @click="open = false"><span>Cancel</span></button></div>""");
        sb.Append("</form></div>");
    }
}
