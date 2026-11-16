using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Theming;

namespace Modulus.Ui.Abstractions.Shell;
public static partial class ShellRenderer
{
    /// <summary>
    /// The header row and the horizontal navbar in one wrapper, which the theme settings' header modes (sticky,
    /// floating) and the condensed layout (menu inside the header row) style as a unit. Render it once, after the sidebar.
    /// </summary>
    public static string Header(ShellView view) =>
        """<div class="m-header" id="m-header">""" + Topbar(view) + Navbar(view) +
        // The module bar (with its trail) is hidden below 992 px, so the trail is repeated in a strip that shows only there.
        (view.Options.ShowAppTrail && ModuleTrail(view) is { Length: > 0 } strip ? $"""<div class="m-crumbs-bar">{strip}</div>""" : "") + "</div>";

    public static string Topbar(ShellView view)
    {
        var c = view.Content;
        var sb = new StringBuilder();
        sb.Append("""<header class="m-topbar"><div class="m-container m-topbar-inner">""");
        sb.Append("""<button type="button" class="m-icon-btn m-nav-toggle" data-m-nav-open aria-controls="m-sidebar" aria-label="Open navigation">""").Append(Icon("menu")).Append("</button>");
        sb.Append("""<button type="button" class="m-icon-btn m-fold-toggle" data-m-fold-toggle aria-controls="m-sidebar" aria-label="Fold sidebar" title="Fold sidebar">""").Append(Icon("menu-fold")).Append("</button>");
        if (HasLauncher(c))
            sb.Append("""<button type="button" class="m-icon-btn m-launcher-btn" data-m-launcher-open aria-controls="m-launcher" aria-haspopup="dialog" aria-label="All apps" title="All apps">""").Append(Icon("tb:grid-dots")).Append("</button>");
        sb.Append(Brand(view.Options, "m-topbar-brand"));

        if (c.SearchPlaceholder is { } placeholder)
        {
            sb.Append(view.ServerSearch ? ServerSearchBox(placeholder) : ClientSearchBox(placeholder));
        }

        sb.Append("""<div class="m-topbar-actions">""");
        sb.Append("""<button type="button" class="m-icon-btn m-search-open" data-m-search-open aria-label="Search">""").Append(Icon("search")).Append("</button>");
        // The utility buttons share one tray, so the row reads as search, tools, then company and account.
        sb.Append("""<div class="m-topbar-tools">""");
        if (!HasLauncher(c)) AppSwitcher(sb, c);
        // Full screen for the whole app (the browser's Fullscreen API); modulus-shell.js shows it where the browser allows it.
        sb.Append("""<button type="button" class="m-icon-btn m-fullscreen-btn" data-m-fullscreen aria-pressed="false" aria-label="Full screen" title="Full screen">""")
          .Append(Icon("fullscreen", "m-show-windowed")).Append(Icon("fullscreen-exit", "m-show-fullscreen")).Append("</button>");
        sb.Append("""<button type="button" class="m-icon-btn" data-m-theme-toggle aria-label="Toggle dark mode">""")
          .Append(Icon("moon", "m-show-light")).Append(Icon("sun", "m-show-dark")).Append("</button>");
        if (view.Options.ShowCustomizer)
            sb.Append("""<button type="button" class="m-icon-btn" data-m-customizer-open aria-controls="m-customizer" aria-label="Theme settings">""").Append(Icon("tb:palette")).Append("</button>");

        Activities(sb, c);

        if (c.Notices is { } notices)
        {
            var unread = notices.Count(n => n.Unread);
            sb.Append("""<details class="m-dropdown m-notices" data-m-notices><summary class="m-icon-btn" aria-label="Notifications">""")
              .Append(Icon("bell"))
              .Append(unread > 0 ? $"""<span class="m-notice-dot" aria-hidden="true"></span><span class="m-sr-only">{unread} unread</span>""" : "")
              .Append("""</summary><div class="m-dropdown-menu m-dropdown-end m-dropdown-card">""")
              .Append("""<div class="m-dropdown-card-header"><span class="m-dropdown-card-title">Notifications</span>""")
              .Append(unread > 0 ? MarkAllRead(c) : "")
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

        if (c.Chats is { } chats)
            Chats(sb, chats, c.ChatsHref);

        sb.Append("</div>");

        // Odoo's systray: the company sits with the account menu at the end of the row.
        var user = c.User;
        sb.Append("""<span class="m-topbar-sep" aria-hidden="true"></span>""");
        TenantSwitcher(sb, view);
        sb.Append("""<details class="m-dropdown m-user"><summary class="m-user-toggle" aria-label="Account menu">""")
          .Append("""<span class="m-user-avatar">""").Append(Avatar(user)).Append("""<span class="m-user-presence" aria-hidden="true"></span></span>""")
          .Append($"""<span class="m-user-text"><span class="m-user-name">{E(user.Name)}</span>""")
          .Append(user.Subtitle is null ? "" : $"""<span class="m-user-subtitle">{E(user.Subtitle)}</span>""")
          .Append("</span>").Append(Icon("down", "m-user-chevron"))
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
          .Append(Icon("tb:selector", "m-tenant-chevron"))
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
        var many = apps.Count > AppFilterThreshold;
        sb.Append("""<details class="m-dropdown m-apps"><summary class="m-icon-btn" aria-label="Apps">""").Append(Icon("tb:grid-dots"))
          .Append($"""</summary><div class="m-dropdown-menu m-dropdown-end m-dropdown-card m-apps-menu{(many ? " m-apps-menu-wide" : "")}" data-m-app-menu>""")
          .Append($"""<div class="m-dropdown-card-header"><span class="m-dropdown-card-title">Apps</span>{AppCount(apps)}</div>""");
        if (many) AppFilter(sb);
        sb.Append("""<div class="m-app-menu-body">""");
        foreach (var group in apps.GroupBy(a => a.Group))
        {
            AppGroupStart(sb, group.Key, apps);
            sb.Append("""<div class="m-apps-grid">""");
            foreach (var app in group)
            {
                sb.Append($"""<a class="m-app-tile" href="{Url(app.Href)}"{(app.Description is null ? "" : $" title=\"{E(app.Description)}\"")}>""")
                  .Append($"""<span class="m-icon-tile m-tile-{E(app.Tone)}-lt">{Icon(app.Icon)}</span>""")
                  .Append($"""<span class="m-app-name">{E(app.Name)}</span></a>""");
            }
            sb.Append("</div></section>");
        }
        sb.Append("""</div><div class="m-app-menu-empty" hidden>No apps match.</div>""");
        if (c.AppsHref is { } all)
            sb.Append($"""<a class="m-dropdown-card-footer" href="{Url(all)}">View all apps</a>""");
        sb.Append("</div></details>");
    }

    /// <summary>Apps listed before the app switchers show a filter box and lay the apps out wider.</summary>
    public const int AppFilterThreshold = 6;

    private static string AppCount(IReadOnlyList<ShellApp> apps) =>
        $"""<span class="m-app-menu-count">{apps.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)} {(apps.Count == 1 ? "app" : "apps")}</span>""";

    private static void AppFilter(StringBuilder sb) =>
        sb.Append("""<div class="m-app-filter">""").Append(Icon("search"))
          .Append("""<input type="search" class="m-app-filter-input" data-m-app-filter placeholder="Find an app…" aria-label="Find an app" autocomplete="off" spellcheck="false" /></div>""");

    // A group of apps in a switcher, headed by its name when any app has a group; the filter hides a group that has
    // no match left. The caller closes the section.
    private static void AppGroupStart(StringBuilder sb, string? heading, IReadOnlyList<ShellApp> apps)
    {
        sb.Append($"""<section class="m-app-menu-group" data-m-app-group="{E(heading ?? "")}">""");
        if (apps.Any(a => !string.IsNullOrEmpty(a.Group)))
            sb.Append($"""<div class="m-dropdown-header">{E(string.IsNullOrEmpty(heading) ? "Other" : heading)}</div>""");
    }

    private static void Activities(StringBuilder sb, ShellContent c)
    {
        if (c.Activities is not { } activities) return;
        var due = activities.Sum(a => a.Late + a.Today);
        var late = activities.Any(a => a.Late > 0);
        sb.Append("""<details class="m-dropdown m-activities" data-m-activities><summary class="m-icon-btn" aria-label="Activities">""")
          .Append(Icon("clock-circle"))
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

}
