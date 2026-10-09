using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Gantt: the project timeline, read only.</summary>
[Route("apps/gantt")]
public class GanttController : SampleController
{
    [HttpGet("")]
    public IActionResult Index() => Page("Gantt", "Apps/Gantt/Gantt", GanttPage.Board(DateTime.Today));
}
