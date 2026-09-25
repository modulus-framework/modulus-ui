using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;

namespace Modulus.AdminTemplate.RazorPages.Pages.Apps.Customers;

/// <summary>One customer: profile card and order history (edits go through the list's drawer handlers).</summary>
public class CustomerViewModel(SampleStore store) : SamplePageModel
{
    public CustomerViewPage View { get; private set; } = default!;

    public IActionResult OnGet(int id)
    {
        if (store.FindCustomer(id) is not { } customer) return NotFound();
        View = CustomerViewPage.Load(store, customer);
        return Page();
    }
}
