using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Kanban;

/// <summary>Kanban board: same URLs as the MVC sample (/add, /move, /delete/{id}).</summary>
public class KanbanModel(SampleStore store) : SamplePageModel
{
    public const string RefreshEvent = "kanban:refresh";

    public IReadOnlyList<KanbanTask> Tasks { get; private set; } = [];

    public void OnGet() => Tasks = store.Tasks();

    public IActionResult OnPostAdd(KanbanTaskInput input)
    {
        if (!ModelState.IsValid)
        {
            Response.HxMessage(MessageType.Error, "Give the task a title");
            return Request.IsHtmx() ? new UnprocessableEntityResult() : Redirect("~/apps/kanban");
        }
        store.AddTask(input);
        Response.HxMessage(MessageType.Success, "Task added").HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/kanban");
    }

    public IActionResult OnPostMove([FromForm] int id, [FromForm] string column, [FromForm] int index)
    {
        if (!store.MoveTask(id, column, index)) return NotFound();
        Response.HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/kanban");
    }

    public IActionResult OnPostDelete(int id)
    {
        store.DeleteTask(id);
        Response.HxMessage(MessageType.Success, "Task deleted").HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/kanban");
    }
}
