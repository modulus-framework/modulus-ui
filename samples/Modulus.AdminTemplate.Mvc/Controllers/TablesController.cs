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

    /// <summary>"Add item" in a label dropdown: the new label as JSON, or { error }.</summary>
    [HttpPost("advanced/labels")]
    public IActionResult AddLabel([FromForm] string? label, [FromForm] string? color) => Json(EmployeeLabelActions.Add(label, color));

    /// <summary>The batch bar's label dropdown: gives the label to the selected rows and re-renders the grid.</summary>
    [HttpPost("advanced/tag")]
    public IActionResult Tag([FromQuery] EmployeeQuery query, [FromQuery] ProTableRequest request, string? hide, [FromForm] int[] ids, [FromForm] string? label)
    {
        if (EmployeeLabelActions.Apply(ids, label) is { } message) Response.HxMessage(MessageType.Success, message);
        return Page("Data table", "Tables/Advanced", EmployeeTablePage.Load(query, request, hide));
    }

    /// <summary>Nested grid: the top level; expanded rows load theirs from nested/level.</summary>
    [HttpGet("nested")]
    public IActionResult Nested([FromServices] GarmentStore store) => Page("Nested grid", "Tables/Nested", GarmentGridActions.Page(store, Request.Query));

    [HttpGet("nested/level")]
    public IActionResult NestedLevel([FromServices] GarmentStore store) => PartialView(NestedLevelView, GarmentGridActions.Get(store, Request.Query));

    [HttpPost("nested/level")]
    public IActionResult NestedLevelPost([FromServices] GarmentStore store) => PartialView(NestedLevelView, GarmentGridActions.Post(store, Request, Response));

    private const string NestedLevelView = "~/Views/Shared/Ui/Tables/_NestedLevel.cshtml";

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
