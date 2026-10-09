using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Dashboard;
using Modulus.Ui.Abstractions.State;

namespace Modulus.AdminTemplate.Mvc.Controllers;

public class DashboardsController(SampleStore store) : SampleController
{
    [HttpGet("")]
    public IActionResult Analytics(int? range) => Page("Analytics", "Dashboards/Analytics", store.Analytics(range ?? 30));

    [HttpGet("dashboards/crm")]
    public IActionResult Crm() => Page("CRM", "Dashboards/Crm", store.Crm());

    [HttpGet("dashboards/ecommerce")]
    public IActionResult Ecommerce() => Page("eCommerce", "Dashboards/Ecommerce", store.Ecommerce());

    [HttpGet("dashboards/erp")]
    public IActionResult Erp() => Page("ERP analytics", "Dashboards/Erp");

    /// <summary>The tile layout: the signed-in user's saved arrangement, or the default. The forms post to <c>/_modulus/dashboard</c>.</summary>
    [HttpGet("dashboards/tiles")]
    public async Task<IActionResult> Tiles([FromServices] IUiStateStore state)
    {
        var layout = await DashboardModel.LoadAsync(state, UiUser.Key(User), DashboardPage.Id, DashboardPage.Tiles, HttpContext.RequestAborted);
        return Page("Dashboard layout", "Dashboards/Tiles/Tiles", DashboardModel.View(DashboardPage.Id, DashboardPage.Tiles, layout));
    }
}
