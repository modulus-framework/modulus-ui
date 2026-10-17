using Modulus.Ui.Abstractions.Navigation;

namespace ContosoMvc.Menu;

/// <summary>Adds the app's pages to the menu. Order decides the position; RequiredPolicy hides an item from users who lack it.</summary>
public sealed class AppMenuContributor : IMenuContributor
{
    public Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        // Href is relative to the site root, without a leading slash. "" is the home page.
        context.Add(new MenuItemDefinition("dashboard", "Dashboard", "", "dashboard") { Order = 0, ExactMatch = true })
               .Add(new MenuItemDefinition("customers", "Customers", "customers", "team") { Order = 10 });

        return Task.CompletedTask;
    }
}
