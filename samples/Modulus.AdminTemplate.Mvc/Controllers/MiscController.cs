using Microsoft.AspNetCore.Mvc;

namespace Modulus.AdminTemplate.Mvc.Controllers;

/// <summary>Error, status and holding pages. The status-code middleware re-executes /not-found and /error.</summary>
[Microsoft.AspNetCore.Authorization.AllowAnonymous]
public class MiscController : SampleController
{
    // Any verb: the status-code middleware re-executes the original request method.
    [Route("misc/not-found")]
    [Route("not-found")]
    public IActionResult NotFoundPage()
    {
        if (HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IStatusCodeReExecuteFeature>() is not null)
            Response.StatusCode = StatusCodes.Status404NotFound;
        return AuthPage("Page not found", "Misc/NotFound");
    }

    [HttpGet("misc/not-authorized")]
    [HttpGet("forbidden")]
    public IActionResult NotAuthorized() => AuthPage("Not authorized", "Misc/NotAuthorized");

    [Route("misc/server-error")]
    [Route("error")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult ServerError() => AuthPage("Server error", "Misc/ServerError");

    [HttpGet("misc/maintenance")]
    public IActionResult Maintenance() => AuthPage("Maintenance", "Misc/Maintenance");

    [HttpGet("misc/coming-soon")]
    public IActionResult ComingSoon() => AuthPage("Coming soon", "Misc/ComingSoon");
}
