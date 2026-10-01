using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Pivot;

namespace Modulus.AdminTemplate.RazorPages.Pages.Tables;

/// <summary>Pivot of the shipments: same URLs as the MVC sample (/tables/pivot, /tables/pivot/download).</summary>
public class PivotModel : SamplePageModel
{
    public PivotTablePage Table { get; private set; } = default!;

    public void OnGet([FromQuery] ShipmentFilter filter) => Table = PivotTablePage.Load(filter, QueryValues());

    /// <summary>GET /tables/pivot/download: the pivot as an Excel workbook.</summary>
    public IActionResult OnGetDownload([FromQuery] ShipmentFilter filter) =>
        File(PivotTablePage.Download(filter, QueryValues()), PivotXlsx.ContentType, PivotTablePage.DownloadName);

    private IEnumerable<KeyValuePair<string, string?>> QueryValues() =>
        Request.Query.SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string?>(q.Key, v)));
}
