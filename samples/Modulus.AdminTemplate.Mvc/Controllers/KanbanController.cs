using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Kanban board: add, move (drag and drop or menu) and delete post over htmx and fire kanban:refresh.</summary>
[Route("apps/kanban")]
public class KanbanController(SampleStore store) : SampleController
{
    public const string RefreshEvent = "kanban:refresh";

    [HttpGet("")]
    public IActionResult Index() => Page("Kanban", "Apps/Kanban/Kanban", KanbanPage.Board(store.Tasks()));

    [HttpPost("add")]
    public IActionResult Add(KanbanTaskInput input)
    {
        if (!ModelState.IsValid)
        {
            Response.HxMessage(MessageType.Error, "Give the task a title");
            return Request.IsHtmx() ? UnprocessableEntity() : Redirect("~/apps/kanban");
        }
        store.AddTask(input);
        Response.HxMessage(MessageType.Success, "Task added").HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/kanban");
    }

    [HttpPost("move")]
    public IActionResult Move([FromForm] int id, [FromForm] string column, [FromForm] int index)
    {
        if (!store.MoveTask(id, column, index)) return NotFound();
        Response.HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/kanban");
    }

    [HttpPost("delete/{id:int}")]
    public IActionResult Delete(int id)
    {
        store.DeleteTask(id);
        Response.HxMessage(MessageType.Success, "Task deleted").HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/kanban");
    }
}
