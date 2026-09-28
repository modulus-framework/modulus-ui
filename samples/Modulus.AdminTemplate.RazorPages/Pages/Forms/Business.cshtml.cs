using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Forms;

namespace Modulus.AdminTemplate.RazorPages.Pages.Forms;

public class BusinessModel : SamplePageModel
{
    public BusinessFormInput Input { get; private set; } = new();

    public void OnGet() { }

    // The product lookup's search (/forms/business/products, the same URL as the MVC sample): JSON LookupItems.
    public IActionResult OnGetProducts(string? q, int max = 20) => new JsonResult(BusinessFormsDemo.SearchProducts(q, max));

    // Bound as a handler parameter (field names without a prefix, same as the MVC action).
    public IActionResult OnPost(BusinessFormInput input)
    {
        input.Notes = RichText.Sanitize(input.Notes);
        Input = input;
        if (!ModelState.IsValid) return Page();
        Flash(BusinessFormsDemo.Summary(input));
        return Redirect("~/forms/business");
    }
}
