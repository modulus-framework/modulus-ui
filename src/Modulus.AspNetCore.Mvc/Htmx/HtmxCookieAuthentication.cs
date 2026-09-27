using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Modulus.AspNetCore.Mvc.Htmx;

/// <summary>
/// Makes cookie authentication (plain cookies, Identity, OIDC with cookies) behave under htmx. By
/// default an htmx request whose session has expired follows the 302 to the sign-in page and swaps
/// that page into its target. Instead:
/// <list type="bullet">
/// <item>Sign-in challenge: 401 with <c>HX-Redirect</c>, so the whole page goes to sign-in. The return
/// URL is the page the user is on (<c>HX-Current-URL</c>), not the fragment endpoint htmx called.</item>
/// <item>Access denied: a plain 403, which modulus-mvc.js shows as a "no permission" toast and leaves
/// the page as it is.</item>
/// </list>
/// Applies to every cookie scheme and wraps any events the app sets. Apps that resolve events
/// from DI with <c>EventsType</c> call <see cref="HandleRedirectToLogin"/> and
/// <see cref="HandleRedirectToAccessDenied"/> from their own handlers.
/// </summary>
public sealed class HtmxCookieAuthentication : IPostConfigureOptions<CookieAuthenticationOptions>
{
    public void PostConfigure(string? name, CookieAuthenticationOptions options)
    {
        var events = options.Events;
        var toLogin = events.OnRedirectToLogin;
        var toAccessDenied = events.OnRedirectToAccessDenied;
        events.OnRedirectToLogin = context => HandleRedirectToLogin(context) ? Task.CompletedTask : toLogin(context);
        events.OnRedirectToAccessDenied = context => HandleRedirectToAccessDenied(context) ? Task.CompletedTask : toAccessDenied(context);
    }

    /// <summary>Answers an htmx sign-in challenge with 401 and <c>HX-Redirect</c>; false for other requests.</summary>
    public static bool HandleRedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        var request = context.Request;
        if (!request.IsHtmx())
            return false;

        var returnUrl = CurrentPage(request) ?? request.PathBase + request.Path + request.QueryString;
        var login = request.PathBase + context.Options.LoginPath + QueryString.Create(context.Options.ReturnUrlParameter, returnUrl);
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.HxRedirect(login);
        return true;
    }

    /// <summary>Answers an htmx request the user may not make with a plain 403; false for other requests.</summary>
    public static bool HandleRedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (!context.Request.IsHtmx())
            return false;

        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return true;
    }

    // The browser's address (HX-Current-URL), when it is on this site, as a local path and query.
    private static string? CurrentPage(HttpRequest request) =>
        Uri.TryCreate(request.Headers["HX-Current-URL"].ToString(), UriKind.Absolute, out var current)
        && string.Equals(current.Authority, request.Host.Value, StringComparison.OrdinalIgnoreCase)
            ? current.PathAndQuery
            : null;
}
