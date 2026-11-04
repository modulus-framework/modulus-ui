using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Gantt: the project timeline; bars can be moved, and a move that breaks a dependency is refused.</summary>
[Route("apps/gantt")]
public class GanttController : SampleController
{
    [HttpGet("")]
    public IActionResult Index() => Page("Gantt", "Apps/Gantt/Gantt", GanttPage.Board(DateTime.Today));

    /// <summary>The bars the page moved (a form posts the moves when a bar is dropped or moved with the keyboard).</summary>
    [HttpPost("move")]
    public IActionResult Move([FromForm] string? moves)
    {
        var applied = GanttPage.ApplyMoves(moves, DateTime.Today);
        Flash(applied > 0 ? $"Moved {applied} task{(applied == 1 ? "" : "s")}." : "That move would break a dependency, so nothing changed.", applied > 0 ? "success" : "error");
        return Redirect("/apps/gantt");
    }
}
