using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Auth;

public class ResetPasswordModel : SamplePageModel
{
    [BindProperty] public ResetPasswordInput Input { get; set; } = new();

    public void OnGet() { }

    public IActionResult OnPost() => ModelState.IsValid ? Redirect("~/auth/login?reset=1") : Page();
}
