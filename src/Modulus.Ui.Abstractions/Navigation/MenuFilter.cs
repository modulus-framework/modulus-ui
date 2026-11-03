namespace Modulus.Ui.Abstractions.Navigation;

public static class MenuFilter
{
    /// <summary>
    /// Returns a copy of the menu without items whose <see cref="MenuItemDefinition.RequiredPolicy"/> the user
    /// fails, and without groups left empty. <paramref name="authorize"/> checks one policy name.
    /// </summary>
    public static async Task<IReadOnlyList<MenuItemDefinition>> FilterAsync(IEnumerable<MenuItemDefinition> items, Func<string, Task<bool>> authorize)
    {
        var result = new List<MenuItemDefinition>();
        foreach (var item in items)
        {
            if (item.RequiredPolicy is { } policy && !await authorize(policy)) continue;
            var copy = new MenuItemDefinition(item.Key, item.Label, item.Href, item.Icon)
            {
                Order = item.Order,
                RequiredPolicy = item.RequiredPolicy,
                ExactMatch = item.ExactMatch,
                ActivePaths = item.ActivePaths,
            };
            if (item.Children.Count > 0)
            {
                copy.Children.AddRange(await FilterAsync(item.Children, authorize));
                if (copy.Children.Count == 0 && item.Href is null) continue;
            }
            result.Add(copy);
        }
        return result;
    }
}
