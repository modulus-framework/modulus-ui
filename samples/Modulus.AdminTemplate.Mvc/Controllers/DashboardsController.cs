using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

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
}
