using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web;

namespace Modulus.AdminTemplate.RazorPages.Pages.Ui;

public class ComponentsModel : SamplePageModel
{
    public GalleryForm Form { get; } = new();

    public void OnGet() { }

    /// <summary>POST /ui/components/customer: the customer picker's "Add customer" modal.</summary>
    public IActionResult OnPostCustomer([FromServices] SampleStore store, CustomerInput input)
    {
        if (!ModelState.IsValid) return Partial(CustomerPicker.FormView, input);
        CustomerPicker.Saved(Response, store.AddCustomer(input));
        ModelState.Clear();
        return Partial(CustomerPicker.FormView, new CustomerInput());
    }

    public IActionResult OnPostDemo(string kind)
    {
        GalleryDemo.Respond(Request, kind);
        return NoContent();
    }
}
