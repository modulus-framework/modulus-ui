using Microsoft.AspNetCore.Mvc.RazorPages;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Dashboards;

public class EcommerceModel(SampleStore store) : PageModel
{
    public EcommerceDashboard Dashboard { get; private set; } = default!;

    public void OnGet() => Dashboard = store.Ecommerce();
}
