using AntDesign.ProLayout;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Modulus.Ui.Abstractions.Admin;
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

        // Browser-side same-origin requests (cookies + antiforgery), for the account and permission pages.
        services.TryAddScoped<ModulusHttp>();

        // Sample data behind the scaffolded admin pages (users, roles, tenants, notifications, audit, settings, metrics). Replace any
        // of them by registering your own implementation of the interface; the page then stops saying "Sample data".
        services.TryAddSingleton<SampleAdminData>();
        services.TryAddSingleton<IUserDirectory>(sp => sp.GetRequiredService<SampleAdminData>());
        services.TryAddSingleton<IRoleDirectory>(sp => sp.GetRequiredService<SampleAdminData>());
        services.TryAddSingleton<ITenantDirectory>(sp => sp.GetRequiredService<SampleAdminData>());
        services.TryAddSingleton<INotificationCenter>(sp => sp.GetRequiredService<SampleAdminData>());
        services.TryAddSingleton<IAuditTrail>(sp => sp.GetRequiredService<SampleAdminData>());
        services.TryAddSingleton<IAdminSettingsStore>(sp => sp.GetRequiredService<SampleAdminData>());
        services.TryAddSingleton<IMetricsSource>(sp => sp.GetRequiredService<SampleAdminData>());

        return services;
    }

    /// <summary>
    /// Adds the premade admin module navigation (Overview, Settings, System, My account), in this framework's own menu style.
    /// Pass the pages you scaffolded so the menu only links to pages that exist.
    /// </summary>
    /// <example>
    /// <code>
    /// builder.Services.AddModulusAdminUi(o => o.AdministrationPolicy = "admin");
    /// </code>
    /// </example>
    public static IServiceCollection AddModulusAdminUi(this IServiceCollection services, Action<AdminModuleOptions>? configure = null)
    {
        var options = new AdminModuleOptions();
        configure?.Invoke(options);
        services.AddSingleton(options);
        services.AddTransient<IMenuContributor, AdminMenuContributor>();
        return services;
    }
}
