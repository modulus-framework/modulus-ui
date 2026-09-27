using System.Text;
using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Tables;

/// <summary>Data table: same URLs as the MVC sample (/tables/advanced, /tables/advanced/export).</summary>
public class AdvancedModel : SamplePageModel
{
    public EmployeeTablePage Table { get; private set; } = default!;

    public void OnGet([FromQuery] EmployeeQuery query, [FromQuery] ProTableRequest request, string? hide) =>
        Table = EmployeeTablePage.Load(query, request, hide);

    /// <summary>POST /tables/advanced/labels: "Add item" in a label dropdown.</summary>
    public IActionResult OnPostLabels([FromForm] string? label, [FromForm] string? color) => new JsonResult(EmployeeLabelActions.Add(label, color));

    /// <summary>POST /tables/advanced/tag: the batch bar gives a label to the selected rows; htmx swaps the grid.</summary>
    public void OnPostTag([FromQuery] EmployeeQuery query, [FromQuery] ProTableRequest request, string? hide, [FromForm] int[] ids, [FromForm] string? label)
    {
        if (EmployeeLabelActions.Apply(ids, label) is { } message) Response.HxMessage(MessageType.Success, message);
        Table = EmployeeTablePage.Load(query, request, hide);
    }

    public IActionResult OnGetExport([FromQuery] EmployeeQuery query, [FromQuery] int[] ids) =>
        File(Encoding.UTF8.GetBytes(EmployeeDirectory.Csv(EmployeeDirectory.ForExport(query, ids))), "text/csv", "employees.csv");
}
