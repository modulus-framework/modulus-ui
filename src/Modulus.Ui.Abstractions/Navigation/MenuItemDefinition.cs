namespace Modulus.Ui.Abstractions.Navigation;

/// <summary>
/// A navigation entry. The same tree drives the Blazor ProLayout sider and the MVC sider,
/// so modules contribute menus once for every flavor.
/// </summary>
public class MenuItemDefinition
{
    public MenuItemDefinition(string key, string label, string? href = null, string? icon = null)
    {
        Key = key;
        Label = label;
        Href = href;
        Icon = icon;
    }

    /// <summary>Stable unique key; used for selection state.</summary>
    public string Key { get; }

    public string Label { get; set; }

    /// <summary>App-relative URL without a leading slash (e.g. <c>customers</c>), or null for a group.</summary>
    public string? Href { get; set; }

    /// <summary>Ant Design icon name (outline theme), e.g. <c>dashboard</c>, <c>team</c>.</summary>
    public string? Icon { get; set; }

    /// <summary>Lower sorts first. Items with equal order keep contribution order.</summary>
    public int Order { get; set; } = 1000;

    /// <summary>
    /// Policy or permission name. The flavor's menu renderer hides the item when
    /// the current user is not authorized. Null means always visible.
    /// </summary>
    public string? RequiredPolicy { get; set; }

    /// <summary>Match only the exact URL (for the dashboard / root entry).</summary>
    public bool ExactMatch { get; set; }

    /// <summary>
    /// More path prefixes that activate this item — e.g. a list link that should stay highlighted on
    /// its detail pages (<c>["apps/invoice/preview", "apps/invoice/edit"]</c>).
    /// </summary>
    public string[] ActivePaths { get; set; } = [];

    public List<MenuItemDefinition> Children { get; } = [];

    public MenuItemDefinition AddChild(MenuItemDefinition child)
    {
        Children.Add(child);
        return this;
    }

    /// <summary>True when <paramref name="path"/> (app-relative, no leading slash) activates this item.</summary>
    public bool Matches(string path)
    {
        if (Href is null) return false;
        var href = Href.Trim('/');
        path = path.Trim('/');
        foreach (var prefix in ActivePaths)
        {
            var p = prefix.Trim('/');
            if (path.Equals(p, StringComparison.OrdinalIgnoreCase) || path.StartsWith(p + "/", StringComparison.OrdinalIgnoreCase)) return true;
        }
        if (ExactMatch || href.Length == 0) return string.Equals(path, href, StringComparison.OrdinalIgnoreCase);
        return path.Equals(href, StringComparison.OrdinalIgnoreCase)
               || path.StartsWith(href + "/", StringComparison.OrdinalIgnoreCase);
    }
}
