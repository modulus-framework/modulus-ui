using System.Text;
using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web;
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

    /// <summary>Editable grid: the saved schema and rows.</summary>
    [HttpGet("editable")]
    public IActionResult Editable([FromServices] ProjectGridStore store) =>
        Page("Editable grid", "Tables/Editable", ProjectGridPage.Saved(store));

    /// <summary>Every grid button (Save, column operations, Reset) posts the whole grid here; htmx swaps the card.</summary>
    [HttpPost("editable")]
    public IActionResult EditablePost([FromServices] ProjectGridStore store)
    {
        var (page, type, message) = ProjectGridActions.Handle(Request.Form, store);
        if (message is not null)
        {
            if (Request.IsHtmx()) Response.HxMessage(type!.Value, message);
            else Flash(message, type!.Value.ToString().ToLowerInvariant());
        }
        return Page("Editable grid", "Tables/Editable", page);
    }
}
