namespace Modulus.Ui.Abstractions.Navigation;

/// <summary>
/// One entry of the application menu as the server serves it (<c>GET /api/navigation/menu</c>, the framework's <c>NavigationNode</c>).
/// The server has already removed what the caller may not see and applied the company's overrides, so the renderer shows the tree as
/// it arrives. <paramref name="Url"/> is null for a group.
/// </summary>
public sealed record ServerMenuNode(string Key, string Title, string? Url, string? Icon, IReadOnlyList<ServerMenuNode> Children);

/// <summary>Turns the server's menu into the menu model every renderer reads.</summary>
public static class ServerMenu
{
    /// <summary>
    /// The item for one node: the server's URL becomes an app-relative path (no leading slash), the server's order among siblings is
    /// kept, and the children are converted the same way. A server URL of <c>/</c> is the application root, matched exactly.
    /// </summary>
    public static MenuItemDefinition ToDefinition(ServerMenuNode node, int order = 0)
    {
        ArgumentNullException.ThrowIfNull(node);
        var href = node.Url?.TrimStart('/');
        var item = new MenuItemDefinition(node.Key, node.Title, href, node.Icon)
        {
            Order = order,
            ExactMatch = node.Url == "/",
        };
        for (var i = 0; i < node.Children.Count; i++)
            item.AddChild(ToDefinition(node.Children[i], i));
        return item;
    }

    /// <summary>The items for a whole menu, in the server's order.</summary>
    public static IReadOnlyList<MenuItemDefinition> ToDefinitions(IReadOnlyList<ServerMenuNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        return nodes.Select((node, i) => ToDefinition(node, i)).ToList();
    }
}

/// <summary>
/// Adds the server's menu to the main menu. The host supplies how the menu is fetched (for example through its BFF client, with the
/// signed-in person's session), so this contributor makes no assumption about the transport. A failed fetch is not swallowed: an empty
/// menu would look like a permission problem to the person.
/// </summary>
public sealed class ServerMenuContributor(Func<CancellationToken, Task<IReadOnlyList<ServerMenuNode>>> fetch) : IMenuContributor
{
    private readonly Func<CancellationToken, Task<IReadOnlyList<ServerMenuNode>>> _fetch = fetch ?? throw new ArgumentNullException(nameof(fetch));

    /// <inheritdoc />
    public async Task ConfigureMenuAsync(MenuConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        foreach (var item in ServerMenu.ToDefinitions(await _fetch(CancellationToken.None)))
            context.Add(item);
    }
}
