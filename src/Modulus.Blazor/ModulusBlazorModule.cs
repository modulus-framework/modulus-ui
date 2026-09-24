using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Core.Theme;

namespace Modulus.Blazor;

/// <summary>
/// Modulus Blazor UI module.
/// Registers all UI services, theme providers, and component defaults.
/// </summary>
public static class ModulusBlazorModule
{
    /// <summary>
    /// Add Modulus Blazor UI to the DI container.
    /// </summary>
    public static IServiceCollection AddModulusBlazor(
        this IServiceCollection services,
        ModulusThemeOptions? options = null)
    {
        options ??= new ModulusThemeOptions();

        // Register options
        services.AddSingleton(options);

        // Register theme provider (scoped for component injection)
        services.AddScoped<ThemeProvider>();

        // Add AntDesign (via the NuGet package)
        services.AddAntDesign();

        // Register component defaults (to be extended)
        services.AddScoped<ModulusComponentDefaults>();

        return services;
    }
}

/// <summary>
/// Component defaults and shared configuration.
/// Inject this to customize component behaviors across the app.
/// </summary>
public class ModulusComponentDefaults
{
    private readonly ModulusThemeOptions _options;

    public ModulusComponentDefaults(ModulusThemeOptions options)
    {
        _options = options;
    }

    /// <summary>
    /// Get the default button size (e.g. "middle", "large", "small").
    /// </summary>
    public string DefaultButtonSize => _options.CompactMode ? "small" : "middle";

    /// <summary>
    /// Get the default button type (e.g. "default", "primary").
    /// </summary>
    public string DefaultButtonType => "default";

    /// <summary>
    /// Get the default form layout.
    /// </summary>
    public string DefaultFormLayout => "vertical";

    /// <summary>
    /// Get the default table size ("small", "middle", "large").
    /// </summary>
    public string DefaultTableSize => _options.CompactMode ? "small" : "middle";

    /// <summary>
    /// Get the default page size for pagination.
    /// </summary>
    public int DefaultPageSize => 20;
}
