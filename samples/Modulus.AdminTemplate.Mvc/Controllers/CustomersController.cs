using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>
/// Customers list (Tabler datagrid over htmx) and customer view. Filters, paging and sorting are
/// boosted GETs; the add / edit drawer saves return 204 with a refresh event.
/// </summary>
[Route("apps/customers")]
public class CustomersController(SampleStore store) : SampleController
{
    public const string RefreshEvent = "customers:refresh";
    private const string FormView = "~/Views/Shared/Ui/Apps/Customers/_CustomerForm.cshtml";

    [HttpGet("")]
    public IActionResult List([FromQuery] CustomerQuery query, [FromQuery] ProTableRequest request) =>
        Page("Customers", "Apps/Customers/CustomerList", CustomerListPage.Load(store, query, request));

    [HttpGet("{id:int}")]
    public IActionResult View(int id) =>
        store.FindCustomer(id) is { } customer ? Page(customer.Name, "Apps/Customers/CustomerView", CustomerViewPage.Load(store, customer)) : NotFound();

    [HttpGet("form/{id:int?}")]
    public IActionResult Form(int? id)
    {
        if (id is null) return Drawer(null, new CustomerInput());
        return store.FindCustomer(id.Value) is { } customer ? Drawer(id, CustomerInput.From(customer)) : NotFound();
    }

    [HttpPost("save/{id:int?}")]
    public IActionResult Save(int? id, CustomerInput input)
    {
        if (!ModelState.IsValid) return Drawer(id, input);
        if (id is { } existing)
        {
            if (!store.UpdateCustomer(existing, input)) return NotFound();
        }
        else store.AddCustomer(input);

        Response.HxMessage(MessageType.Success, id is null ? $"Added {input.Name}" : $"Saved {input.Name}")
            .HxCloseDrawer().HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/customers");
    }

    [HttpPost("delete")]
    public IActionResult Delete([FromForm] int[] ids)
    {
        var count = store.DeleteCustomers(ids);
        Response.HxMessage(MessageType.Success, count == 1 ? "Customer deleted" : $"{count} customers deleted").HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/customers");
    }

    private PartialViewResult Drawer(int? id, CustomerInput input)
    {
        ViewData["Id"] = id;
        return PartialView(FormView, input);
    }
}
