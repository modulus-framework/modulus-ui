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

    [HttpPost("reset")]
    public IActionResult Reset()
    {
        FlowDemo.Reset();
        Flash("Back to the sample workflow.");
        return Redirect("/apps/flow");
    }
}
