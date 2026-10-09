using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Kanban;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>app-kanban: the board's markup, with its drag handlers and the htmx posts for moves, adds and deletes.</summary>
public class KanbanTagHelperTests
{
    private static readonly KanbanColumn[] Columns = [new("todo", "To do"), new("done", "Done")];

    private static async Task<string> RenderAsync(KanbanBoardView board)
    {
        var helper = new KanbanTagHelper { Board = board, Url = "/apps/kanban" };
        var output = new TagHelperOutput("app-kanban", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext("app-kanban", [], new Dictionary<object, object>(), "k1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task DrawsTheColumnsAndCards_WithTheRefreshAndDragHandlers()
    {
        var board = KanbanModel.Build(Columns,
            [new KanbanCard(1, "todo", 0, "Write <spec>", "UX", null, ["Ada Lovelace"])], ["UX"], "done", new DateTime(2026, 10, 9));

        var html = await RenderAsync(board);

        Assert.Equal(2, CountOf(html, "class=\"m-kanban-col\""));
        Assert.Equal(1, CountOf(html, "class=\"m-kanban-card\""));
        Assert.Contains("x-data=\"kanbanBoard('/apps/kanban/move')\"", html);
        Assert.Contains("hx-trigger=\"kanban:refresh from:body\"", html);
        Assert.Contains("Write &lt;spec&gt;", html);
        Assert.Contains("class=\"m-avatar m-avatar-xs m-av-", html);
    }

    [Fact]
    public async Task TheCardMenu_MovesToEachOtherColumn_AndDeletesWithAConfirmation()
    {
        var board = KanbanModel.Build(Columns, [new KanbanCard(7, "todo", 0, "Ship", "UX", null, [])], ["UX"], "done", new DateTime(2026, 10, 9));

        var html = await RenderAsync(board);

        Assert.Contains("Move to Done", html);
        Assert.Contains("hx-post=\"/apps/kanban/move\"", html);
        Assert.Contains("hx-vals=\"{&quot;id&quot;:7,&quot;column&quot;:&quot;done&quot;,&quot;index&quot;:999}\"", html);
        Assert.Contains("hx-post=\"/apps/kanban/delete/7\"", html);
        Assert.Contains("hx-confirm=\"Delete &quot;Ship&quot;?\"", html);
    }

    [Fact]
    public async Task TheAddForm_PostsTheColumnToAdd()
    {
        var html = await RenderAsync(KanbanModel.Build(Columns, [], ["UX", "Code review"], "done", DateTime.Today));

        Assert.Equal(2, CountOf(html, "hx-post=\"/apps/kanban/add\""));
        Assert.Contains("<input type=\"hidden\" name=\"Column\" value=\"todo\" />", html);
        Assert.Contains("<option value=\"Code review\">Code review</option>", html);
    }

    private static int CountOf(string text, string needle) => (text.Length - text.Replace(needle, "", StringComparison.Ordinal).Length) / needle.Length;
}
