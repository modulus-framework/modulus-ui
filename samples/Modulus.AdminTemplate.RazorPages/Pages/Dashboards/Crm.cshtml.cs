using Microsoft.AspNetCore.Mvc.RazorPages;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Dashboards;

public class CrmModel(SampleStore store) : PageModel
{
    public CrmDashboard Dashboard { get; private set; } = default!;

    public void OnGet() => Dashboard = store.Crm();
}
