using Microsoft.AspNetCore.Mvc;
using Modulus.Samples.Shared;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Sign-in flows. Demo only: any valid input succeeds.</summary>
[Route("auth")]
public class AuthController : SampleController
{
    [HttpGet("login")]
    public IActionResult Login() => AuthPage("Sign in", "Auth/Login", new LoginInput());

    [HttpPost("login")]
    public IActionResult Login(LoginInput input) =>
        ModelState.IsValid ? Redirect("~/auth/two-steps") : AuthPage("Sign in", "Auth/Login", input);

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
