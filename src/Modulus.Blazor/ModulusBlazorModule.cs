using AntDesign.ProLayout;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Shell;
using Modulus.Ui.Abstractions.State;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.Blazor;

/// <summary>
/// Modulus Blazor UI module — the single entry point for a Blazor host.
/// </summary>
public static class ModulusBlazorModule
{
    /// <summary>
    /// Registers Ant Design Blazor, the official Pro layout, Modulus theming and the menu pipeline.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddModulusBlazor(theme =>
    /// {
    ///     theme.Title = "Contoso ERP";
    ///     theme.PrimaryColor = "#722ed1";
    /// });
    /// builder.Services.AddTransient&lt;IMenuContributor, AppMenuContributor&gt;();
    /// </code>
    /// </example>
    public static IServiceCollection AddModulusBlazor(
        this IServiceCollection services,
        Action<ModulusThemeOptions>? configure = null)
    {
        var options = services.AddOptions<ModulusThemeOptions>();
        if (configure is not null)
            options.Configure(configure);

        services.AddAntDesign();

        // Drive the official ProLayout from the same options every flavor uses.
        services.AddOptions<ProSettings>().Configure<IOptions<ModulusThemeOptions>>((pro, theme) =>
        {
            var t = theme.Value;
            pro.Title = t.Title;
            pro.NavTheme = t.NavTheme == NavTheme.Dark ? "dark" : "light";
            pro.Layout = "side";
            pro.ContentWidth = "Fluid";
            pro.FixedHeader = true;
            pro.FixSiderbar = true;
            pro.HeaderHeight = t.HeaderHeight;
            pro.MenuHeaderRender = true;
            pro.FooterRender = true;
            pro.PrimaryColor = t.PrimaryColor;
        });

        services.TryAddScoped<IMenuProvider, MenuProvider>();
        services.TryAddSingleton<IShellContentProvider, DefaultShellContentProvider>();

        // Per-user UI state (saved views); an app registers its own IUiStateStore to keep it across restarts.
        services.TryAddSingleton<IUiStateStore, MemoryUiStateStore>();
        services.TryAddScoped<SavedViews>();

        return services;
    }
}
