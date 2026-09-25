using Microsoft.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.Htmx;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;
using Modulus.Ui.Abstractions;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Customers;

/// <summary>Customers list: same URLs as the MVC sample (/form/{id}, /save/{id}, /delete).</summary>
public class CustomerListModel(SampleStore store) : SamplePageModel
{
    public const string RefreshEvent = "customers:refresh";
    private const string FormView = "~/Views/Shared/Ui/Apps/Customers/_CustomerForm.cshtml";

    public CustomerListPage View { get; private set; } = default!;

    public void OnGet([FromQuery] CustomerQuery query, [FromQuery] ProTableRequest request) =>
        View = CustomerListPage.Load(store, query, request);

    public IActionResult OnGetForm(int? id)
    {
        if (id is null) return Drawer(null, new CustomerInput());
        return store.FindCustomer(id.Value) is { } customer ? Drawer(id, CustomerInput.From(customer)) : NotFound();
    }

    public IActionResult OnPostSave(int? id, CustomerInput input)
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

    public IActionResult OnPostDelete([FromForm] int[] ids)
    {
        var count = store.DeleteCustomers(ids);
        Response.HxMessage(MessageType.Success, count == 1 ? "Customer deleted" : $"{count} customers deleted").HxTrigger(RefreshEvent);
        return Request.IsHtmx() ? NoContent() : Redirect("~/apps/customers");
    }

    private PartialViewResult Drawer(int? id, CustomerInput input) => Partial(FormView, input, ("Id", id));
}
