using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;
using Modulus.Samples.Web.Models;

namespace Modulus.AdminTemplate.RazorPages.Pages.Forms;

/// <summary>Account wizard: "go" is the step to show next; moving forward validates the current step only.</summary>
public class WizardModel : SamplePageModel
{
    public WizardPage Wizard { get; private set; } = new(new WizardInput(), 0);

    public void OnGet()
    {
        if (TempData["WizardUser"] is string user)
            Wizard = new(new WizardInput { Username = user }, WizardPage.ReviewStep + 1);
    }

    public IActionResult OnPost([Bind(Prefix = "Input")] WizardInput input, int step, int go)
    {
        step = Math.Clamp(step, 0, WizardPage.ReviewStep);
        go = Math.Clamp(go, 0, WizardPage.ReviewStep + 1);
        ModelState.Clear();
        if (go > step)
        {
            foreach (var (field, messages) in input.Validate(step))
                foreach (var message in messages) ModelState.AddModelError("Input." + field, message);
            if (ModelState.IsValid && go > WizardPage.ReviewStep)
            {
                TempData["WizardUser"] = input.Username;
                return Redirect("~/forms/wizard");
            }
        }
        Wizard = new(input, ModelState.IsValid ? go : step);
        return Page();
    }
}
