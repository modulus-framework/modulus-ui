using Modulus.Blazor;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Ui.Abstractions.Admin;
using Modulus.Ui.Abstractions.Navigation;
using Xunit;

namespace Modulus.Blazor.Tests;

public class AdminModuleTests
{
    private static async Task<List<MenuItemDefinition>> Menu(Action<AdminModuleOptions>? configure = null)
    {
        var services = new ServiceCollection().AddModulusAdminUi(configure).BuildServiceProvider();
        var context = new MenuConfigurationContext(services);
        foreach (var c in services.GetServices<IMenuContributor>()) await c.ConfigureMenuAsync(context);
        return context.Items;
    }

    private static IEnumerable<MenuItemDefinition> Flatten(IEnumerable<MenuItemDefinition> items) =>
        items.SelectMany(i => new[] { i }.Concat(Flatten(i.Children)));

    [Fact]
    public async Task AllPages_GivesTheFrameworkAppsInOrder()
    {
        var menu = await Menu();

        Assert.Equal(["admin-overview", "admin-settings-app", "admin-system", "admin-account"], menu.Select(i => i.Key));
        Assert.Equal(menu.Select(i => i.Order).OrderBy(o => o), menu.Select(i => i.Order));
        var settings = menu.Single(i => i.Key == "admin-settings-app");
        Assert.Equal(["admin-settings", "admin-access", "admin-organisation", "admin-audit"], settings.Children.Select(i => i.Key));
        Assert.Equal(["admin-users", "admin-roles"], settings.Children[1].Children.Select(i => i.Key));
    }

    [Fact]
    public async Task OnlySelectedPages_AreLinked_AndEmptyGroupsAreDropped()
    {
        var menu = await Menu(o => o.Pages = AdminPages.Dashboard | AdminPages.Users);

        Assert.Equal(["admin-overview", "admin-settings-app"], menu.Select(i => i.Key));
        Assert.Equal(["admin-dashboard", "admin-users"], Flatten(menu).Where(i => i.Href is not null).Select(i => i.Key));
    }

    [Fact]
    public async Task NoPages_GivesAnEmptyMenu() => Assert.Empty(await Menu(o => o.Pages = AdminPages.None));

    [Fact]
    public async Task Policy_AppliesToSettingsAndSystemOnly()
    {
        var menu = await Menu(o => o.AdministrationPolicy = "admin");

        Assert.All(menu.Where(i => i.Key is "admin-settings-app" or "admin-system"), i => Assert.Equal("admin", i.RequiredPolicy));
        Assert.All(menu.Where(i => i.Key is "admin-overview" or "admin-account"), i => Assert.Null(i.RequiredPolicy));
    }

    [Fact]
    public async Task Hrefs_AreTheScaffoldRoutes_AndKeysAreUnique()
    {
        var all = Flatten(await Menu()).ToList();

        Assert.Equal(all.Count, all.Select(i => i.Key).Distinct().Count());
        var hrefs = all.Where(i => i.Href is not null).Select(i => i.Href!).ToHashSet();
        Assert.Superset(new HashSet<string> { "dashboard", "admin/users", "admin/roles", "admin/tenants", "admin/settings", "audit",
            "analytics", "reports", "system/health", "account/profile", "notifications", "account/notification-preferences" }, hrefs);
    }

    [Fact]
    public async Task FirstOrder_ShiftsEveryGroup()
    {
        var menu = await Menu(o => o.FirstOrder = 100);

        Assert.All(menu, i => Assert.InRange(i.Order, 100, 130));
    }

    [Fact]
    public async Task AddModulusAdminUi_CoexistsWithOtherContributors()
    {
        var services = new ServiceCollection().AddModulusAdminUi().AddTransient<IMenuContributor, Other>().BuildServiceProvider();
        var context = new MenuConfigurationContext(services);
        foreach (var c in services.GetServices<IMenuContributor>()) await c.ConfigureMenuAsync(context);

        Assert.Contains(context.Items, i => i.Key == "other");
        Assert.Contains(context.Items, i => i.Key == "admin-settings-app");
    }

    private sealed class Other : IMenuContributor
    {
        public Task ConfigureMenuAsync(MenuConfigurationContext context)
        {
            context.Add(new MenuItemDefinition("other", "Other", "other"));
            return Task.CompletedTask;
        }
    }
}
