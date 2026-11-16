using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.Ui.Abstractions.Shell;
public static partial class ShellRenderer
{
    // Sidebar (vertical layout, and the navigation drawer on small screens) ------------------------

    public static string Sidebar(ShellView view)
    {
        var app = FindApp(view);
        var sb = new StringBuilder();
        sb.Append("""<aside class="m-sidebar" id="m-sidebar" aria-label="Main navigation">""");
        sb.Append("""<div class="m-sidebar-brand">""").Append(Brand(view.Options)).Append("""<button type="button" class="m-icon-btn m-sidebar-close" data-m-nav-close aria-label="Close navigation">""").Append(Icon("close")).Append("</button></div>");
        sb.Append($"""<nav class="m-sidebar-nav m-nav-all{(app is null ? "" : " m-has-app")}"><ul class="m-snav">""");
        foreach (var item in view.Menu)
            SidebarItem(sb, item, view.Path, "");
        sb.Append("</ul></nav>");

        if (app is var (shellApp, menu))
        {
            // The module switcher sits above the scrolling menu so its list is not clipped by it.
            var tone = $"style=\"--m-app-tone: var(--m-{E(shellApp.Tone)})\"";
            sb.Append($"""<div class="m-sidebar-module m-nav-app" {tone}><details class="m-dropdown m-module-switch"><summary class="m-sidebar-app" aria-label="{E(shellApp.Name)}, switch module">""")
              .Append(AppMark(shellApp))
              .Append($"""<span class="m-sidebar-app-text"><span class="m-sidebar-app-name">{E(shellApp.Name)}</span>""")
              .Append(string.IsNullOrEmpty(shellApp.Description) ? "" : $"""<span class="m-sidebar-app-desc">{E(shellApp.Description)}</span>""")
              .Append("</span>").Append(Icon("tb:selector", "m-sidebar-app-chevron")).Append("</summary>");
            ModuleMenu(sb, view, shellApp);
            sb.Append("</details></div>");
            sb.Append($"""<nav class="m-sidebar-nav m-nav-app" aria-label="{E(shellApp.Name)}" {tone}>""");
            sb.Append("""<ul class="m-snav">""");
            foreach (var child in menu.Children)
            {
                if (child.Children.Count == 0)
                {
                    SidebarItem(sb, child, view.Path, "");
                    continue;
                }
                sb.Append($"""<li class="m-snav-header m-snav-header-top">{E(child.Label)}</li>""");
                foreach (var grandchild in child.Children) SidebarItem(sb, grandchild, view.Path, "");
            }
            sb.Append("</ul></nav>");
        }
        sb.Append("</aside>");
        sb.Append("""<div class="m-sidebar-backdrop" data-m-nav-close></div>""");
        return sb.ToString();
    }

    // trail: labels of the groups above the item ("Apps › eCommerce"), which search shows next to a result.
    private static void SidebarItem(StringBuilder sb, MenuItemDefinition item, string path, string trail)
    {
        if (item.Children.Count == 0)
        {
            var active = item.Matches(path);
            sb.Append($"""<li class="m-snav-item{(active ? " active" : "")}"><a class="m-snav-link" href="{Href(item)}"{Trail(trail)}{(active ? " aria-current=\"page\"" : "")}>""")
              .Append(SnavIcon(item))
              .Append($"""<span class="m-snav-title">{E(item.Label)}</span></a></li>""");
            return;
        }

        var open = IsActive(item, path);
        sb.Append($"""<li class="m-snav-item{(open ? " active" : "")}"><details class="m-snav-group"{(open ? " open" : "")}><summary class="m-snav-link">""")
          .Append(SnavIcon(item))
          .Append($"""<span class="m-snav-title">{E(item.Label)}</span>""").Append(Icon("down", "m-snav-chevron"))
          .Append($"""</summary><ul class="m-snav-sub"><li class="m-snav-flyout-title" aria-hidden="true">{E(item.Label)}</li>""");
        SidebarChildren(sb, item, path, Join(trail, item.Label), 0);
        sb.Append("</ul></details></li>");
    }

    // A group's entries in the sidebar tree. A child with children is a branch that folds open in place, to any
    // depth (Odoo's and SAP's navigation trees); depth indents it (--m-depth on its list).
    private static void SidebarChildren(StringBuilder sb, MenuItemDefinition group, string path, string trail, int depth)
    {
        foreach (var child in group.Children)
        {
            if (child.Children.Count == 0)
            {
                SidebarLeaf(sb, child, path, trail);
                continue;
            }
            var open = IsActive(child, path);
            sb.Append($"""<li class="m-snav-item m-snav-branch{(open ? " m-in-path" : "")}"><details class="m-snav-group"{(open ? " open" : "")}><summary class="m-snav-link">""")
              .Append($"""<span class="m-snav-title">{E(child.Label)}</span>""").Append(Icon("down", "m-snav-chevron"))
              .Append($"""</summary><ul class="m-snav-sub" style="--m-depth:{depth + 1}">""");
            SidebarChildren(sb, child, path, Join(trail, child.Label), depth + 1);
            sb.Append("</ul></details></li>");
        }
    }

    // A folded sidebar shows only this: the item's icon, or its initial when it has none.
    private static string SnavIcon(MenuItemDefinition item) =>
        item.Icon is { } icon ? Icon(icon, "m-snav-icon") : $"""<span class="m-snav-initial" aria-hidden="true">{E(item.Label.Trim() is [var first, ..] ? char.ToUpperInvariant(first).ToString() : "")}</span>""";

    private static void SidebarLeaf(StringBuilder sb, MenuItemDefinition item, string path, string trail)
    {
        var active = item.Matches(path);
        sb.Append($"""<li class="m-snav-item{(active ? " active" : "")}"><a class="m-snav-link" href="{Href(item)}"{Trail(trail)}{(active ? " aria-current=\"page\"" : "")}><span class="m-snav-title">{E(item.Label)}</span></a></li>""");
    }

    private static string Join(string trail, string label) => trail.Length == 0 ? label : trail + " › " + label;

    private static string Trail(string trail) => trail.Length == 0 ? "" : $" data-m-trail=\"{E(trail)}\"";

}
