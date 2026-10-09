using Modulus.Samples.Shared;
using Modulus.Samples.Web;

namespace Modulus.AdminTemplate.RazorPages.Pages.Tables;

/// <summary>Visual filter: same URL as the MVC sample (/tables/query), the filter in the query string.</summary>
public class QueryModel : SamplePageModel
{
    public QueryPage Grid { get; private set; } = default!;

    public void OnGet() => Grid = QueryPageActions.Load(Request.Query);
}
