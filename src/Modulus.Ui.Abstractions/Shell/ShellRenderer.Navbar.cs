using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.Ui.Abstractions.Shell;
public static partial class ShellRenderer
{
    // Horizontal navbar ---------------------------------------------------------------------------------

    public static string Navbar(ShellView view)
    {
        var app = FindApp(view);
        var sb = new StringBuilder();
        sb.Append($"""<nav class="m-navbar m-nav-all{(app is null ? "" : " m-has-app")}" aria-label="Main navigation"><div class="m-container"><ul class="m-hnav">""");
        foreach (var item in view.Menu) NavbarItem(sb, item, view.Path, icons: true);
        sb.Append("</ul></div></nav>");

        if (app is var (shellApp, menu))
        {
            // The module bar (Odoo's app menu, SAP's shell): tinted with the app's tone, a module switcher in place
            // of the app's name, the app's menus, and where the page sits in them.
            sb.Append($"""<nav class="m-navbar m-nav-app" aria-label="{E(shellApp.Name)}" style="--m-app-tone: var(--m-{E(shellApp.Tone)})"><div class="m-container"><ul class="m-hnav">""");
            ModuleSwitcher(sb, view, shellApp);
            foreach (var item in menu.Children) NavbarItem(sb, item, view.Path, icons: false);
            sb.Append("</ul>");
            if (view.Options.ShowAppTrail) sb.Append(ModuleTrail(view));
            sb.Append("</div></nav>");
        }
        return sb.ToString();
    }

    // The app, then the sections down to the page on screen, as a breadcrumb trail; empty when no app or menu item matches.
    private static string ModuleTrail(ShellView view)
    {
        var app = FindApp(view);
        if (app is not var (shellApp, menu) || Breadcrumb(menu, view.Path) is not { Count: > 0 } trail) return "";
        // The app first, then the sections and the page; the page is the current level and is not a link.
        // Each level opens its related pages: the app level lists the other apps, a section or page lists its siblings under
        // the same parent (the app's menu for the first level), with the page on screen marked.
        var otherApps = (view.Content.Apps ?? []).Where(a => a != shellApp)
            .Select(a => new Crumb(a.Name, Url(a.Href), a.Icon)).ToList();
        var crumbs = new List<Crumb> { new(shellApp.Name, Url(shellApp.Href), shellApp.Icon, otherApps, RelatedTitle: "Apps") };
        for (var i = 0; i < trail.Count; i++)
        {
            var item = trail[i];
            var parent = i == 0 ? menu : trail[i - 1];
            // A group opens its first page; the group that holds the page on screen is the selected one.
            var siblings = parent.Children.Select(c => new Crumb(c.Label, FirstPage(c) is { } href ? Url(href) : null, c.Icon,
                Selected: ReferenceEquals(c, item) || Holds(c, item))).ToList();
            crumbs.Add(new Crumb(item.Label, i == trail.Count - 1 || item.Href is null ? null : Url(item.Href), item.Icon,
                siblings, RelatedTitle: parent.Label));
        }
        return CrumbHtml.Trail(crumbs);
    }

    // The page an item leads to: its own link, or the first link under it (a group has none of its own).
    private static string? FirstPage(MenuItemDefinition item)
    {
        if (item.Href is not null) return item.Href;
        foreach (var child in item.Children)
            if (FirstPage(child) is { } href) return href;
        return null;
    }

    // Whether the item holds the given one anywhere below it.
    private static bool Holds(MenuItemDefinition item, MenuItemDefinition target) =>
        item.Children.Any(c => ReferenceEquals(c, target) || Holds(c, target));

    // The app's name opens a list of every app to jump to (SAP's product switch), and the full launcher.
    private static void ModuleSwitcher(StringBuilder sb, ShellView view, ShellApp current)
    {
        sb.Append($"""<li class="m-hnav-app"><details class="m-dropdown m-module-switch"><summary class="m-hnav-app-link" aria-label="{E(current.Name)}, switch module">""")
          .Append(AppMark(current)).Append($"""<span class="m-hnav-app-name">{E(current.Name)}</span>""").Append(Icon("down", "m-hnav-app-chevron"))
          .Append("</summary>");
        ModuleMenu(sb, view, current);
        sb.Append("</details></li>");
    }

    // Every app to jump to, filed under its group: two columns and a filter box once there are more than a few,
    // the body scrolling under a fixed head and foot, then the full launcher and the "View all apps" page.
    private static void ModuleMenu(StringBuilder sb, ShellView view, ShellApp current)
    {
        var apps = view.Content.Apps!;
        var many = apps.Count > AppFilterThreshold;
        sb.Append($"""<div class="m-dropdown-menu m-module-menu{(many ? " m-module-menu-wide" : "")}" data-m-app-menu>""")
          .Append($"""<div class="m-app-menu-head"><span class="m-app-menu-title">Switch app</span>{AppCount(apps)}</div>""");
        if (many) AppFilter(sb);
        sb.Append("""<div class="m-app-menu-body">""");
        foreach (var group in apps.GroupBy(a => a.Group))
        {
            AppGroupStart(sb, group.Key, apps);
            sb.Append("""<div class="m-module-grid">""");
            foreach (var app in group)
            {
                var active = app == current;
                sb.Append($"""<a class="m-module-item{(active ? " active" : "")}" href="{Url(app.Href)}" style="--m-app-tone: var(--m-{E(app.Tone)})"{(active ? " aria-current=\"true\"" : "")}>""")
                  .Append(AppMark(app))
                  .Append($"""<span class="m-module-text"><span class="m-module-name">{E(app.Name)}</span>""")
                  .Append(string.IsNullOrEmpty(app.Description) ? "" : $"""<span class="m-module-desc">{E(app.Description)}</span>""")
                  .Append("</span>").Append(active ? Icon("check", "m-module-check") : "").Append("</a>");
            }
            sb.Append("</div></section>");
        }
        sb.Append("""</div><div class="m-app-menu-empty" hidden>No apps match.</div>""");
        var launcher = HasLauncher(view.Content);
        if (launcher || view.Content.AppsHref is not null)
        {
            sb.Append("""<div class="m-app-menu-foot">""");
            if (launcher)
                sb.Append("""<button type="button" class="m-dropdown-item" data-m-launcher-open aria-controls="m-launcher">""").Append(Icon("tb:grid-dots")).Append("All apps</button>");
            if (view.Content.AppsHref is { } all)
                sb.Append($"""<a class="m-dropdown-item" href="{Url(all)}">""").Append(Icon("tb:grid-dots")).Append("View all apps</a>");
            sb.Append("</div>");
        }
        sb.Append("</div>");
    }

    // Menu items from the app's menu down to the page on screen (section, then link), empty when none matches.
    private static List<MenuItemDefinition> Breadcrumb(MenuItemDefinition menu, string path)
    {
        foreach (var item in menu.Children)
        {
            if (item.Children.Count == 0)
            {
                if (item.Matches(path)) return [item];
                continue;
            }
            if (Breadcrumb(item, path) is { Count: > 0 } below) return [item, .. below];
        }
        return [];
    }

    // icons: false for an app's own menu, which reads as text after the app's name (Odoo).
    private static void NavbarItem(StringBuilder sb, MenuItemDefinition item, string path, bool icons)
    {
        var active = IsActive(item, path);
        var icon = icons && item.Icon is not null ? Icon(item.Icon, "m-hnav-icon") : "";
        if (item.Children.Count == 0)
        {
            sb.Append($"""<li class="m-hnav-item{(active ? " active" : "")}"><a class="m-hnav-link" href="{Href(item)}"{(active ? " aria-current=\"page\"" : "")}>""")
              .Append(icon)
              .Append($"""<span>{E(item.Label)}</span></a></li>""");
            return;
        }

        sb.Append($"""<li class="m-hnav-item{(active ? " active" : "")}"><details class="m-dropdown"><summary class="m-hnav-link">""")
          .Append(icon)
          .Append($"""<span>{E(item.Label)}</span>""").Append(Icon("down", "m-hnav-chevron"))
          .Append("""</summary><div class="m-dropdown-menu m-tree">""");
        DropdownItems(sb, item, path);
        sb.Append("</div></details></li>");
    }

    // One narrow column, as in Odoo: a group directly in the dropdown is a section (its name in bold, its entries
    // indented under it); a group below that folds open in place, to any depth, opened already when it holds the
    // page on screen.
    private static void DropdownItems(StringBuilder sb, MenuItemDefinition group, string path, int depth = 0)
    {
        foreach (var child in group.Children)
        {
            if (child.Children.Count == 0)
            {
                DropdownItem(sb, child, path);
                continue;
            }
            if (depth == 0)
            {
                sb.Append($"""<div class="m-menu-section{(IsActive(child, path) ? " m-in-path" : "")}" role="group" aria-label="{E(child.Label)}"><div class="m-menu-section-title" aria-hidden="true">""")
                  .Append(child.Icon is null ? "" : Icon(child.Icon))
                  .Append($"""{E(child.Label)}</div>""");
                DropdownItems(sb, child, path, 1);
                sb.Append("</div>");
                continue;
            }
            var open = IsActive(child, path);
            sb.Append($"""<details class="m-tree-group{(open ? " m-in-path" : "")}"{(open ? " open" : "")}><summary class="m-dropdown-item">""")
              .Append(child.Icon is null ? "" : Icon(child.Icon))
              .Append($"""<span class="m-tree-label">{E(child.Label)}</span>""").Append(Icon("down", "m-tree-chevron"))
              .Append($"""</summary><div class="m-tree-sub" style="--m-depth:{depth + 1}">""");
            DropdownItems(sb, child, path, depth + 1);
            sb.Append("</div></details>");
        }
    }

    private static void DropdownItem(StringBuilder sb, MenuItemDefinition item, string path)
    {
        var active = item.Matches(path);
        sb.Append($"""<a class="m-dropdown-item{(active ? " active" : "")}" href="{Href(item)}"{(active ? " aria-current=\"page\"" : "")}>""")
          .Append(item.Icon is null ? "" : Icon(item.Icon))
          .Append(E(item.Label)).Append("</a>");
    }

}
