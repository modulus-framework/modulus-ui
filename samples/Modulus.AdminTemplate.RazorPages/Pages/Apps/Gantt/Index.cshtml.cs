using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Gantt;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Gantt;

/// <summary>Gantt: the project timeline, read only (the same page as the MVC sample).</summary>
public class GanttModel : SamplePageModel
{
    public GanttView View { get; private set; } = GanttPage.Board(DateTime.Today);

    public void OnGet() => View = GanttPage.Board(DateTime.Today);
}
