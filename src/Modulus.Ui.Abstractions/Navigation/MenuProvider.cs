namespace Modulus.Ui.Abstractions.Navigation;

/// <summary>
/// Implemented by each module to add items to the main menu (ABP-style contributor).
/// Register with <c>services.AddTransient&lt;IMenuContributor, MyMenuContributor&gt;()</c>.
/// </summary>
public interface IMenuContributor
{
    Task ConfigureMenuAsync(MenuConfigurationContext context);
}

public sealed class MenuConfigurationContext(IServiceProvider services)
{
    public IServiceProvider Services { get; } = services;

    public List<MenuItemDefinition> Items { get; } = [];

    public MenuConfigurationContext Add(MenuItemDefinition item)
    {
        Items.Add(item);
        return this;
    }
}

public interface IMenuProvider
{
    Task<IReadOnlyList<MenuItemDefinition>> GetMainMenuAsync();
}

/// <summary>Runs every registered <see cref="IMenuContributor"/> and returns the sorted tree.</summary>
public sealed class MenuProvider(IEnumerable<IMenuContributor> contributors, IServiceProvider services) : IMenuProvider
{
    public async Task<IReadOnlyList<MenuItemDefinition>> GetMainMenuAsync()
    {
        var context = new MenuConfigurationContext(services);
        foreach (var contributor in contributors)
            await contributor.ConfigureMenuAsync(context);
        Sort(context.Items);
        return context.Items;
    }

    private static void Sort(List<MenuItemDefinition> items)
    {
        // OrderBy is stable, so equal Order values keep contribution order.
        var sorted = items.OrderBy(i => i.Order).ToList();
        items.Clear();
        items.AddRange(sorted);
        foreach (var item in items)
            Sort(item.Children);
    }
}
