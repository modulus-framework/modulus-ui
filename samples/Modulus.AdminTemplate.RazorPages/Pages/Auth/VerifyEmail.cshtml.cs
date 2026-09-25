using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Auth;

public class VerifyEmailModel : SamplePageModel
{
    public void OnGet() { }

    public IActionResult OnPost()
    {
        Flash("Verification email sent again.");
        return Redirect("~/auth/verify-email" + Request.QueryString);
    }
}
