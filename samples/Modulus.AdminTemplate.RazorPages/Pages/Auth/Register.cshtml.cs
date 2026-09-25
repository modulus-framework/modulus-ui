using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Auth;

public class RegisterModel : SamplePageModel
{
    [BindProperty] public RegisterInput Input { get; set; } = new();

    public void OnGet() { }

    public IActionResult OnPost() =>
        ModelState.IsValid ? Redirect("~/auth/verify-email?email=" + Uri.EscapeDataString(Input.Email)) : Page();
}
