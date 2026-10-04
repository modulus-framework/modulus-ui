using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Pivot;
using Modulus.Ui.Abstractions.State;

namespace Modulus.AdminTemplate.RazorPages.Pages.Tables;

/// <summary>Pivot of the shipments: same URLs as the MVC sample (/tables/pivot, /tables/pivot/download).</summary>
public class PivotModel : SamplePageModel
{
    public PivotTablePage Table { get; private set; } = default!;

    public async Task<IActionResult> OnGetAsync([FromQuery] ShipmentFilter filter, [FromServices] SavedViews views)
    {
        // A visit that names no layout opens the user's default view (the Views dropdown's star).
        if (await views.DefaultUrlAsync(UiUser.Key(User), PivotTablePage.Path, PivotTablePage.Path, QueryValues()) is { } url) return LocalRedirect(url);
        Table = PivotTablePage.Load(filter, QueryValues(), PivotTablePage.Access.For(User));
        return Page();
    }

    /// <summary>GET /tables/pivot/download: the pivot as an Excel workbook.</summary>
    public IActionResult OnGetDownload([FromQuery] ShipmentFilter filter) =>
        File(PivotTablePage.Download(filter, QueryValues(), PivotTablePage.Access.For(User)), PivotXlsx.ContentType, PivotTablePage.DownloadName);

    private IEnumerable<KeyValuePair<string, string?>> QueryValues() =>
        Request.Query.SelectMany(q => q.Value.Select(v => new KeyValuePair<string, string?>(q.Key, v)));
}
