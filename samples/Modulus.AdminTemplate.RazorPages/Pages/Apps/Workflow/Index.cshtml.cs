using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Workflow;

/// <summary>Approvals: a purchase order's approval steps and audit trail (the same page as the MVC sample). The filter is a query.</summary>
public class WorkflowModel : SamplePageModel
{
    public WorkflowPage.Order View { get; private set; } = WorkflowPage.Board(DateTime.Today);

    public void OnGet([FromQuery] string? action) => View = WorkflowPage.Board(DateTime.Today, action);
}
