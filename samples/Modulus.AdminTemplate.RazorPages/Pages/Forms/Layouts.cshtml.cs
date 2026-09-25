using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;

namespace Modulus.AdminTemplate.RazorPages.Pages.Forms;

/// <summary>Form layouts: same URLs as the MVC sample (/contact, /signup, /shipping).</summary>
public class LayoutsModel : SamplePageModel
{
    public FormLayoutsPage Form { get; private set; } = FormLayoutsPage.Load();

    public void OnGet() { }

    public IActionResult OnPostContact([Bind(Prefix = "Contact")] ContactFormInput input) =>
        ModelState.IsValid ? Done("Message sent. We reply within one business day.") : Show(FormLayoutsPage.Load(contact: input));

    public IActionResult OnPostSignup([Bind(Prefix = "Signup")] SignupFormInput input) =>
        ModelState.IsValid ? Done($"Account {input.Username} created") : Show(FormLayoutsPage.Load(signup: input));

    public IActionResult OnPostShipping([Bind(Prefix = "Shipping")] ShippingFormInput input) =>
        ModelState.IsValid ? Done("Shipping address saved") : Show(FormLayoutsPage.Load(shipping: input));

    private PageResult Show(FormLayoutsPage form)
    {
        Form = form;
        return Page();
    }

    private RedirectResult Done(string message)
    {
        Flash(message);
        return Redirect("~/forms/layouts");
    }
}
