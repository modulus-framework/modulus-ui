using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Modulus.AspNetCore.Mvc.Htmx;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>Cookie sign-in and access-denied redirects under htmx.</summary>
public class HtmxCookieAuthenticationTests
{
    private static CookieAuthenticationOptions Options()
    {
        var options = new CookieAuthenticationOptions { LoginPath = "/account/login", AccessDeniedPath = "/account/denied" };
        new HtmxCookieAuthentication().PostConfigure(CookieAuthenticationDefaults.AuthenticationScheme, options);
        return options;
    }

    private static RedirectContext<CookieAuthenticationOptions> Context(CookieAuthenticationOptions options, bool htmx, string? currentUrl = null)
    {
        var http = new DefaultHttpContext();
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("admin.example.com");
        http.Request.Path = "/customers/row/5";
        if (htmx)
            http.Request.Headers["HX-Request"] = "true";
        if (currentUrl is not null)
            http.Request.Headers["HX-Current-URL"] = currentUrl;
        var scheme = new AuthenticationScheme(CookieAuthenticationDefaults.AuthenticationScheme, null, typeof(CookieAuthenticationHandler));
        return new RedirectContext<CookieAuthenticationOptions>(http, scheme, options, new AuthenticationProperties(), "/account/login?ReturnUrl=%2Fcustomers%2Frow%2F5");
    }

    [Fact]
    public async Task Login_HtmxRequest_RedirectsTheWholePageBackToTheCurrentPage()
    {
        var options = Options();
        var context = Context(options, htmx: true, currentUrl: "https://admin.example.com/customers?status=Active");

        await options.Events.RedirectToLogin(context);

        Assert.Equal(401, context.Response.StatusCode);
        Assert.Equal("/account/login?ReturnUrl=%2Fcustomers%3Fstatus%3DActive", context.Response.Headers["HX-Redirect"].ToString());
        Assert.Empty(context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Login_HtmxRequestFromAnotherSite_ReturnsToTheRequestedPath()
    {
        var options = Options();
        var context = Context(options, htmx: true, currentUrl: "https://evil.example.net/phish");

        await options.Events.RedirectToLogin(context);

        Assert.Equal("/account/login?ReturnUrl=%2Fcustomers%2Frow%2F5", context.Response.Headers["HX-Redirect"].ToString());
    }

    [Fact]
    public async Task Login_NormalRequest_KeepsTheDefaultRedirect()
    {
        var options = Options();
        var context = Context(options, htmx: false);

        await options.Events.RedirectToLogin(context);

        Assert.Equal(302, context.Response.StatusCode);
        Assert.Equal("/account/login?ReturnUrl=%2Fcustomers%2Frow%2F5", context.Response.Headers.Location.ToString());
        Assert.False(context.Response.Headers.ContainsKey("HX-Redirect"));
    }

    [Fact]
    public async Task AccessDenied_HtmxRequest_IsAPlain403()
    {
        var options = Options();
        var context = Context(options, htmx: true);

        await options.Events.RedirectToAccessDenied(context);

        Assert.Equal(403, context.Response.StatusCode);
        Assert.Empty(context.Response.Headers.Location.ToString());
    }

    [Fact]
    public async Task Login_WrapsTheAppsOwnHandler()
    {
        var options = new CookieAuthenticationOptions { LoginPath = "/account/login" };
        var called = false;
        options.Events.OnRedirectToLogin = _ => { called = true; return Task.CompletedTask; };
        new HtmxCookieAuthentication().PostConfigure(null, options);

        await options.Events.RedirectToLogin(Context(options, htmx: true));
        Assert.False(called);

        await options.Events.RedirectToLogin(Context(options, htmx: false));
        Assert.True(called);
    }
}
