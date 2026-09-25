using System.Text;
using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Data table (Tabler datagrid): every option is a query value, so the grid is a plain GET.</summary>
[Route("tables")]
public class TablesController : SampleController
{
    [HttpGet("advanced")]
    public IActionResult Advanced([FromQuery] EmployeeQuery query, [FromQuery] ProTableRequest request, string? hide) =>
        Page("Data table", "Tables/Advanced", EmployeeTablePage.Load(query, request, hide));

    /// <summary>CSV of the selected rows (ids) or of every row matching the filters.</summary>
    [HttpGet("advanced/export")]
    public IActionResult Export([FromQuery] EmployeeQuery query, [FromQuery] int[] ids) =>
        File(Encoding.UTF8.GetBytes(EmployeeDirectory.Csv(EmployeeDirectory.ForExport(query, ids))), "text/csv", "employees.csv");
}
