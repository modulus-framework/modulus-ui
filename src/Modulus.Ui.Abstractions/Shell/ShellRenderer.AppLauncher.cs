using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.Ui.Abstractions.Shell;
public static partial class ShellRenderer
{
    // App launcher ------------------------------------------------------------------------------------

    private static bool HasLauncher(ShellContent c) => c.AppLauncher == AppLauncherStyle.Overlay && c.Apps is { Count: > 0 };

    /// <summary>
    /// Full-screen home menu for <see cref="AppLauncherStyle.Overlay"/>, as Odoo's: the apps as large icons in one
    /// centred grid, a group's apps kept together (<see cref="ShellApp.Group"/>). Typing shows a search box that
    /// filters the apps and lists matching menu entries. Empty for the dropdown launcher. Render it once, after the page.
    /// </summary>
    public static string AppLauncher(ShellView view)
    {
        var c = view.Content;
        if (!HasLauncher(c)) return "";
        var current = FindApp(view)?.App;
        var sb = new StringBuilder();
        sb.Append("""<div class="m-launcher" id="m-launcher" data-m-launcher hidden>""")
          .Append("""<div class="m-launcher-panel" role="dialog" aria-modal="true" aria-label="Apps"><div class="m-launcher-inner">""")
          .Append(Brand(view.Options, "m-launcher-brand"))
          .Append("""<button type="button" class="m-icon-btn m-launcher-close" data-m-launcher-close aria-label="Close">""").Append(Icon("close")).Append("</button>")
          // Hidden until the user types, as in Odoo; it still takes focus so typing lands in it.
          .Append("""<div class="m-launcher-search">""").Append(Icon("search"))
          .Append("""<input type="search" class="m-launcher-input" data-m-launcher-search placeholder="Search apps and menus…" aria-label="Search apps and menus" autocomplete="off" spellcheck="false" />""")
          .Append("""<kbd class="m-launcher-esc" aria-hidden="true">Esc</kbd></div>""")
          .Append("""<div class="m-launcher-grid">""");
        var index = 0;
        foreach (var app in c.Apps!.GroupBy(a => a.Group).SelectMany(g => g))
        {
            // An icon with the name under it; the description is the tooltip. --m-i staggers the icons' entrance.
            var active = app == current;
            sb.Append($"""<a class="m-launcher-app{(active ? " active" : "")}" href="{Url(app.Href)}" style="--m-app-tone: var(--m-{E(app.Tone)}); --m-i: {index++}" data-m-launcher-name="{E((app.Name + " " + app.Description + " " + app.Group).Trim())}"{(string.IsNullOrEmpty(app.Description) ? "" : $" title=\"{E(app.Description)}\"")}{(active ? " aria-current=\"true\"" : "")}>""")
              .Append(AppMark(app, "lg"))
              .Append($"""<span class="m-launcher-name">{E(app.Name)}</span></a>""");
        }
        sb.Append("""</div><div class="m-launcher-empty" hidden>No apps match.</div>""")
          .Append("""<div class="m-launcher-menus" hidden><div class="m-launcher-results"></div></div>""")
          .Append("</div></div></div>");
        return sb.ToString();
    }

    /// <summary>The app whose menu holds the current page (the deepest one when app menus nest), or null.</summary>
    public static ShellApp? CurrentApp(ShellView view) => FindApp(view)?.App;

    private static (ShellApp App, MenuItemDefinition Menu)? FindApp(ShellView view)
    {
        if (view.Content.Apps is not { Count: > 0 } apps) return null;
        (ShellApp, MenuItemDefinition)? best = null;
        var bestDepth = -1;
        foreach (var app in apps)
        {
            if (app.MenuKey is null || Find(view.Menu, app.MenuKey, 0) is not var (node, depth)) continue;
            if (depth > bestDepth && IsActive(node, view.Path)) (best, bestDepth) = ((app, node), depth);
        }
        return best;

        static (MenuItemDefinition, int)? Find(IEnumerable<MenuItemDefinition> items, string key, int depth)
        {
            foreach (var item in items)
            {
                if (item.Key == key) return (item, depth);
                if (Find(item.Children, key, depth + 1) is { } found) return found;
            }
            return null;
        }
    }

    /// <summary>An app's icon on a solid tile of its tone.</summary>
    private static string AppMark(ShellApp app, string size = "sm") =>
        $"""<span class="m-app-mark m-app-mark-{size} m-tile-{E(app.Tone)}">{Icon(app.Icon)}</span>""";

}
