using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Auth;

public class ForgotPasswordModel : SamplePageModel
{
    [BindProperty] public ForgotPasswordInput Input { get; set; } = new();

    public void OnGet() { }

    public void OnPost() => ViewData["Sent"] = ModelState.IsValid;
}
