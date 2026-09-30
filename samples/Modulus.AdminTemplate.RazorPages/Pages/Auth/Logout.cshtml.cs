using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Auth;

/// <summary>Signs out (the header's Sign out link) and goes to the sign-in page.</summary>
public class LogoutModel : SamplePageModel
{
    public async Task<IActionResult> OnGetAsync()
    {
        await HttpContext.SignOutAsync(SampleAuth.Scheme);
        return Redirect("~" + SampleAuth.LoginPath);
    }
}
