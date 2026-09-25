using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Auth;

public class TwoStepsModel : SamplePageModel
{
    [BindProperty] public TwoStepInput Input { get; set; } = new();

    public void OnGet() { }

    public IActionResult OnPost() => ModelState.IsValid ? Redirect("~/") : Page();
}
