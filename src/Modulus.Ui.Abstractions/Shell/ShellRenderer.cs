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
            // The module switcher sits above the scrolling menu so its list is not clipped by it.
            var tone = $"style=\"--m-app-tone: var(--m-{E(shellApp.Tone)})\"";
            sb.Append($"""<div class="m-sidebar-module m-nav-app" {tone}><details class="m-dropdown m-module-switch"><summary class="m-sidebar-app" aria-label="{E(shellApp.Name)}, switch module">""")
              .Append(AppMark(shellApp))
              .Append($"""<span class="m-sidebar-app-text"><span class="m-sidebar-app-name">{E(shellApp.Name)}</span>""")
              .Append(string.IsNullOrEmpty(shellApp.Description) ? "" : $"""<span class="m-sidebar-app-desc">{E(shellApp.Description)}</span>""")
              .Append("</span>").Append(Icon("selector", "m-sidebar-app-chevron")).Append("</summary>");
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
          .Append($"""<span class="m-snav-title">{E(item.Label)}</span>""").Append(Icon("chevron-down", "m-snav-chevron"))
          .Append($"""</summary><ul class="m-snav-sub"><li class="m-snav-flyout-title" aria-hidden="true">{E(item.Label)}</li>""");
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

    // Header ---------------------------------------------------------------------------------------

    /// <summary>
    /// The header row and the horizontal navbar in one wrapper, which the theme settings' header modes (sticky,
    /// floating) and the condensed layout (menu inside the header row) style as a unit. Render it once, after the sidebar.
    /// </summary>
    public static string Header(ShellView view) => """<div class="m-header">""" + Topbar(view) + Navbar(view) + "</div>";

    public static string Topbar(ShellView view)
    {
        var c = view.Content;
        var sb = new StringBuilder();
        sb.Append("""<header class="m-topbar"><div class="m-container m-topbar-inner">""");
        sb.Append("""<button type="button" class="m-icon-btn m-nav-toggle" data-m-nav-open aria-controls="m-sidebar" aria-label="Open navigation">""").Append(Icon("menu-2")).Append("</button>");
        sb.Append("""<button type="button" class="m-icon-btn m-fold-toggle" data-m-fold-toggle aria-controls="m-sidebar" aria-label="Fold sidebar" title="Fold sidebar">""").Append(Icon("layout-sidebar")).Append("</button>");
        if (HasLauncher(c))
            sb.Append("""<button type="button" class="m-icon-btn m-launcher-btn" data-m-launcher-open aria-controls="m-launcher" aria-haspopup="dialog" aria-label="All apps" title="All apps">""").Append(Icon("grid-dots")).Append("</button>");
        sb.Append(Brand(view.Options, "m-topbar-brand"));

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
        // The utility buttons share one tray, so the row reads as search, tools, then company and account.
        sb.Append("""<div class="m-topbar-tools">""");
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

        sb.Append("</div>");

        // Odoo's systray: the company sits with the account menu at the end of the row.
        var user = c.User;
        sb.Append("""<span class="m-topbar-sep" aria-hidden="true"></span>""");
        TenantSwitcher(sb, view);
        sb.Append("""<details class="m-dropdown m-user"><summary class="m-user-toggle" aria-label="Account menu">""")
          .Append("""<span class="m-user-avatar">""").Append(Avatar(user)).Append("""<span class="m-user-presence" aria-hidden="true"></span></span>""")
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
          .Append(TenantMark(current, tenants, solid: true))
          .Append($"""<span class="m-tenant-text"><span class="m-tenant-name">{E(current.Name)}</span>""")
          .Append(current.Subtitle is null ? "" : $"""<span class="m-tenant-subtitle">{E(current.Subtitle)}</span>""")
          .Append("</span>")
          .Append(active.Count > 1 ? $"""<span class="m-tenant-more" title="{active.Count - 1} more active">+{active.Count - 1}</span>""" : "")
          .Append(Icon("selector", "m-tenant-chevron"))
          .Append("""</summary><div class="m-dropdown-menu m-tenant-menu">""");
        // The tenant in use heads the menu (SAP's and Oracle's context header), with how many are active.
        sb.Append("""<div class="m-tenant-current">""").Append(TenantMark(current, tenants, solid: true))
          .Append($"""<span class="m-tenant-text"><span class="m-tenant-name">{E(current.Name)}</span>""")
          .Append(current.Subtitle is null ? "" : $"""<span class="m-tenant-subtitle">{E(current.Subtitle)}</span>""")
          .Append("</span>")
          .Append(multi
              ? $"""<span class="m-tenant-count" title="Active companies">{active.Count}/{tenants.Count}</span>"""
              : """<span class="m-tenant-badge">Current</span>""")
          .Append("</div>");
        if (tenants.Count > TenantFilterThreshold)
            sb.Append("""<div class="m-tenant-filter">""").Append(Icon("search"))
              .Append($"""<input type="search" class="m-tenant-filter-input" data-m-tenant-filter placeholder="{(multi ? "Find a company…" : "Find a tenant…")}" aria-label="{(multi ? "Find a company" : "Find a tenant")}" autocomplete="off" /></div>""");
        sb.Append($"""<div class="m-dropdown-header">{(multi ? "Companies" : "Switch tenant")}</div>""");
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
            sb.Append("""<div class="m-tenant-links">""");
            foreach (var link in view.Content.TenantLinks)
                sb.Append($"""<a class="m-dropdown-item" href="{Url(link.Href)}">{(link.Icon is null ? "" : Icon(link.Icon))}{E(link.Label)}</a>""");
            sb.Append("</div>");
        }
        sb.Append("</div></details>");
    }

    // Logo, or initials tinted by the tenant's position so each keeps its colour.
    // solid: initials in white on the full tint, for the header and the menu's current tenant.
    private static string TenantMark(ShellTenant tenant, IReadOnlyList<ShellTenant> all, bool solid = false)
    {
        if (tenant.LogoUrl is { } src)
            return $"""<span class="m-avatar m-avatar-sm m-tenant-mark" style="background-image:url('{E(src)}')"></span>""";
        var tint = 1 + (IndexOf(all, tenant) % 8);
        return $"""<span class="m-avatar m-avatar-sm m-tenant-mark{(solid ? " m-tenant-mark-solid" : "")} m-av-{tint}">{E(Initials(tenant.Name))}</span>""";
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
    /// Full-screen home menu for <see cref="AppLauncherStyle.Overlay"/>, after Odoo's home menu and Oracle's navigator:
    /// a greeting, a search box that filters the apps and finds menu entries, the apps as cards filed under their
    /// <see cref="ShellApp.Group"/>, and the pages the user opened recently (kept in the browser by modulus-shell.js).
    /// Empty for the dropdown launcher. Render it once, after the page.
    /// </summary>
    public static string AppLauncher(ShellView view)
    {
        var c = view.Content;
        if (!HasLauncher(c)) return "";
        var current = FindApp(view)?.App;
        var firstName = c.User.Name.Trim().Split(' ', 2)[0];
        var sb = new StringBuilder();
        sb.Append("""<div class="m-launcher" id="m-launcher" data-m-launcher hidden>""")
          .Append("""<div class="m-launcher-panel" role="dialog" aria-modal="true" aria-labelledby="m-launcher-title"><div class="m-launcher-inner">""")
          // The script swaps the greeting for one that fits the time of day and fills in today's date.
          .Append($"""<div class="m-launcher-top"><div class="m-launcher-hello"><h2 class="m-launcher-title" id="m-launcher-title" data-m-greeting="{E(firstName)}">Welcome back{(firstName.Length == 0 ? "" : ", " + E(firstName))}</h2>""")
          .Append("""<span class="m-launcher-date" data-m-today></span></div>""")
          .Append("""<button type="button" class="m-icon-btn m-launcher-close" data-m-launcher-close aria-label="Close">""").Append(Icon("x")).Append("</button></div>")
          .Append("""<div class="m-launcher-search">""").Append(Icon("search"))
          .Append("""<input type="search" class="m-launcher-input" data-m-launcher-search placeholder="Search apps and menus…" aria-label="Search apps and menus" autocomplete="off" spellcheck="false" />""")
          .Append("""<kbd class="m-launcher-esc" aria-hidden="true">Esc</kbd></div>""")
          .Append("""<div class="m-launcher-body"><div class="m-launcher-main">""");
        foreach (var group in c.Apps!.GroupBy(a => a.Group))
        {
            sb.Append("""<section class="m-launcher-group">""");
            if (group.Key is { Length: > 0 } heading) sb.Append($"""<h3 class="m-launcher-heading">{E(heading)}</h3>""");
            sb.Append("""<div class="m-launcher-grid">""");
            foreach (var app in group)
            {
                var active = app == current;
                sb.Append($"""<a class="m-launcher-app{(active ? " active" : "")}" href="{Url(app.Href)}" data-m-launcher-name="{E((app.Name + " " + app.Description + " " + app.Group).Trim())}"{(active ? " aria-current=\"true\"" : "")}>""")
                  .Append(AppMark(app, "lg"))
                  .Append($"""<span class="m-launcher-app-text"><span class="m-launcher-name">{E(app.Name)}</span>""")
                  .Append(string.IsNullOrEmpty(app.Description) ? "" : $"""<span class="m-launcher-desc">{E(app.Description)}</span>""")
                  .Append("</span>")
                  .Append(active ? """<span class="m-launcher-open-tag">Open</span>""" : "")
                  .Append("</a>");
            }
            sb.Append("</div></section>");
        }
        sb.Append("""<div class="m-launcher-empty" hidden>No apps match.</div>""")
          .Append("""<div class="m-launcher-menus" hidden><h3 class="m-launcher-heading">Menus</h3><div class="m-launcher-results"></div></div>""");
        if (c.AppsHref is { } all)
            sb.Append($"""<a class="m-launcher-all" href="{Url(all)}">View all apps{Icon("arrow-right")}</a>""");
        sb.Append("</div>")
          .Append("""<aside class="m-launcher-rail" data-m-recent hidden><h3 class="m-launcher-heading">""").Append(Icon("history")).Append("""Recent</h3><div class="m-launcher-recent"></div></aside>""")
          .Append("</div></div></div></div>");
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
            // The module bar (Odoo's app menu, SAP's shell): tinted with the app's tone, a module switcher in place
            // of the app's name, the app's menus, and where the page sits in them.
            sb.Append($"""<nav class="m-navbar m-nav-app" aria-label="{E(shellApp.Name)}" style="--m-app-tone: var(--m-{E(shellApp.Tone)})"><div class="m-container"><ul class="m-hnav">""");
            ModuleSwitcher(sb, view, shellApp);
            foreach (var item in menu.Children) NavbarItem(sb, item, view.Path, icons: false);
            sb.Append("</ul>");
            if (Breadcrumb(menu, view.Path) is { Count: > 0 } trail)
            {
                sb.Append($"""<ol class="m-nav-crumbs" aria-label="You are here"><li><a href="{Url(shellApp.Href)}">{Icon(shellApp.Icon, "m-nav-crumbs-icon")}{E(shellApp.Name)}</a></li>""");
                for (var i = 0; i < trail.Count; i++)
                    sb.Append(i == trail.Count - 1 ? $"""<li aria-current="page">{E(trail[i].Label)}</li>""" : $"""<li>{E(trail[i].Label)}</li>""");
                sb.Append("</ol>");
            }
            sb.Append("</div></nav>");
        }
        return sb.ToString();
    }

    // The app's name opens a list of every app to jump to (SAP's product switch), and the full launcher.
    private static void ModuleSwitcher(StringBuilder sb, ShellView view, ShellApp current)
    {
        sb.Append($"""<li class="m-hnav-app"><details class="m-dropdown m-module-switch"><summary class="m-hnav-app-link" aria-label="{E(current.Name)}, switch module">""")
          .Append(AppMark(current)).Append($"""<span class="m-hnav-app-name">{E(current.Name)}</span>""").Append(Icon("chevron-down", "m-hnav-app-chevron"))
          .Append("</summary>");
        ModuleMenu(sb, view, current);
        sb.Append("</details></li>");
    }

    private static void ModuleMenu(StringBuilder sb, ShellView view, ShellApp current)
    {
        sb.Append("""<div class="m-dropdown-menu m-module-menu"><div class="m-dropdown-header">Switch module</div>""");
        foreach (var app in view.Content.Apps!)
        {
            var active = app == current;
            sb.Append($"""<a class="m-module-item{(active ? " active" : "")}" href="{Url(app.Href)}"{(active ? " aria-current=\"true\"" : "")}>""")
              .Append(AppMark(app))
              .Append($"""<span class="m-module-text"><span class="m-module-name">{E(app.Name)}</span>""")
              .Append(string.IsNullOrEmpty(app.Description) ? "" : $"""<span class="m-module-desc">{E(app.Description)}</span>""")
              .Append("</span>").Append(active ? Icon("check", "m-module-check") : "").Append("</a>");
        }
        if (HasLauncher(view.Content))
            sb.Append("""<div class="m-dropdown-divider"></div><button type="button" class="m-dropdown-item" data-m-launcher-open aria-controls="m-launcher">""").Append(Icon("grid-dots")).Append("All apps</button>");
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
          .Append("""<aside class="m-offcanvas-panel m-customizer-panel" role="dialog" aria-modal="true" aria-labelledby="m-customizer-title">""")
          .Append("""<div class="m-offcanvas-header"><span class="m-customizer-mark">""").Append(Icon("palette")).Append("</span>")
          .Append("""<div class="m-customizer-heading"><h2 class="m-offcanvas-title" id="m-customizer-title">Theme settings</h2>""")
          .Append("""<p class="m-offcanvas-subtitle">Saved in this browser and applied instantly.</p></div>""")
          .Append("""<button type="button" class="m-icon-btn" data-m-customizer-close aria-label="Close">""").Append(Icon("x")).Append("</button></div>")
          // Two panes, so neither is a long scroll: how the app looks, and how its shell is laid out.
          .Append("""<div class="m-setting-tabs" role="tablist" aria-label="Theme settings">""")
          .Append("""<button type="button" class="m-setting-tab" role="tab" id="m-setting-tab-theme" aria-controls="m-setting-pane-theme" aria-selected="true" data-m-customizer-tab="theme">""").Append(Icon("brush")).Append("Appearance</button>")
          .Append("""<button type="button" class="m-setting-tab" role="tab" id="m-setting-tab-layout" aria-controls="m-setting-pane-layout" aria-selected="false" tabindex="-1" data-m-customizer-tab="layout">""").Append(Icon("layout-dashboard")).Append("Layout</button>")
          .Append("</div>")
          .Append("""<form class="m-offcanvas-body" data-m-customizer-form>""")
          .Append("""<div class="m-setting-pane" id="m-setting-pane-theme" role="tabpanel" aria-labelledby="m-setting-tab-theme">""");

        Choice(sb, "Color mode", "mode", view.Mode ?? "system", [("light", "Light", Icon("sun")), ("dark", "Dark", Icon("moon")), ("system", "System", Icon("device-desktop"))]);

        sb.Append("""<fieldset class="m-setting"><legend class="m-setting-label">Color scheme</legend><div class="m-swatches">""");
        foreach (var (name, hex) in o.ColorPresets)
        {
            var css = ThemeCss.BuildForPrimary(o, hex);
            sb.Append($"""<label class="m-swatch" title="{E(Capitalize(name))}"><input type="radio" name="primary" value="{E(name)}" data-css="{E(css)}"{(name == preset ? " checked" : "")} />""")
              .Append($"""<span class="m-swatch-color" style="background:{E(hex)}">{Icon("check", "m-swatch-check")}</span><span class="m-sr-only">{E(Capitalize(name))}</span></label>""");
        }
        sb.Append("</div></fieldset>");

        // Each base previews its own surfaces, light on the left and dark on the right.
        sb.Append("""<fieldset class="m-setting"><legend class="m-setting-label">Theme base</legend><div class="m-choices m-choices-base">""");
        var currentBase = Lower(o.Base);
        foreach (var (value, text, hint, palette) in BasePalettes)
        {
            sb.Append($"""<label class="m-choice m-base-choice" title="{E(text)}: {E(hint)}"><input type="radio" name="base" value="{value}"{(value == currentBase ? " checked" : "")} />""")
              .Append($"""<span class="m-choice-box"><span class="m-base-preview" style="{palette}" aria-hidden="true"><i><b></b><b></b></i><i><b></b><b></b></i></span>""")
              .Append($"""<span class="m-base-text"><span class="m-base-name">{E(text)}</span><span class="m-base-hint">{E(hint)}</span></span></span></label>""");
        }
        sb.Append("</div></fieldset>");

        Choice(sb, "Font family", "font", Lower(o.Font),
        [
            ("sans", "Inter", """<span class="m-font-preview m-font-sans">Aa</span>"""),
            ("system", "System", """<span class="m-font-preview m-font-system">Aa</span>"""),
            ("serif", "Serif", """<span class="m-font-preview m-font-serif">Aa</span>"""),
            ("mono", "Mono", """<span class="m-font-preview m-font-mono">Aa</span>"""),
        ], "m-choices-compact");
        Choice(sb, "Corner radius", "radius", o.BorderRadius.ToString(System.Globalization.CultureInfo.InvariantCulture),
            new[] { 0, 4, 6, 8, 12 }.Select(r => (r.ToString(System.Globalization.CultureInfo.InvariantCulture), r.ToString(System.Globalization.CultureInfo.InvariantCulture),
                (string?)$"""<span class="m-radius-preview" style="border-start-start-radius:{r * 1.5}px"></span>""")).ToArray(), "m-choices-radius");
        sb.Append("""</div><div class="m-setting-pane" id="m-setting-pane-layout" role="tabpanel" aria-labelledby="m-setting-tab-layout" hidden>""");

        var pick = LayoutPick.From(o);
        sb.Append("""<fieldset class="m-setting"><legend class="m-setting-label">Presets</legend><div class="m-choices m-choices-presets">""");
        foreach (var (value, text, layout) in LayoutPresets)
        {
            sb.Append($"""<label class="m-choice" title="{E(text)}"><input type="radio" name="preset" value="{value}" data-preset="{E(layout.Json())}"{(layout == pick ? " checked" : "")} />""")
              .Append("""<span class="m-choice-box">""").Append(Mini(layout)).Append($"<span>{E(text)}</span></span></label>");
        }
        sb.Append("</div></fieldset>");

        var basePick = new LayoutPick();
        var vertical = basePick with { Layout = "vertical" };
        Choice(sb, "Navigation", "layout", Lower(o.Layout),
            [("horizontal", "Horizontal", Mini(basePick)), ("condensed", "Condensed", Mini(basePick with { Layout = "condensed" })), ("vertical", "Vertical", Mini(vertical))]);
        Choice(sb, "Content width", "width", Lower(o.ContentWidth),
            [("boxed", "Boxed", Mini(basePick)), ("fluid", "Fluid", Mini(basePick with { Width = "fluid" })), ("framed", "Framed", Mini(basePick with { Width = "framed" }))]);
        Choice(sb, "Header", "header", Lower(o.Header),
            Enum.GetValues<HeaderMode>().Select(m => (Lower(m), m.ToString(), (string?)Mini(basePick with { Header = Lower(m) }))).ToArray(), "m-choices-4");
        Choice(sb, "Header color", "topbar", Lower(o.HeaderTheme),
            [("light", "Light", Mini(basePick)), ("dark", "Dark", Mini(basePick with { Topbar = "dark" }))]);
        Choice(sb, "Menu color", "nav", Lower(o.NavTheme), [("light", "Light", Icon("sun")), ("dark", "Dark", Icon("moon"))]);
        Choice(sb, "Sidebar position", "side", Lower(o.SidebarPosition),
            [("left", "Start", Mini(vertical)), ("right", "End", Mini(vertical with { Side = "right" }))], "m-setting-vertical");
        Choice(sb, "Sidebar style", "sidebar", Lower(o.SidebarStyle),
            Enum.GetValues<SidebarStyle>().Select(m => (Lower(m), m.ToString(), (string?)Mini(vertical with { Sidebar = Lower(m) }))).ToArray(), "m-setting-vertical");
        Choice(sb, "Sidebar size", "fold", Lower(o.SidebarFold),
            [("full", "Full", Mini(vertical)), ("folded", "Folded", Mini(vertical with { Fold = "folded" })), ("hover", "Folded hover", Mini(vertical with { Fold = "hover" }))], "m-setting-vertical");
        Choice(sb, "Menu", "menu", Lower(o.MenuScope), [("all", "All menus", Thumb("menu-all")), ("app", "Current app", Thumb("menu-app"))]);
        Choice(sb, "Direction", "dir", Lower(o.Direction),
            [("ltr", "Left to right", Mini(basePick)), ("rtl", "Right to left", Mini(basePick with { Dir = "rtl" }))]);

        sb.Append("""</div></form><div class="m-offcanvas-footer m-customizer-footer"><button type="button" class="ant-btn ant-btn-default" data-m-customizer-reset>""").Append(Icon("refresh")).Append("""<span>Reset</span></button>""")
          .Append("""<button type="button" class="ant-btn ant-btn-primary" data-m-customizer-close><span>Done</span></button></div>""")
          .Append("</aside></div>");
        return sb.ToString();
    }

    // visual: markup above the label (an icon, a preview); trusted, built by this class.
    private static void Choice(StringBuilder sb, string label, string name, string current, (string Value, string Text, string? Visual)[] options, string? cssClass = null)
    {
        var fieldset = cssClass is not null && cssClass.StartsWith("m-setting-", StringComparison.Ordinal);
        sb.Append($"""<fieldset class="m-setting{(fieldset ? " " + cssClass : "")}"><legend class="m-setting-label">{E(label)}</legend>""")
          .Append(fieldset ? """<p class="m-setting-note">Vertical layout only.</p>""" : "")
          .Append($"""<div class="m-choices{(cssClass is null || fieldset ? "" : " " + cssClass)}">""");
        foreach (var (value, text, visual) in options)
        {
            sb.Append($"""<label class="m-choice"><input type="radio" name="{E(name)}" value="{E(value)}"{(value == current ? " checked" : "")} />""")
              .Append("""<span class="m-choice-box">""").Append(visual ?? "").Append($"<span>{E(text)}</span></span></label>");
        }
        sb.Append("</div></fieldset>");
    }

    // Theme bases with their light and dark surfaces (modulus.css), for the previews in the theme settings.
    private static readonly (string Value, string Text, string Hint, string Palette)[] BasePalettes =
    [
        ("gray", "Gray", "Balanced", Palette("#f6f8fb", "#e5e7eb", "#9ca3af", "#111827", "#1a2332", "#2e3c51")),
        ("slate", "Slate", "Cool blue", Palette("#f4f6fa", "#e2e8f0", "#94a3b8", "#0b1120", "#111a2e", "#26344b")),
        ("zinc", "Zinc", "Crisp", Palette("#f6f6f7", "#e4e4e7", "#a1a1aa", "#111113", "#18181b", "#2e2e33")),
        ("neutral", "Neutral", "True gray", Palette("#f6f6f6", "#e5e5e5", "#a3a3a3", "#101010", "#171717", "#2e2e2e")),
        ("stone", "Stone", "Warm", Palette("#f7f6f4", "#e7e5e4", "#a8a29e", "#12100f", "#1c1917", "#34302c")),
    ];

    private static string Palette(string lightBg, string lightLine, string lightInk, string darkBg, string darkCard, string darkLine) =>
        $"--bp-l-bg:{lightBg};--bp-l-line:{lightLine};--bp-l-ink:{lightInk};--bp-d-bg:{darkBg};--bp-d-card:{darkCard};--bp-d-line:{darkLine}";

    /// <summary>The layout settings a preset sets; every other setting keeps its value.</summary>
    private sealed record LayoutPick(string Layout = "horizontal", string Width = "boxed", string Side = "left", string Sidebar = "default",
        string Fold = "full", string Header = "fixed", string Topbar = "light", string Dir = "ltr")
    {
        public static LayoutPick From(ModulusThemeOptions o) => new(Lower(o.Layout), Lower(o.ContentWidth), Lower(o.SidebarPosition),
            Lower(o.SidebarStyle), Lower(o.SidebarFold), Lower(o.Header), Lower(o.HeaderTheme), Lower(o.Direction));

        public string Json() =>
            $$"""{"layout":"{{Layout}}","width":"{{Width}}","side":"{{Side}}","sidebar":"{{Sidebar}}","fold":"{{Fold}}","header":"{{Header}}","topbar":"{{Topbar}}","dir":"{{Dir}}"}""";
    }

    /// <summary>Named layouts offered as one-click presets (the Tabler / Vuexy demo layouts).</summary>
    private static readonly (string Value, string Text, LayoutPick Pick)[] LayoutPresets =
    [
        ("horizontal", "Horizontal", new()),
        ("condensed", "Condensed", new() { Layout = "condensed" }),
        ("fluid", "Fluid", new() { Width = "fluid" }),
        ("boxed", "Boxed", new() { Width = "framed" }),
        ("navbar-dark", "Navbar dark", new() { Topbar = "dark" }),
        ("navbar-sticky", "Navbar sticky", new() { Header = "sticky" }),
        ("navbar-floating", "Navbar floating", new() { Header = "floating" }),
        ("vertical", "Vertical", new() { Layout = "vertical" }),
        ("fluid-vertical", "Fluid vertical", new() { Layout = "vertical", Width = "fluid" }),
        ("right-vertical", "Right vertical", new() { Layout = "vertical", Side = "right" }),
        ("vertical-transparent", "Vertical transparent", new() { Layout = "vertical", Sidebar = "transparent" }),
        ("vertical-floating", "Vertical floating", new() { Layout = "vertical", Sidebar = "floating" }),
        ("folded", "Folded sidebar", new() { Layout = "vertical", Fold = "folded" }),
        ("folded-hover", "Folded hover", new() { Layout = "vertical", Fold = "hover" }),
        ("rtl", "RTL mode", new() { Dir = "rtl" }),
    ];

    // Miniature of a layout (72 x 46), coloured by modulus-shell.css (.m-mini-*) so it follows the theme.
    private static string Mini(LayoutPick p)
    {
        var sb = new StringBuilder("""<svg class="m-mini" viewBox="0 0 72 46" aria-hidden="true" focusable="false">""");
        if (p.Dir == "rtl") sb.Append("""<g transform="matrix(-1 0 0 1 72 0)">""");
        double x0 = 0, x1 = 72;
        if (p.Width == "framed")
        {
            Rect(sb, 0, 0, 72, 46, "m-mini-backdrop");
            (x0, x1) = (8, 64);
        }
        Rect(sb, x0, 0, x1 - x0, 46, "m-mini-bg");

        double cx0 = x0, cx1 = x1;
        if (p.Layout == "vertical")
        {
            var floating = p.Sidebar == "floating";
            var width = p.Fold == "full" ? 16 : 7;
            var inset = floating ? 2 : 0;
            var right = p.Side == "right";
            var sx = right ? x1 - inset - width : x0 + inset;
            var surface = p.Sidebar == "transparent" ? "m-mini-bg" : "m-mini-nav";
            Rect(sb, sx, inset, width, 46 - 2 * inset, surface, floating ? 2 : 0);
            if (p.Sidebar == "default") Rect(sb, right ? sx : sx + width - 0.5, 0, 0.5, 46, "m-mini-edge");
            if (p.Fold == "hover") Rect(sb, right ? sx + width - 16 : sx, inset + 0.5, 16, 45 - 2 * inset, "m-mini-ghost", 1.5);
            Rect(sb, sx + (width - 4) / 2.0, inset + 3, 4, 4, "m-mini-accent", 1);
            for (var i = 0; i < 4; i++)
            {
                var y = inset + 11 + i * 5;
                if (p.Fold == "full") Rect(sb, sx + 3, y, i == 0 ? 10 : 8, 2, i == 0 ? "m-mini-accent" : "m-mini-navline", 1);
                else Rect(sb, sx + 2.5, y, 2, 2, i == 0 ? "m-mini-accent" : "m-mini-navline", 0.5);
            }
            if (right) cx1 = sx; else cx0 = sx + width;
        }

        var floatingHeader = p.Header == "floating";
        var horizontal = p.Layout == "horizontal";
        double hx0 = cx0 + (floatingHeader ? 2 : 0), hx1 = cx1 - (floatingHeader ? 2 : 0), hy = floatingHeader ? 2 : 0;
        var headerHeight = horizontal ? 11 : 7;
        var topbar = p.Topbar == "dark" ? "m-mini-dark" : "m-mini-surface";
        Rect(sb, hx0, hy, hx1 - hx0, headerHeight, "m-mini-surface", floatingHeader ? 1.5 : 0);
        Rect(sb, hx0, hy, hx1 - hx0, 7, topbar, floatingHeader ? 1.5 : 0);
        if (horizontal) Rect(sb, hx0, hy + 7, hx1 - hx0, 4, "m-mini-nav", floatingHeader ? 1.5 : 0);
        if (!floatingHeader && p.Header != "static") Rect(sb, hx0, hy + (p.Header == "sticky" || !horizontal ? headerHeight : 7), hx1 - hx0, 0.75, "m-mini-shadow");
        if (floatingHeader) Rect(sb, hx0, hy, hx1 - hx0, headerHeight, "m-mini-outline", 1.5);
        var line = p.Topbar == "dark" ? "m-mini-darkline" : "m-mini-line";
        if (p.Layout != "vertical") Rect(sb, hx0 + 3, hy + 2, 3, 3, "m-mini-accent", 0.75);
        Circle(sb, hx1 - 4, hy + 3.5, 1.5, line);
        if (p.Layout == "condensed")
            for (var i = 0; i < 3; i++) Rect(sb, hx0 + 9 + i * 7, hy + 3, 5, 1.2, i == 0 ? "m-mini-accent" : line, 0.6);
        if (horizontal)
            for (var i = 0; i < 4; i++) Rect(sb, hx0 + 3 + i * 7, hy + 8.4, 5, 1.2, i == 0 ? "m-mini-accent" : "m-mini-navline", 0.6);

        var gutter = p.Width == "boxed" && p.Layout != "vertical" ? 8 : 3;
        double top = hy + headerHeight + 4, left = cx0 + gutter, width2 = cx1 - cx0 - 2 * gutter;
        Rect(sb, left, top, width2, 46 - top - 3, "m-mini-card", 1.5);
        Rect(sb, left + 3, top + 3, width2 * 0.45, 1.6, "m-mini-line", 0.8);
        Rect(sb, left + 3, top + 7, width2 * 0.7, 1.2, "m-mini-line", 0.6);
        Rect(sb, left + 3, top + 10.5, width2 * 0.3, 1.2, "m-mini-line", 0.6);
        if (p.Dir == "rtl") sb.Append("</g>");
        return sb.Append("</svg>").ToString();

        static void Rect(StringBuilder sb, double x, double y, double w, double h, string cls, double r = 0) =>
            sb.Append(System.FormattableString.Invariant($"""<rect class="{cls}" x="{x}" y="{y}" width="{w}" height="{h}"{(r > 0 ? $" rx=\"{r}\"" : "")}/>"""));

        static void Circle(StringBuilder sb, double x, double y, double r, string cls) =>
            sb.Append(System.FormattableString.Invariant($"""<circle class="{cls}" cx="{x}" cy="{y}" r="{r}"/>"""));
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
