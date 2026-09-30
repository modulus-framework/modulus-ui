using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Shell;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.AspNetCore.Mvc;

public static class ModulusMvcModule
{
    /// <summary>
    /// Registers Modulus for MVC and Razor Pages: theme options, the shared menu pipeline, the
    /// antiforgery header htmx sends and global antiforgery validation. Add <c>@addTagHelper *, Modulus.AspNetCore.Mvc</c>
    /// to _ViewImports and use <c>Layout = "_ModulusLayout"</c>.
    /// </summary>
    public static IServiceCollection AddModulusMvc(
        this IServiceCollection services,
        Action<ModulusThemeOptions>? configure = null)
    {
        var options = services.AddOptions<ModulusThemeOptions>();
        if (configure is not null)
            options.Configure(configure);

        services.TryAddScoped<IMenuProvider, MenuProvider>();
        services.TryAddSingleton<IShellContentProvider, DefaultShellContentProvider>();

        // htmx requests carry the token in this header (see _ModulusLayout: hx-headers on <body>).
        services.AddAntiforgery(o => o.HeaderName = Htmx.HtmxExtensions.AntiforgeryHeader);

        // Validate that token on every unsafe request (POST, PUT, PATCH, DELETE) to any controller, not only
        // those that opt in. Razor Pages already does this; opt an action out with [IgnoreAntiforgeryToken].
        services.Configure<MvcOptions>(o => o.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

        // An expired session sends htmx to the sign-in page as a full navigation instead of swapping
        // the sign-in page into the fragment's target (any cookie scheme, including Identity).
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IPostConfigureOptions<CookieAuthenticationOptions>, Htmx.HtmxCookieAuthentication>());

        return services;
    }

    /// <summary>
    /// Sets each request's culture from <see cref="ModulusThemeOptions.Cultures"/> (the first is the default): the culture cookie the
    /// theme settings panel writes, then <c>?culture=</c>, then the browser's Accept-Language. Call it before routing. Does nothing
    /// when no cultures are configured.
    /// </summary>
    public static IApplicationBuilder UseModulusLocalization(this IApplicationBuilder app)
    {
        var theme = app.ApplicationServices.GetRequiredService<IOptions<ModulusThemeOptions>>().Value;
        return LocalizationOptions(theme.Cultures) is { } options ? app.UseRequestLocalization(options) : app;
    }

    /// <summary>Request localization for the given cultures (the first is the default), with the cookie first; null when none exist.</summary>
    public static RequestLocalizationOptions? LocalizationOptions(IEnumerable<string>? cultures)
    {
        var supported = ModulusCulture.Supported(cultures);
        if (supported.Count == 0) return null;
        var options = new RequestLocalizationOptions
        {
            DefaultRequestCulture = new RequestCulture(supported[0]),
            SupportedCultures = [.. supported],
            SupportedUICultures = [.. supported],
            FallBackToParentCultures = true,
            FallBackToParentUICultures = true,
        };
        // Default order is query string, cookie, Accept-Language; the explicit choice (cookie) wins over a stale link.
        var cookie = options.RequestCultureProviders.OfType<CookieRequestCultureProvider>().First();
        options.RequestCultureProviders.Remove(cookie);
        options.RequestCultureProviders.Insert(0, cookie);
        return options;
    }
}
