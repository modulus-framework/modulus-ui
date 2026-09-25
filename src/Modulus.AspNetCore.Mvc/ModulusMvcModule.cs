using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Shell;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.AspNetCore.Mvc;

public static class ModulusMvcModule
{
    /// <summary>
    /// Registers Modulus for MVC and Razor Pages: theme options, the shared menu pipeline and
    /// the antiforgery header htmx sends. Add <c>@addTagHelper *, Modulus.AspNetCore.Mvc</c>
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

        return services;
    }
}
