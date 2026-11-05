using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Flow;

/// <summary>The workflow designer (the same page as the MVC sample): the editor's graph posts as JSON in the "Workflow" field.</summary>
public class FlowModel : SamplePageModel
{
    public FlowView View { get; private set; } = FlowPage.Show();

    public void OnGet() => View = FlowPage.Show();

    public IActionResult OnPost([FromForm(Name = "Workflow")] string? workflow)
    {
        var saved = FlowPage.TrySave(workflow, out var message);
        Flash(message, saved ? "success" : "error");
        return Redirect("/apps/flow");
    }

    /// <summary>Runs the workflow as it is on the canvas for one customer message, and shows what each node did.</summary>
    public async Task<IActionResult> OnPostRunAsync([FromForm(Name = "Workflow")] string? workflow, [FromForm] string? message)
    {
        var view = await FlowPage.RunAsync(workflow, message);
        if (view is null)
        {
            Flash("That is not a workflow, so nothing ran.", "error");
            return Redirect("/apps/flow");
        }
        View = view;
        return Page();
    }

    public IActionResult OnPostReset()
    {
        FlowDemo.Reset();
        Flash("Back to the sample workflow.");
        return Redirect("/apps/flow");
    }
}
