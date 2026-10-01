using System.Text;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Plugins;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Data table (Tabler datagrid): every option is a query value, so the grid is a plain GET.</summary>
[Route("tables")]
public class TablesController : SampleController
{
    // The header column searches (s.{field}) are read here: model binding leaves the dictionary alone.
    private EmployeeQuery WithSearches(EmployeeQuery query) => query with { Searches = Request.Query.ColumnSearches() };

    [HttpGet("advanced")]
    public IActionResult Advanced([FromQuery] EmployeeQuery query, [FromQuery] ProTableRequest request, string? hide) =>
        Page("Data table", "Tables/Advanced", EmployeeTablePage.Load(WithSearches(query), request, hide));

    /// <summary>Large data: 100,000 shipments in pages, with infinite scrolling or in a virtual table (mode).</summary>
    [HttpGet("large")]
    public IActionResult Large(string? mode, [FromQuery] ShipmentFilter filter, [FromQuery] ProTableRequest request)
    {
        request.Searches = Request.Query.ColumnSearches();
        return Page("Large data", "Tables/Large", LargeTablePage.Load(mode, filter, request));
    }

    /// <summary>More rows for the large-data table: the next infinite page (cursor) or a virtual block (current, pageSize).</summary>
    [HttpGet("large/rows")]
    public IActionResult LargeRows(string? mode, [FromQuery] ShipmentFilter filter, [FromQuery] ProTableRequest request)
    {
        request.Searches = Request.Query.ColumnSearches();
        ViewData["RowsUrl"] = Request.Path + QueryString.Create(Request.Query
            .Where(q => q.Key is not ("current" or "pageSize" or "cursor"))
            .Select(q => new KeyValuePair<string, string?>(q.Key, q.Value.ToString())));
        return PartialView("~/Views/Shared/Ui/Tables/_LargeRows.cshtml", LargeTablePage.Rows(mode, filter, request));
    }

    /// <summary>Pivot of the 100,000 shipments, in the manner of Odoo's pivot view: the layout is in the query string.</summary>
    [HttpGet("pivot")]
    public IActionResult Pivot([FromQuery] ShipmentFilter filter) =>
        Page("Pivot", "Tables/Pivot", PivotTablePage.Load(filter, QueryValues()));

    /// <summary>The pivot as an Excel workbook (the toolbar's Download).</summary>
    [HttpGet("pivot/download")]
    public IActionResult PivotDownload([FromQuery] ShipmentFilter filter) =>
        File(PivotTablePage.Download(filter, QueryValues()), Modulus.Ui.Abstractions.Pivot.PivotXlsx.ContentType, PivotTablePage.DownloadName);

    private IEnumerable<KeyValuePair<string, string?>> QueryValues() =>
        Request.Query.SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string?>(q.Key, v)));

    /// <summary>CSV of the selected rows (ids) or of every row matching the filters.</summary>
    [HttpGet("advanced/export")]
    public IActionResult Export([FromQuery] EmployeeQuery query, [FromQuery] int[] ids) =>
        File(Encoding.UTF8.GetBytes(EmployeeDirectory.Csv(EmployeeDirectory.ForExport(WithSearches(query), ids))), "text/csv", "employees.csv");

    /// <summary>"Add item" in a label dropdown: the new label as JSON, or { error }.</summary>
    [HttpPost("advanced/labels")]
    public IActionResult AddLabel([FromForm] string? label, [FromForm] string? color) => Json(EmployeeLabelActions.Add(label, color));

    /// <summary>The batch bar's label dropdown: gives the label to the selected rows and re-renders the grid.</summary>
    [HttpPost("advanced/tag")]
    public IActionResult Tag([FromQuery] EmployeeQuery query, [FromQuery] ProTableRequest request, string? hide, [FromForm] int[] ids, [FromForm] string? label)
    {
        if (EmployeeLabelActions.Apply(ids, label) is { } message) Response.HxMessage(MessageType.Success, message);
        return Page("Data table", "Tables/Advanced", EmployeeTablePage.Load(WithSearches(query), request, hide));
    }

    /// <summary>A row dragged to a new place on the page shown: stores the new order and re-renders the grid.</summary>
    [HttpPost("advanced/move")]
    public IActionResult Move([FromQuery] EmployeeQuery query, [FromQuery] ProTableRequest request, string? hide, [FromForm] string? move)
    {
        if (SortMove.TryParse(move, out var m)) EmployeeDirectory.Move(WithSearches(query), request, m);
        return Page("Data table", "Tables/Advanced", EmployeeTablePage.Load(WithSearches(query), request, hide));
    }

    /// <summary>Data grid: the typed orders table (columns discovered from the row type) over the nested orders-and-lines grid.</summary>
    [HttpGet("data-grid")]
    public IActionResult DataGrid([FromServices] PurchaseStore store, [FromQuery] ProTableRequest request) =>
        Page("Data grid", "Tables/DataGrid", PurchaseGridActions.Page(store, Request.Query, request));

    [HttpGet("data-grid/level")]
    public IActionResult DataGridLevel([FromServices] PurchaseStore store) => PartialView(DataGridLevelView, PurchaseGridActions.Get(store, Request.Query));

    [HttpPost("data-grid/level")]
    public IActionResult DataGridLevelPost([FromServices] PurchaseStore store) => PartialView(DataGridLevelView, PurchaseGridActions.Post(store, Request, Response));

    private const string DataGridLevelView = "~/Views/Shared/Ui/Tables/_NestedLevel.cshtml";

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
