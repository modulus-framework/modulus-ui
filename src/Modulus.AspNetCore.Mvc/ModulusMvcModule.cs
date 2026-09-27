using Microsoft.AspNetCore.Authentication.Cookies;
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
}
