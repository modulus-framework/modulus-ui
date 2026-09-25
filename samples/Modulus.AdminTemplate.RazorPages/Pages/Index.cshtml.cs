using Microsoft.AspNetCore.Mvc.RazorPages;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages;

public class IndexModel(SampleStore store) : PageModel
{
    public AnalyticsDashboard Dashboard { get; private set; } = default!;

    public void OnGet(int? range) => Dashboard = store.Analytics(range ?? 30);
}
