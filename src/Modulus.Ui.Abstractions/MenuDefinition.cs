namespace Modulus.Ui.Abstractions;

/// <summary>
/// Menu item definition for navigation.
/// </summary>
public class MenuItemDefinition
{
    public string? Id { get; set; }
    public string? Name { get; set; }
    public string? Label { get; set; }
    public string? Icon { get; set; }
    public string? Href { get; set; }
    public int? Order { get; set; }
    public List<MenuItemDefinition>? Children { get; set; } = new();
    public string? Permission { get; set; }
}

/// <summary>
/// Base class for menu contributions (like ABP's IMenuContributor).
/// </summary>
public interface IMenuContributor
{
    Task ConfigureMenuAsync(MenuConfigurationContext context);
}

/// <summary>
/// Context for menu configuration.
/// </summary>
public class MenuConfigurationContext
{
    public List<MenuItemDefinition> Items { get; set; } = new();
    public IServiceProvider ServiceProvider { get; set; } = null!;
}
