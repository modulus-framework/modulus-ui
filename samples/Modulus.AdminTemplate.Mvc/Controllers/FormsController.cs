using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;

namespace Modulus.AdminTemplate.Mvc.Controllers;

[Route("forms")]
public class FormsController : SampleController
{
    // Layouts: one POST per form; invalid input re-renders the page with that form's errors (Post/Redirect/Get on success).

    [HttpGet("layouts")]
    public IActionResult Layouts() => LayoutsPage(FormLayoutsPage.Load());

    [HttpPost("layouts/contact")]
    public IActionResult Contact([Bind(Prefix = "Contact")] ContactFormInput input) =>
        ModelState.IsValid ? Done("Message sent. We reply within one business day.") : LayoutsPage(FormLayoutsPage.Load(contact: input));

    [HttpPost("layouts/signup")]
    public IActionResult Signup([Bind(Prefix = "Signup")] SignupFormInput input) =>
        ModelState.IsValid ? Done($"Account {input.Username} created") : LayoutsPage(FormLayoutsPage.Load(signup: input));

    [HttpPost("layouts/shipping")]
    public IActionResult Shipping([Bind(Prefix = "Shipping")] ShippingFormInput input) =>
        ModelState.IsValid ? Done("Shipping address saved") : LayoutsPage(FormLayoutsPage.Load(shipping: input));

    // Wizard: one form; "go" is the step to show next. Moving forward validates the current step only.

    [HttpGet("wizard")]
    public IActionResult Wizard() =>
        WizardView(new WizardInput { Username = TempData["WizardUser"] as string }, TempData.ContainsKey("WizardUser") ? WizardPage.ReviewStep + 1 : 0);

    [HttpPost("wizard")]
    public IActionResult Wizard([Bind(Prefix = "Input")] WizardInput input, int step, int go)
    {
        step = Math.Clamp(step, 0, WizardPage.ReviewStep);
        go = Math.Clamp(go, 0, WizardPage.ReviewStep + 1);
        ModelState.Clear();
        if (go <= step) return WizardView(input, go);

        foreach (var (field, messages) in input.Validate(step))
            foreach (var message in messages) ModelState.AddModelError("Input." + field, message);
        if (!ModelState.IsValid) return WizardView(input, step);
        if (go <= WizardPage.ReviewStep) return WizardView(input, go);

        TempData["WizardUser"] = input.Username;
        return Redirect("~/forms/wizard");
    }

    [HttpGet("validation")]
    public IActionResult Validation() => Page("Validation", "Forms/Validation", new ValidationDemoInput());

    [HttpPost("validation")]
    public IActionResult Validation(ValidationDemoInput input)
    {
        if (!ModelState.IsValid) return Page("Validation", "Forms/Validation", input);
        Flash($"Profile for {input.Name} submitted");
        return Redirect("~/forms/validation");
    }

    private ViewResult WizardView(WizardInput input, int step) => Page("Wizard", "Forms/Wizard", new WizardPage(input, step));

    private ViewResult LayoutsPage(FormLayoutsPage model) => Page("Form layouts", "Forms/Layouts", model);

    private RedirectResult Done(string message)
    {
        Flash(message);
        return Redirect("~/forms/layouts");
    }
}
