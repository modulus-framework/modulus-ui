using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>The workflow designer: the editor's graph posts as JSON in one form field and is kept in memory (a real app stores it).</summary>
[Route("apps/flow")]
public class FlowController : SampleController
{
    [HttpGet("")]
    public IActionResult Index() => Page("Workflow designer", "Apps/Flow/Flow", FlowPage.Show());

    [HttpPost("")]
    public IActionResult Save([FromForm(Name = "Workflow")] string? workflow)
    {
        var saved = FlowPage.TrySave(workflow, out var message);
        Flash(message, saved ? "success" : "error");
        return Redirect("/apps/flow");
    }

    /// <summary>Runs the workflow as it is on the canvas for one customer message, and shows what each node did.</summary>
    [HttpPost("run")]
    public async Task<IActionResult> Run([FromForm(Name = "Workflow")] string? workflow, [FromForm] string? message)
    {
        var view = await FlowPage.RunAsync(workflow, message);
        if (view is null)
        {
            Flash("That is not a workflow, so nothing ran.", "error");
            return Redirect("/apps/flow");
        }
        return Page("Workflow designer", "Apps/Flow/Flow", view);
    }

    [HttpPost("reset")]
    public IActionResult Reset()
    {
        FlowDemo.Reset();
        Flash("Back to the sample workflow.");
        return Redirect("/apps/flow");
    }
}
