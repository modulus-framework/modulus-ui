using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Approvals: a purchase order's approval steps and audit trail. The filter is a link, so the page reloads with it.</summary>
[Route("apps/workflow")]
public class WorkflowController : SampleController
{
    [HttpGet("")]
    // The query's "action" is bound explicitly: a bare "action" parameter would take the route value (the action's name).
    public IActionResult Index([FromQuery(Name = "action")] string? filter) => Page("Approvals", "Apps/Workflow/Workflow", WorkflowPage.Board(DateTime.Today, filter));
}
