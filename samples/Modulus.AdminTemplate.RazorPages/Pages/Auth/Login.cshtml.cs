using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.RazorPages.Pages.Auth;

/// <summary>Cookie sign-in (see <see cref="SampleAuth"/>); back to the page that asked for it.</summary>
public class LoginModel(SampleStore store) : SamplePageModel
{
    [BindProperty] public LoginInput Input { get; set; } = new();

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        if (ModelState.IsValid && SampleAuth.Check(store, Input.Email, Input.Password) is { } user)
        {
            await HttpContext.SignInAsync(SampleAuth.Scheme, SampleAuth.Principal(user), new AuthenticationProperties { IsPersistent = Input.Remember });
            return LocalRedirect(SampleAuth.SafeReturnUrl(returnUrl));
        }
        if (ModelState.IsValid) ModelState.AddModelError(nameof(LoginInput.Password), SampleAuth.WrongCredentials); // the form posts unprefixed names
        return Page();
    }
}
