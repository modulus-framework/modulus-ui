using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Tables;

/// <summary>Large data: same URLs as the MVC sample (/tables/large, /tables/large/rows).</summary>
public class LargeModel : SamplePageModel
{
    public LargeTablePage Table { get; private set; } = default!;

    public void OnGet(string? mode, [FromQuery] ShipmentFilter filter, [FromQuery] ProTableRequest request)
    {
        request.Searches = Request.Query.ColumnSearches();
        Table = LargeTablePage.Load(mode, filter, request);
    }

    /// <summary>GET /tables/large/rows: the next infinite page (cursor) or a virtual block (current, pageSize).</summary>
    public IActionResult OnGetRows(string? mode, [FromQuery] ShipmentFilter filter, [FromQuery] ProTableRequest request)
    {
        request.Searches = Request.Query.ColumnSearches();
        var rowsUrl = Request.Path + QueryString.Create(Request.Query
            .Where(q => q.Key is not ("current" or "pageSize" or "cursor"))
            .SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string?>(q.Key, v))));
        return Partial("~/Views/Shared/Ui/Tables/_LargeRows.cshtml", LargeTablePage.Rows(mode, filter, request), ("RowsUrl", rowsUrl));
    }
}
