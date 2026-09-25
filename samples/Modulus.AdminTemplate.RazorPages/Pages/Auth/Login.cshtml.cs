using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Auth;

public class LoginModel : SamplePageModel
{
    [BindProperty] public LoginInput Input { get; set; } = new();

    public void OnGet() { }

    public IActionResult OnPost() => ModelState.IsValid ? Redirect("~/auth/two-steps") : Page();
}
