using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.Ui.Abstractions.Shell;

/// <summary>Everything the shell needs for one render.</summary>
/// <param name="Options">Theme options with the user's theme-settings cookies applied (<see cref="ThemeCss.ResolveOptions"/>).</param>
/// <param name="Menu">Authorized menu tree.</param>
/// <param name="Path">Current app-relative path without a leading slash.</param>
/// <param name="Mode">"light" or "dark" when known on the server, otherwise null.</param>
/// <param name="TenantId">Value of the <see cref="ShellRenderer.TenantCookie"/> cookie, used when the content names no current tenant.</param>
/// <param name="TenantIds">Value of the <see cref="ShellRenderer.TenantsCookie"/> cookie (active tenants in multi-company mode).</param>
public sealed record ShellView(ModulusThemeOptions Options, IReadOnlyList<MenuItemDefinition> Menu, string Path, ShellContent Content, string? Mode = null, string? TenantId = null, string? TenantIds = null);

/// <summary>
/// Renders the Tabler-style application shell (sidebar, header, horizontal navbar, footer and the
/// theme settings panel) as HTML. Blazor and MVC / Razor Pages both output these strings, so the
/// shell DOM is identical in every flavor. Behaviour lives in <c>modulus-shell.js</c>.
/// <para>
/// Menu depth: a top-level item with children becomes a dropdown (horizontal) or a collapsible group
/// (vertical). A child that has children of its own is a section: a header followed by its links.
/// </para>
/// <para>
/// Apps: a <see cref="ShellApp"/> with a <see cref="ShellApp.MenuKey"/> owns that menu item. While one of its pages
/// is open, the navbar and sidebar are rendered twice, the whole menu and the app's own menu, and the
/// <c>data-menu</c> attribute on &lt;html&gt; (theme settings) shows one of them.
/// </para>
/// </summary>
public static class ShellRenderer
{
    public const string IconSprite = "/_content/Modulus.Ui.Assets/icons/tabler.svg";

    /// <summary>Cookie the tenant switcher writes when a tenant has no link of its own.</summary>
    public const string TenantCookie = "modulus-tenant";

    /// <summary>Cookie holding the active tenants in multi-company mode, as comma-separated ids.</summary>
    public const string TenantsCookie = "modulus-tenants";

    /// <summary>Tabler outline icon: <c>&lt;svg class="m-ti"&gt;&lt;use href="…#ti-name"/&gt;&lt;/svg&gt;</c>.</summary>
    public static string Icon(string name, string? cssClass = null) =>
        $"""<svg class="m-ti{(cssClass is null ? "" : " " + cssClass)}" aria-hidden="true" focusable="false"><use href="{IconSprite}#ti-{E(name)}"></use></svg>""";

    // Sidebar (vertical layout, and the navigation drawer on small screens) ------------------------

    public static string Sidebar(ShellView view)
    {
        var app = FindApp(view);
        var sb = new StringBuilder();
        sb.Append("""<aside class="m-sidebar" id="m-sidebar" aria-label="Main navigation">""");
        sb.Append("""<div class="m-sidebar-brand">""").Append(Brand(view.Options)).Append("""<button type="button" class="m-icon-btn m-sidebar-close" data-m-nav-close aria-label="Close navigation">""").Append(Icon("x")).Append("</button></div>");
        sb.Append($"""<nav class="m-sidebar-nav m-nav-all{(app is null ? "" : " m-has-app")}"><ul class="m-snav">""");
        foreach (var item in view.Menu)
            SidebarItem(sb, item, view.Path, "");
        sb.Append("</ul></nav>");

        if (app is var (shellApp, menu))
        {
            sb.Append($"""<nav class="m-sidebar-nav m-nav-app" aria-label="{E(shellApp.Name)}">""")
              .Append($"""<a class="m-sidebar-app" href="{Url(shellApp.Href)}">{AppMark(shellApp)}<span class="m-sidebar-app-name">{E(shellApp.Name)}</span></a>""");
            if (HasLauncher(view.Content))
                sb.Append("""<button type="button" class="m-sidebar-apps" data-m-launcher-open aria-controls="m-launcher">""").Append(Icon("grid-dots")).Append("<span>All apps</span></button>");
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
              .Append(item.Icon is null ? "" : Icon(item.Icon, "m-snav-icon"))
              .Append($"""<span class="m-snav-title">{E(item.Label)}</span></a></li>""");
            return;
        }

        var open = IsActive(item, path);
        sb.Append($"""<li class="m-snav-item{(open ? " active" : "")}"><details class="m-snav-group"{(open ? " open" : "")}><summary class="m-snav-link">""")
          .Append(item.Icon is null ? "" : Icon(item.Icon, "m-snav-icon"))
          .Append($"""<span class="m-snav-title">{E(item.Label)}</span>""").Append(Icon("chevron-down", "m-snav-chevron"))
          .Append("""</summary><ul class="m-snav-sub">""");
        var groupTrail = Join(trail, item.Label);
        foreach (var child in item.Children)
        {
            if (child.Children.Count == 0)
            {
                SidebarLeaf(sb, child, path, groupTrail);
                continue;
            }
            sb.Append($"""<li class="m-snav-header">{E(child.Label)}</li>""");
            foreach (var leaf in child.Children) SidebarLeaf(sb, leaf, path, Join(groupTrail, child.Label));
        }
        sb.Append("</ul></details></li>");
    }

    private static void SidebarLeaf(StringBuilder sb, MenuItemDefinition item, string path, string trail)
    {
        var active = item.Matches(path);
        sb.Append($"""<li class="m-snav-item{(active ? " active" : "")}"><a class="m-snav-link" href="{Href(item)}"{Trail(trail)}{(active ? " aria-current=\"page\"" : "")}><span class="m-snav-title">{E(item.Label)}</span></a></li>""");
    }

    private static string Join(string trail, string label) => trail.Length == 0 ? label : trail + " › " + label;

    private static string Trail(string trail) => trail.Length == 0 ? "" : $" data-m-trail=\"{E(trail)}\"";

    // Header ---------------------------------------------------------------------------------------

    public static string Topbar(ShellView view)
    {
        var c = view.Content;
        var sb = new StringBuilder();
        sb.Append("""<header class="m-topbar"><div class="m-container m-topbar-inner">""");
        sb.Append("""<button type="button" class="m-icon-btn m-nav-toggle" data-m-nav-open aria-controls="m-sidebar" aria-label="Open navigation">""").Append(Icon("menu-2")).Append("</button>");
        if (HasLauncher(c))
            sb.Append("""<button type="button" class="m-icon-btn m-launcher-btn" data-m-launcher-open aria-controls="m-launcher" aria-haspopup="dialog" aria-label="All apps" title="All apps">""").Append(Icon("grid-dots")).Append("</button>");
        sb.Append(Brand(view.Options, "m-topbar-brand"));
        if (CurrentTenant(view) is not null) sb.Append("""<span class="m-topbar-sep m-topbar-sep-brand" aria-hidden="true"></span>""");
        TenantSwitcher(sb, view);

        if (c.SearchPlaceholder is { } placeholder)
        {
            // Keycaps: modulus-shell.js shows ⌘ instead of Ctrl on Apple devices.
            sb.Append("""<div class="m-search" data-m-search role="search">""")
              .Append(Icon("search", "m-search-icon"))
              .Append($"""<input type="search" class="m-search-input" placeholder="{E(placeholder)}" aria-label="Search pages" autocomplete="off" spellcheck="false" aria-controls="m-search-results" aria-expanded="false" />""")
              .Append("""<span class="m-search-kbd" aria-hidden="true"><kbd data-m-mod-key>Ctrl</kbd><kbd>K</kbd></span>""")
              .Append("""<div class="m-search-results" id="m-search-results" role="listbox" hidden></div></div>""");
        }

        sb.Append("""<div class="m-topbar-actions">""");
        sb.Append("""<button type="button" class="m-icon-btn m-search-open" data-m-search-open aria-label="Search">""").Append(Icon("search")).Append("</button>");
        if (!HasLauncher(c)) AppSwitcher(sb, c);
        sb.Append("""<button type="button" class="m-icon-btn" data-m-theme-toggle aria-label="Toggle dark mode">""")
          .Append(Icon("moon", "m-show-light")).Append(Icon("sun", "m-show-dark")).Append("</button>");
        if (view.Options.ShowCustomizer)
            sb.Append("""<button type="button" class="m-icon-btn" data-m-customizer-open aria-controls="m-customizer" aria-label="Theme settings">""").Append(Icon("palette")).Append("</button>");

        Activities(sb, c);

        if (c.Notices is { } notices)
        {
            var unread = notices.Count(n => n.Unread);
            sb.Append("""<details class="m-dropdown m-notices" data-m-notices><summary class="m-icon-btn" aria-label="Notifications">""")
              .Append(Icon("bell"))
              .Append(unread > 0 ? $"""<span class="m-notice-dot" aria-hidden="true"></span><span class="m-sr-only">{unread} unread</span>""" : "")
              .Append("""</summary><div class="m-dropdown-menu m-dropdown-end m-dropdown-card">""")
              .Append("""<div class="m-dropdown-card-header"><span class="m-dropdown-card-title">Notifications</span>""")
              .Append(unread > 0 ? """<button type="button" class="m-link-btn" data-m-mark-read>Mark all as read</button>""" : "")
              .Append("</div>");
            if (notices.Count == 0)
            {
                sb.Append("""<div class="m-dropdown-empty">You're all caught up.</div>""");
            }
            else
            {
                sb.Append("""<ul class="m-notice-list">""");
                foreach (var n in notices)
                {
                    var tag = n.Href is null ? "div" : "a";
                    sb.Append($"""<li class="m-notice{(n.Unread ? " unread" : "")}"><{tag} class="m-notice-link"{(n.Href is null ? "" : $" href=\"{Url(n.Href)}\"")}>""")
                      .Append($"""<span class="m-icon-tile m-tile-{E(n.Tone)}-lt">{Icon(n.Icon)}</span>""")
                      .Append($"""<span class="m-notice-body"><span class="m-notice-title">{E(n.Title)}</span>""")
                      .Append(string.IsNullOrEmpty(n.Description) ? "" : $"""<span class="m-notice-desc">{E(n.Description)}</span>""")
                      .Append($"""<span class="m-notice-time">{E(n.When)}</span></span>""")
                      .Append(n.Unread ? """<span class="m-status-dot m-status-primary" aria-hidden="true"></span>""" : "")
                      .Append($"</{tag}></li>");
                }
                sb.Append("</ul>");
            }
            if (c.NoticesHref is { } all)
                sb.Append($"""<a class="m-dropdown-card-footer" href="{Url(all)}">View all notifications</a>""");
            sb.Append("</div></details>");
        }

        var user = c.User;
        sb.Append("""<span class="m-topbar-sep" aria-hidden="true"></span>""");
        sb.Append("""<details class="m-dropdown m-user"><summary class="m-user-toggle" aria-label="Account menu">""")
          .Append(Avatar(user))
          .Append($"""<span class="m-user-text"><span class="m-user-name">{E(user.Name)}</span>""")
          .Append(user.Subtitle is null ? "" : $"""<span class="m-user-subtitle">{E(user.Subtitle)}</span>""")
          .Append("</span>").Append(Icon("chevron-down", "m-user-chevron"))
          .Append("""</summary><div class="m-dropdown-menu m-dropdown-end" role="menu">""")
          .Append($"""<div class="m-user-head">{Avatar(user, "md")}<span class="m-user-text"><span class="m-user-name">{E(user.Name)}</span>""")
          .Append(user.Subtitle is null ? "" : $"""<span class="m-user-subtitle">{E(user.Subtitle)}</span>""")
          .Append("""</span></div><div class="m-dropdown-divider"></div>""");
        foreach (var link in c.UserLinks)
        {
            if (link.DividerBefore) sb.Append("""<div class="m-dropdown-divider"></div>""");
            sb.Append($"""<a class="m-dropdown-item" role="menuitem" href="{Url(link.Href)}">{(link.Icon is null ? "" : Icon(link.Icon))}{E(link.Label)}</a>""");
        }
        if (c.SignOutHref is { } signOut)
            sb.Append("""<div class="m-dropdown-divider"></div>""").Append($"""<a class="m-dropdown-item" role="menuitem" href="{Url(signOut)}">{Icon("logout")}Sign out</a>""");
        sb.Append("</div></details>");

        sb.Append("</div></div></header>");
        return sb.ToString();
    }

    /// <summary>Tenants listed before the switcher shows a filter box.</summary>
    public const int TenantFilterThreshold = 5;

    /// <summary>The tenant the header shows: the content's current id, then the cookie, then the first tenant.</summary>
    public static ShellTenant? CurrentTenant(ShellView view)
    {
        var tenants = view.Content.Tenants;
        if (tenants is not { Count: > 0 }) return null;
        return tenants.FirstOrDefault(t => t.Id == view.Content.CurrentTenantId)
            ?? tenants.FirstOrDefault(t => t.Id == view.TenantId)
            ?? tenants[0];
    }

    /// <summary>
    /// Active tenants in multi-company mode, in list order: the content's ids, else the
    /// <see cref="TenantsCookie"/> cookie, always with the current tenant. Just the current tenant otherwise.
    /// </summary>
    public static IReadOnlyList<ShellTenant> ActiveTenants(ShellView view)
    {
        if (CurrentTenant(view) is not { } current) return [];
        if (!view.Content.MultiTenant) return [current];
        var ids = view.Content.ActiveTenantIds
            ?? (view.TenantIds ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return view.Content.Tenants!.Where(t => t == current || ids.Contains(t.Id)).ToList();
    }

    private static void TenantSwitcher(StringBuilder sb, ShellView view)
    {
        if (CurrentTenant(view) is not { } current) return;
        var tenants = view.Content.Tenants!;
        var multi = view.Content.MultiTenant;
        var active = ActiveTenants(view);
        sb.Append($"""<details class="m-dropdown m-tenant{(multi ? " m-tenant-multi" : "")}" data-m-tenants><summary class="m-tenant-toggle" aria-label="Switch tenant" aria-haspopup="{(multi ? "true" : "listbox")}">""")
          .Append(TenantMark(current, tenants))
          .Append($"""<span class="m-tenant-text"><span class="m-tenant-name">{E(current.Name)}</span>""")
          .Append(current.Subtitle is null ? "" : $"""<span class="m-tenant-subtitle">{E(current.Subtitle)}</span>""")
          .Append("</span>")
          .Append(active.Count > 1 ? $"""<span class="m-tenant-more" title="{active.Count - 1} more active">+{active.Count - 1}</span>""" : "")
          .Append(Icon("selector", "m-tenant-chevron"))
          .Append($"""</summary><div class="m-dropdown-menu m-tenant-menu"><div class="m-dropdown-header">{(multi ? "Companies" : "Switch tenant")}</div>""");
        if (tenants.Count > TenantFilterThreshold)
            sb.Append("""<div class="m-tenant-filter">""").Append(Icon("search"))
              .Append("""<input type="search" class="m-tenant-filter-input" data-m-tenant-filter placeholder="Find a tenant…" aria-label="Find a tenant" autocomplete="off" /></div>""");
        sb.Append(multi ? """<div class="m-tenant-list">""" : """<div class="m-tenant-list" role="listbox" aria-label="Tenants">""");
        foreach (var t in tenants)
        {
            var selected = t == current;
            var body = TenantMark(t, tenants)
                + $"""<span class="m-tenant-text"><span class="m-tenant-name">{E(t.Name)}</span>"""
                + (t.Subtitle is null ? "" : $"""<span class="m-tenant-subtitle">{E(t.Subtitle)}</span>""")
                + "</span>" + (selected ? Icon("check", "m-tenant-check") : "");
            var cls = $"m-dropdown-item m-tenant-item{(selected ? " active" : "")}";
            var aria = multi
                ? $"""{(selected ? " aria-current=\"true\"" : "")} data-m-tenant-name="{E(t.Name)}" """
                : $""" role="option" aria-selected="{(selected ? "true" : "false")}" data-m-tenant-name="{E(t.Name)}" """;
            var item = t.Href is null
                ? $"""<button type="button" class="{cls}"{aria}data-m-tenant="{E(t.Id)}">{body}</button>"""
                : $"""<a class="{cls}"{aria}href="{Url(t.Href)}">{body}</a>""";
            if (!multi)
            {
                sb.Append(item);
                continue;
            }
            // Odoo: the checkbox adds the tenant to the active set, the name makes it the current one.
            var on = active.Contains(t);
            sb.Append($"""<div class="m-tenant-row{(t.ParentId is null ? "" : " m-tenant-branch")}">""")
              .Append($"""<button type="button" class="m-tenant-active" role="checkbox" aria-checked="{(on ? "true" : "false")}" aria-label="Active: {E(t.Name)}" data-m-tenant-toggle="{E(t.Id)}"{(selected ? " disabled title=\"The current tenant is always active\"" : "")}>""")
              .Append(Icon("check")).Append("</button>").Append(item).Append("</div>");
        }
        sb.Append("""</div><div class="m-dropdown-empty m-tenant-empty" hidden>No tenants match.</div>""");
        if (view.Content.TenantLinks.Count > 0)
        {
            sb.Append("""<div class="m-dropdown-divider"></div>""");
            foreach (var link in view.Content.TenantLinks)
                sb.Append($"""<a class="m-dropdown-item" href="{Url(link.Href)}">{(link.Icon is null ? "" : Icon(link.Icon))}{E(link.Label)}</a>""");
        }
        sb.Append("</div></details>");
    }

    // Logo, or initials tinted by the tenant's position so each keeps its colour.
    private static string TenantMark(ShellTenant tenant, IReadOnlyList<ShellTenant> all)
    {
        if (tenant.LogoUrl is { } src)
            return $"""<span class="m-avatar m-avatar-sm m-tenant-mark" style="background-image:url('{E(src)}')"></span>""";
        var tint = 1 + (IndexOf(all, tenant) % 8);
        return $"""<span class="m-avatar m-avatar-sm m-tenant-mark m-av-{tint}">{E(Initials(tenant.Name))}</span>""";
    }

    private static int IndexOf(IReadOnlyList<ShellTenant> all, ShellTenant tenant)
    {
        for (var i = 0; i < all.Count; i++) if (all[i] == tenant) return i;
        return 0;
    }

    private static void AppSwitcher(StringBuilder sb, ShellContent c)
    {
        if (c.Apps is not { Count: > 0 } apps) return;
        sb.Append("""<details class="m-dropdown m-apps"><summary class="m-icon-btn" aria-label="Apps">""").Append(Icon("grid-dots"))
          .Append("""</summary><div class="m-dropdown-menu m-dropdown-end m-dropdown-card m-apps-menu">""")
          .Append("""<div class="m-dropdown-card-header"><span class="m-dropdown-card-title">Apps</span></div><div class="m-apps-grid">""");
        foreach (var app in apps)
        {
            sb.Append($"""<a class="m-app-tile" href="{Url(app.Href)}"{(app.Description is null ? "" : $" title=\"{E(app.Description)}\"")}>""")
              .Append($"""<span class="m-icon-tile m-tile-{E(app.Tone)}-lt">{Icon(app.Icon)}</span>""")
              .Append($"""<span class="m-app-name">{E(app.Name)}</span></a>""");
        }
        sb.Append("</div>");
        if (c.AppsHref is { } all)
            sb.Append($"""<a class="m-dropdown-card-footer" href="{Url(all)}">View all apps</a>""");
        sb.Append("</div></details>");
    }

    private static void Activities(StringBuilder sb, ShellContent c)
    {
        if (c.Activities is not { } activities) return;
        var due = activities.Sum(a => a.Late + a.Today);
        var late = activities.Any(a => a.Late > 0);
        sb.Append("""<details class="m-dropdown m-activities" data-m-activities><summary class="m-icon-btn" aria-label="Activities">""")
          .Append(Icon("clock"))
          .Append(due > 0 ? $"""<span class="m-icon-badge{(late ? " m-icon-badge-danger" : "")}" aria-hidden="true">{(due > 99 ? "99+" : due.ToString(System.Globalization.CultureInfo.InvariantCulture))}</span><span class="m-sr-only">{due} due</span>""" : "")
          .Append("""</summary><div class="m-dropdown-menu m-dropdown-end m-dropdown-card m-activity-menu">""")
          .Append("""<div class="m-dropdown-card-header"><span class="m-dropdown-card-title">Activities</span></div>""");
        if (activities.Count == 0)
        {
            sb.Append("""<div class="m-dropdown-empty">No activities planned.</div>""");
        }
        else
        {
            sb.Append("""<ul class="m-activity-list">""");
            foreach (var a in activities)
            {
                var tag = a.Href is null ? "div" : "a";
                sb.Append($"""<li><{tag} class="m-activity"{(a.Href is null ? "" : $" href=\"{Url(a.Href)}\"")}>""")
                  .Append($"""<span class="m-icon-tile m-tile-{E(a.Tone)}-lt">{Icon(a.Icon)}</span>""")
                  .Append($"""<span class="m-activity-name">{E(a.Name)}</span><span class="m-activity-counts">""")
                  .Append(Count(a.Late, "late", "danger")).Append(Count(a.Today, "today", "warning")).Append(Count(a.Planned, "future", "success"))
                  .Append($"</span></{tag}></li>");
            }
            sb.Append("</ul>");
        }
        if (c.ActivitiesHref is { } all)
            sb.Append($"""<a class="m-dropdown-card-footer" href="{Url(all)}">View all activities</a>""");
        sb.Append("</div></details>");

        static string Count(int n, string label, string tone) =>
            $"""<span class="m-activity-count m-badge m-badge-{(n > 0 ? tone : "gray")}-lt" title="{n} {label}"><b>{n}</b> {label}</span>""";
    }

    // App launcher ------------------------------------------------------------------------------------

    private static bool HasLauncher(ShellContent c) => c.AppLauncher == AppLauncherStyle.Overlay && c.Apps is { Count: > 0 };

    /// <summary>
    /// Full-screen home menu (Odoo) for <see cref="AppLauncherStyle.Overlay"/>: large app icons, and a search box
    /// that filters them and finds menu entries. Empty for the dropdown launcher. Render it once, after the page.
    /// </summary>
    public static string AppLauncher(ShellView view)
    {
        var c = view.Content;
        if (!HasLauncher(c)) return "";
        var current = FindApp(view)?.App;
        var sb = new StringBuilder();
        sb.Append("""<div class="m-launcher" id="m-launcher" data-m-launcher hidden>""")
          .Append("""<div class="m-launcher-panel" role="dialog" aria-modal="true" aria-label="Apps"><div class="m-launcher-inner">""")
          .Append("""<div class="m-launcher-top"><div class="m-launcher-search">""").Append(Icon("search"))
          .Append("""<input type="search" class="m-launcher-input" data-m-launcher-search placeholder="Search apps and menus…" aria-label="Search apps and menus" autocomplete="off" spellcheck="false" /></div>""")
          .Append("""<button type="button" class="m-icon-btn" data-m-launcher-close aria-label="Close">""").Append(Icon("x")).Append("</button></div>")
          .Append("""<div class="m-launcher-grid">""");
        foreach (var app in c.Apps!)
        {
            var active = app == current;
            sb.Append($"""<a class="m-launcher-app{(active ? " active" : "")}" href="{Url(app.Href)}" data-m-launcher-name="{E((app.Name + " " + app.Description).Trim())}"{(active ? " aria-current=\"true\"" : "")}>""")
              .Append(AppMark(app, "lg"))
              .Append($"""<span class="m-launcher-name">{E(app.Name)}</span></a>""");
        }
        sb.Append("""</div><div class="m-launcher-empty" hidden>No apps match.</div>""")
          .Append("""<div class="m-launcher-menus" hidden><div class="m-launcher-heading">Menus</div><div class="m-launcher-results"></div></div>""");
        if (c.AppsHref is { } all)
            sb.Append($"""<a class="m-launcher-all" href="{Url(all)}">View all apps{Icon("arrow-right")}</a>""");
        sb.Append("</div></div></div>");
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
            sb.Append($"""<nav class="m-navbar m-nav-app" aria-label="{E(shellApp.Name)}"><div class="m-container"><ul class="m-hnav">""")
              .Append($"""<li class="m-hnav-app"><a class="m-hnav-app-link" href="{Url(shellApp.Href)}">{AppMark(shellApp)}<span>{E(shellApp.Name)}</span></a></li>""");
            foreach (var item in menu.Children) NavbarItem(sb, item, view.Path, icons: false);
            sb.Append("</ul></div></nav>");
        }
        return sb.ToString();
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

        {

            var sections = item.Children.Where(c => c.Children.Count > 0).ToList();
            var loose = item.Children.Where(c => c.Children.Count == 0).ToList();
            var columns = sections.Count + (loose.Count > 0 ? 1 : 0);
            sb.Append($"""<li class="m-hnav-item{(active ? " active" : "")}"><details class="m-dropdown"><summary class="m-hnav-link">""")
              .Append(icon)
              .Append($"""<span>{E(item.Label)}</span>""").Append(Icon("chevron-down", "m-hnav-chevron"))
              .Append($"""</summary><div class="m-dropdown-menu{(columns > 1 ? $" m-dropdown-columns m-dropdown-columns-{Math.Min(columns, 4)}" : "")}">""");
            if (loose.Count > 0)
            {
                if (columns > 1) sb.Append("""<div class="m-dropdown-col">""");
                foreach (var leaf in loose) DropdownItem(sb, leaf, path);
                if (columns > 1) sb.Append("</div>");
            }
            foreach (var section in sections)
            {
                sb.Append($"""<div class="m-dropdown-col"><div class="m-dropdown-header">{E(section.Label)}</div>""");
                foreach (var leaf in section.Children) DropdownItem(sb, leaf, path);
                sb.Append("</div>");
            }
            sb.Append("</div></details></li>");
        }
    }

    private static void DropdownItem(StringBuilder sb, MenuItemDefinition item, string path)
    {
        var active = item.Matches(path);
        sb.Append($"""<a class="m-dropdown-item{(active ? " active" : "")}" href="{Href(item)}"{(active ? " aria-current=\"page\"" : "")}>""")
          .Append(item.Icon is null ? "" : Icon(item.Icon))
          .Append(E(item.Label)).Append("</a>");
    }

    // Footer ----------------------------------------------------------------------------------------------

    public static string Footer(ShellView view)
    {
        var o = view.Options;
        var sb = new StringBuilder("""<footer class="m-footer"><div class="m-container m-footer-inner">""");
        if (view.Content.FooterLinks.Count > 0)
        {
            sb.Append("""<ul class="m-footer-links">""");
            foreach (var link in view.Content.FooterLinks)
                sb.Append($"""<li><a href="{Url(link.Href)}">{(link.Icon is null ? "" : Icon(link.Icon))}{E(link.Label)}</a></li>""");
            sb.Append("</ul>");
        }
        sb.Append("""<ul class="m-footer-meta">""");
        if (o.Copyright is { } copyright) sb.Append($"<li>Copyright © {E(copyright)}. All rights reserved.</li>");
        if (o.Version is { } version) sb.Append($"""<li><span class="m-footer-version">{E(version)}</span></li>""");
        sb.Append("</ul></div></footer>");
        return sb.ToString();
    }

    // Theme settings panel ----------------------------------------------------------------------------------

    /// <summary>
    /// Offcanvas "Theme settings" panel. Each colour swatch carries its generated brand CSS in
    /// <c>data-css</c>, so the page recolours instantly; the choice is saved in cookies.
    /// </summary>
    public static string Customizer(ShellView view)
    {
        var o = view.Options;
        if (!o.ShowCustomizer) return "";
        var preset = ThemeCss.PresetName(o);
        var sb = new StringBuilder();
        sb.Append("""<div class="m-offcanvas" id="m-customizer" data-m-customizer hidden>""")
          .Append("""<div class="m-offcanvas-backdrop" data-m-customizer-close></div>""")
          .Append("""<aside class="m-offcanvas-panel" role="dialog" aria-modal="true" aria-labelledby="m-customizer-title">""")
          .Append("""<div class="m-offcanvas-header"><div><h2 class="m-offcanvas-title" id="m-customizer-title">Theme settings</h2>""")
          .Append("""<p class="m-offcanvas-subtitle">Saved in this browser and applied instantly.</p></div>""")
          .Append("""<button type="button" class="m-icon-btn" data-m-customizer-close aria-label="Close">""").Append(Icon("x")).Append("</button></div>")
          .Append("""<form class="m-offcanvas-body" data-m-customizer-form>""");

        Choice(sb, "Color mode", "mode", view.Mode ?? "system", [("light", "Light", Icon("sun")), ("dark", "Dark", Icon("moon")), ("system", "System", Icon("device-desktop"))]);

        sb.Append("""<fieldset class="m-setting"><legend class="m-setting-label">Color scheme</legend><div class="m-swatches">""");
        foreach (var (name, hex) in o.ColorPresets)
        {
            var css = ThemeCss.BuildForPrimary(o, hex);
            sb.Append($"""<label class="m-swatch" title="{E(Capitalize(name))}"><input type="radio" name="primary" value="{E(name)}" data-css="{E(css)}"{(name == preset ? " checked" : "")} />""")
              .Append($"""<span class="m-swatch-color" style="background:{E(hex)}"></span><span class="m-sr-only">{E(Capitalize(name))}</span></label>""");
        }
        sb.Append("</div></fieldset>");

        Choice(sb, "Theme base", "base", Lower(o.Base),
            Enum.GetValues<ThemeBase>().Select(b => (Lower(b), b.ToString(), (string?)$"""<span class="m-base-swatch m-base-{Lower(b)}"></span>""")).ToArray(), "m-choices-compact");
        Choice(sb, "Font family", "font", Lower(o.Font),
        [
            ("sans", "Inter", """<span class="m-font-preview m-font-sans">Aa</span>"""),
            ("system", "System", """<span class="m-font-preview m-font-system">Aa</span>"""),
            ("serif", "Serif", """<span class="m-font-preview m-font-serif">Aa</span>"""),
            ("mono", "Mono", """<span class="m-font-preview m-font-mono">Aa</span>"""),
        ], "m-choices-compact");
        Choice(sb, "Corner radius", "radius", o.BorderRadius.ToString(System.Globalization.CultureInfo.InvariantCulture), [("0", "0", null), ("4", "4", null), ("6", "6", null), ("8", "8", null), ("12", "12", null)]);
        Choice(sb, "Layout", "layout", Lower(o.Layout), [("horizontal", "Horizontal", Thumb("horizontal")), ("vertical", "Vertical", Thumb("vertical"))]);
        Choice(sb, "Menu", "menu", Lower(o.MenuScope), [("all", "All menus", Thumb("menu-all")), ("app", "Current app", Thumb("menu-app"))]);
        Choice(sb, "Navigation", "nav", Lower(o.NavTheme), [("light", "Light", Icon("sun")), ("dark", "Dark", Icon("moon"))]);
        Choice(sb, "Content width", "width", Lower(o.ContentWidth), [("boxed", "Boxed", Icon("layout")), ("fluid", "Fluid", Icon("arrows-horizontal"))]);

        sb.Append("""</form><div class="m-offcanvas-footer"><button type="button" class="ant-btn ant-btn-default ant-btn-block" data-m-customizer-reset><span>Reset to defaults</span></button></div>""")
          .Append("</aside></div>");
        return sb.ToString();
    }

    // visual: markup above the label (an icon, a preview); trusted, built by this class.
    private static void Choice(StringBuilder sb, string label, string name, string current, (string Value, string Text, string? Visual)[] options, string? cssClass = null)
    {
        sb.Append($"""<fieldset class="m-setting"><legend class="m-setting-label">{E(label)}</legend><div class="m-choices{(cssClass is null ? "" : " " + cssClass)}">""");
        foreach (var (value, text, visual) in options)
        {
            sb.Append($"""<label class="m-choice"><input type="radio" name="{E(name)}" value="{E(value)}"{(value == current ? " checked" : "")} />""")
              .Append("""<span class="m-choice-box">""").Append(visual ?? "").Append($"<span>{E(text)}</span></span></label>");
        }
        sb.Append("</div></fieldset>");
    }

    // Miniature of a layout, drawn by modulus-shell.css.
    private static string Thumb(string kind) => $"""<span class="m-thumb m-thumb-{kind}" aria-hidden="true"><i></i><i></i><i></i></span>""";

    private static string Lower<T>(T value) where T : struct, Enum => value.ToString().ToLowerInvariant();

    // Helpers ------------------------------------------------------------------------------------------------

    /// <summary>Brand mark and title linking to the home page.</summary>
    public static string Brand(ModulusThemeOptions options, string? cssClass = null)
    {
        var logo = options.LogoUrl is { } url
            ? $"""<img class="m-brand-logo" src="{E(url)}" alt="" />"""
            : """<svg class="m-brand-logo" viewBox="0 0 32 32" aria-hidden="true"><rect width="32" height="32" rx="8" fill="var(--m-primary)"/><path d="M8 22V10.5a.5.5 0 0 1 .85-.35L16 17.3l7.15-7.15a.5.5 0 0 1 .85.35V22" fill="none" stroke="#fff" stroke-width="2.6" stroke-linecap="round" stroke-linejoin="round"/></svg>""";
        return $"""<a class="m-brand{(cssClass is null ? "" : " " + cssClass)}" href="/">{logo}<span class="m-brand-text">{E(options.Title)}</span></a>""";
    }

    /// <summary>Square Tabler avatar: the image, or initials on a tinted background.</summary>
    public static string Avatar(ShellUser user, string size = "sm") =>
        user.AvatarUrl is { } src
            ? $"""<span class="m-avatar m-avatar-{size}" style="background-image:url('{E(src)}')"></span>"""
            : $"""<span class="m-avatar m-avatar-{size} m-av-1">{E(Initials(user.Name))}</span>""";

    public static string Initials(string name) =>
        string.Concat(name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Take(2).Select(p => char.ToUpperInvariant(p[0])));

    private static bool IsActive(MenuItemDefinition item, string path) =>
        item.Matches(path) || item.Children.Any(c => IsActive(c, path));

    private static string Href(MenuItemDefinition item) => E("/" + (item.Href ?? "").TrimStart('/'));

    /// <summary>App-relative links ("settings") resolve from the site root; absolute URLs and anchors pass through.</summary>
    private static string Url(string href) =>
        E(href.StartsWith('/') || href.StartsWith('#') || href.Contains("://", StringComparison.Ordinal) ? href : "/" + href);

    private static string Capitalize(string s) => s.Length == 0 ? s : char.ToUpperInvariant(s[0]) + s[1..];

    private static string E(string? value) => WebUtility.HtmlEncode(value ?? "");
}
