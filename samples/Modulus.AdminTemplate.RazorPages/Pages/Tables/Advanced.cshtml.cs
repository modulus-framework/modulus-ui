using System.Text;
using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Tables;

/// <summary>Data table: same URLs as the MVC sample (/tables/advanced, /tables/advanced/export).</summary>
public class AdvancedModel : SamplePageModel
{
    public EmployeeTablePage Table { get; private set; } = default!;

    public void OnGet([FromQuery] EmployeeQuery query, [FromQuery] ProTableRequest request, string? hide) =>
        Table = EmployeeTablePage.Load(query, request, hide);

    public IActionResult OnGetExport([FromQuery] EmployeeQuery query, [FromQuery] int[] ids) =>
        File(Encoding.UTF8.GetBytes(EmployeeDirectory.Csv(EmployeeDirectory.ForExport(query, ids))), "text/csv", "employees.csv");
}
