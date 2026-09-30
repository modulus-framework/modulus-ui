using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>
/// Sign-in flows. Sign in and sign out are real (a cookie, see <see cref="SampleAuth"/>); registration, password reset and the
/// two-step code are demos where any valid input succeeds.
/// </summary>
[AllowAnonymous]
[Route("auth")]
public class AuthController(SampleStore store) : SampleController
{
    [HttpGet("login")]
    public IActionResult Login() => AuthPage("Sign in", "Auth/Login", new LoginInput());

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginInput input, [FromQuery] string? returnUrl)
    {
        if (ModelState.IsValid && SampleAuth.Check(store, input.Email, input.Password) is { } user)
        {
            await HttpContext.SignInAsync(SampleAuth.Scheme, SampleAuth.Principal(user), new AuthenticationProperties { IsPersistent = input.Remember });
            return LocalRedirect(SampleAuth.SafeReturnUrl(returnUrl));
        }
        if (ModelState.IsValid) ModelState.AddModelError(nameof(LoginInput.Password), SampleAuth.WrongCredentials);
        return AuthPage("Sign in", "Auth/Login", input);
    }

    [HttpGet("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(SampleAuth.Scheme);
        return Redirect("~" + SampleAuth.LoginPath);
    }

    [HttpGet("register")]
    public IActionResult Register() => AuthPage("Sign up", "Auth/Register", new RegisterInput());

    [HttpPost("register")]
    public IActionResult Register(RegisterInput input) =>
        ModelState.IsValid ? Redirect("~/auth/verify-email?email=" + Uri.EscapeDataString(input.Email)) : AuthPage("Sign up", "Auth/Register", input);

    [HttpGet("forgot-password")]
    public IActionResult ForgotPassword() => AuthPage("Forgot password", "Auth/ForgotPassword", new ForgotPasswordInput());

    [HttpPost("forgot-password")]
    public IActionResult ForgotPassword(ForgotPasswordInput input)
    {
        ViewData["Sent"] = ModelState.IsValid;
        return AuthPage("Forgot password", "Auth/ForgotPassword", input);
    }

    [HttpGet("reset-password")]
    public IActionResult ResetPassword() => AuthPage("Reset password", "Auth/ResetPassword", new ResetPasswordInput());

    [HttpPost("reset-password")]
    public IActionResult ResetPassword(ResetPasswordInput input) =>
        ModelState.IsValid ? Redirect("~/auth/login?reset=1") : AuthPage("Reset password", "Auth/ResetPassword", input);

    [HttpGet("verify-email")]
    public IActionResult VerifyEmail() => AuthPage("Verify email", "Auth/VerifyEmail");

    [HttpPost("verify-email")]
    public IActionResult ResendVerification(string? email)
    {
        Flash("Verification email sent again.");
        return Redirect("~/auth/verify-email" + Request.QueryString);
    }

    [HttpGet("two-steps")]
    public IActionResult TwoSteps() => AuthPage("Two-step verification", "Auth/TwoSteps", new TwoStepInput());

    [HttpPost("two-steps")]
    public IActionResult TwoSteps(TwoStepInput input) =>
        ModelState.IsValid ? Redirect("~/") : AuthPage("Two-step verification", "Auth/TwoSteps", input);
}
