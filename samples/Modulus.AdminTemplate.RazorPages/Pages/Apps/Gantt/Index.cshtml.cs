using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Gantt;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Gantt;

/// <summary>Gantt: the project timeline, read only (the same page as the MVC sample).</summary>
public class GanttModel : SamplePageModel
{
    public GanttView View { get; private set; } = GanttPage.Board(DateTime.Today);

    public void OnGet() => View = GanttPage.Board(DateTime.Today);

    /// <summary>The bars the page moved (the form posts the moves when a bar is dropped or moved with the keyboard).</summary>
    public IActionResult OnPostMove([FromForm] string? moves)
    {
        var applied = GanttPage.ApplyMoves(moves, DateTime.Today);
        Flash(applied > 0 ? $"Moved {applied} task{(applied == 1 ? "" : "s")}." : "That move would break a dependency, so nothing changed.", applied > 0 ? "success" : "error");
        return Redirect("/apps/gantt");
    }
}
