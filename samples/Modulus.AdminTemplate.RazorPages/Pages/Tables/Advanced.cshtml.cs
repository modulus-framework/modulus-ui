using System.Text;
using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Plugins;

namespace Modulus.AdminTemplate.RazorPages.Pages.Tables;

/// <summary>Data table: same URLs as the MVC sample (/tables/advanced, /tables/advanced/export).</summary>
public class AdvancedModel : SamplePageModel
{
    public EmployeeTablePage Table { get; private set; } = default!;

    // The header column searches (s.{field}) are read here: model binding leaves the dictionary alone.
    private EmployeeQuery WithSearches(EmployeeQuery query) => query with { Searches = Request.Query.ColumnSearches() };

    public void OnGet([FromQuery] EmployeeQuery query, [FromQuery] ProTableRequest request, string? hide) =>
        Table = EmployeeTablePage.Load(WithSearches(query), request, hide);

    /// <summary>POST /tables/advanced/labels: "Add item" in a label dropdown.</summary>
    public IActionResult OnPostLabels([FromForm] string? label, [FromForm] string? color) => new JsonResult(EmployeeLabelActions.Add(label, color));

    /// <summary>POST /tables/advanced/tag: the batch bar gives a label to the selected rows; htmx swaps the grid.</summary>
    public void OnPostTag([FromQuery] EmployeeQuery query, [FromQuery] ProTableRequest request, string? hide, [FromForm] int[] ids, [FromForm] string? label)
    {
        if (EmployeeLabelActions.Apply(ids, label) is { } message) Response.HxMessage(MessageType.Success, message);
        Table = EmployeeTablePage.Load(WithSearches(query), request, hide);
    }

    /// <summary>POST /tables/advanced/move: a row dragged to a new place on the page shown; htmx swaps the grid.</summary>
    public void OnPostMove([FromQuery] EmployeeQuery query, [FromQuery] ProTableRequest request, string? hide, [FromForm] string? move)
    {
        if (SortMove.TryParse(move, out var m)) EmployeeDirectory.Move(WithSearches(query), request, m);
        Table = EmployeeTablePage.Load(WithSearches(query), request, hide);
    }

    public IActionResult OnGetExport([FromQuery] EmployeeQuery query, [FromQuery] int[] ids) =>
        File(Encoding.UTF8.GetBytes(EmployeeDirectory.Csv(EmployeeDirectory.ForExport(WithSearches(query), ids))), "text/csv", "employees.csv");
}
