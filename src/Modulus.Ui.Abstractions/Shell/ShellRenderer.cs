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
public sealed record ShellView(ModulusThemeOptions Options, IReadOnlyList<MenuItemDefinition> Menu, string Path, ShellContent Content, string? Mode = null, string? TenantId = null);

/// <summary>
/// Renders the Tabler-style application shell (sidebar, header, horizontal navbar, footer and the
/// theme settings panel) as HTML. Blazor and MVC / Razor Pages both output these strings, so the
/// shell DOM is identical in every flavor. Behaviour lives in <c>modulus-shell.js</c>.
/// <para>
/// Menu depth: a top-level item with children becomes a dropdown (horizontal) or a collapsible group
/// (vertical). A child that has children of its own is a section: a header followed by its links.
/// </para>
/// </summary>
public static class ShellRenderer
{
    public const string IconSprite = "/_content/Modulus.Ui.Assets/icons/tabler.svg";

    /// <summary>Cookie the tenant switcher writes when a tenant has no link of its own.</summary>
    public const string TenantCookie = "modulus-tenant";

    /// <summary>Tabler outline icon: <c>&lt;svg class="m-ti"&gt;&lt;use href="…#ti-name"/&gt;&lt;/svg&gt;</c>.</summary>
    public static string Icon(string name, string? cssClass = null) =>
        $"""<svg class="m-ti{(cssClass is null ? "" : " " + cssClass)}" aria-hidden="true" focusable="false"><use href="{IconSprite}#ti-{E(name)}"></use></svg>""";

    // Sidebar (vertical layout, and the navigation drawer on small screens) ------------------------

    public static string Sidebar(ShellView view)
    {
        var sb = new StringBuilder();
        sb.Append("""<aside class="m-sidebar" id="m-sidebar" aria-label="Main navigation">""");
        sb.Append("""<div class="m-sidebar-brand">""").Append(Brand(view.Options)).Append("""<button type="button" class="m-icon-btn m-sidebar-close" data-m-nav-close aria-label="Close navigation">""").Append(Icon("x")).Append("</button></div>");
        sb.Append("""<nav class="m-sidebar-nav"><ul class="m-snav">""");
        foreach (var item in view.Menu)
            SidebarItem(sb, item, view.Path);
        sb.Append("</ul></nav></aside>");
        sb.Append("""<div class="m-sidebar-backdrop" data-m-nav-close></div>""");
        return sb.ToString();
    }

    private static void SidebarItem(StringBuilder sb, MenuItemDefinition item, string path)
    {
        if (item.Children.Count == 0)
        {
            var active = item.Matches(path);
            sb.Append($"""<li class="m-snav-item{(active ? " active" : "")}"><a class="m-snav-link" href="{Href(item)}"{(active ? " aria-current=\"page\"" : "")}>""")
              .Append(item.Icon is null ? "" : Icon(item.Icon, "m-snav-icon"))
              .Append($"""<span class="m-snav-title">{E(item.Label)}</span></a></li>""");
            return;
        }

        var open = IsActive(item, path);
        sb.Append($"""<li class="m-snav-item{(open ? " active" : "")}"><details class="m-snav-group"{(open ? " open" : "")}><summary class="m-snav-link">""")
          .Append(item.Icon is null ? "" : Icon(item.Icon, "m-snav-icon"))
          .Append($"""<span class="m-snav-title">{E(item.Label)}</span>""").Append(Icon("chevron-down", "m-snav-chevron"))
          .Append("""</summary><ul class="m-snav-sub">""");
        foreach (var child in item.Children)
        {
            if (child.Children.Count == 0)
            {
                SidebarLeaf(sb, child, path);
                continue;
            }
            sb.Append($"""<li class="m-snav-header">{E(child.Label)}</li>""");
            foreach (var leaf in child.Children) SidebarLeaf(sb, leaf, path);
        }
        sb.Append("</ul></details></li>");
    }

    private static void SidebarLeaf(StringBuilder sb, MenuItemDefinition item, string path)
    {
        var active = item.Matches(path);
        sb.Append($"""<li class="m-snav-item{(active ? " active" : "")}"><a class="m-snav-link" href="{Href(item)}"{(active ? " aria-current=\"page\"" : "")}><span class="m-snav-title">{E(item.Label)}</span></a></li>""");
    }

    // Header ---------------------------------------------------------------------------------------

    public static string Topbar(ShellView view)
    {
        var c = view.Content;
        var sb = new StringBuilder();
        sb.Append("""<header class="m-topbar"><div class="m-container m-topbar-inner">""");
        sb.Append("""<button type="button" class="m-icon-btn m-nav-toggle" data-m-nav-open aria-controls="m-sidebar" aria-label="Open navigation">""").Append(Icon("menu-2")).Append("</button>");
        sb.Append(Brand(view.Options, "m-topbar-brand"));
        TenantSwitcher(sb, view);

        if (c.SearchPlaceholder is { } placeholder)
        {
            sb.Append("""<div class="m-search" data-m-search role="search">""")
              .Append(Icon("search", "m-search-icon"))
              .Append($"""<input type="search" class="m-search-input" placeholder="{E(placeholder)}" aria-label="Search pages" autocomplete="off" spellcheck="false" aria-controls="m-search-results" aria-expanded="false" />""")
              .Append("""<kbd class="m-search-kbd" aria-hidden="true">Ctrl K</kbd>""")
              .Append("""<div class="m-search-results" id="m-search-results" role="listbox" hidden></div></div>""");
        }

        sb.Append("""<div class="m-topbar-actions">""");
        sb.Append("""<button type="button" class="m-icon-btn m-search-open" data-m-search-open aria-label="Search">""").Append(Icon("search")).Append("</button>");
        AppSwitcher(sb, c);
        sb.Append("""<button type="button" class="m-icon-btn" data-m-theme-toggle aria-label="Toggle dark mode">""")
          .Append(Icon("moon", "m-show-light")).Append(Icon("sun", "m-show-dark")).Append("</button>");
        if (view.Options.ShowCustomizer)
            sb.Append("""<button type="button" class="m-icon-btn" data-m-customizer-open aria-controls="m-customizer" aria-label="Theme settings">""").Append(Icon("palette")).Append("</button>");

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
        sb.Append("""<details class="m-dropdown m-user"><summary class="m-user-toggle" aria-label="Account menu">""")
          .Append(Avatar(user))
          .Append($"""<span class="m-user-text"><span class="m-user-name">{E(user.Name)}</span>""")
          .Append(user.Subtitle is null ? "" : $"""<span class="m-user-subtitle">{E(user.Subtitle)}</span>""")
          .Append("""</span></summary><div class="m-dropdown-menu m-dropdown-end" role="menu">""");
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

    private static void TenantSwitcher(StringBuilder sb, ShellView view)
    {
        if (CurrentTenant(view) is not { } current) return;
        var tenants = view.Content.Tenants!;
        sb.Append("""<details class="m-dropdown m-tenant" data-m-tenants><summary class="m-tenant-toggle" aria-label="Switch tenant" aria-haspopup="listbox">""")
          .Append(TenantMark(current, tenants))
          .Append($"""<span class="m-tenant-text"><span class="m-tenant-name">{E(current.Name)}</span>""")
          .Append(current.Subtitle is null ? "" : $"""<span class="m-tenant-subtitle">{E(current.Subtitle)}</span>""")
          .Append("</span>").Append(Icon("selector", "m-tenant-chevron"))
          .Append("""</summary><div class="m-dropdown-menu m-tenant-menu"><div class="m-dropdown-header">Switch tenant</div>""");
        if (tenants.Count > TenantFilterThreshold)
            sb.Append("""<div class="m-tenant-filter">""").Append(Icon("search"))
              .Append("""<input type="search" class="m-tenant-filter-input" data-m-tenant-filter placeholder="Find a tenant…" aria-label="Find a tenant" autocomplete="off" /></div>""");
        sb.Append("""<div class="m-tenant-list" role="listbox" aria-label="Tenants">""");
        foreach (var t in tenants)
        {
            var selected = t == current;
            var body = TenantMark(t, tenants)
                + $"""<span class="m-tenant-text"><span class="m-tenant-name">{E(t.Name)}</span>"""
                + (t.Subtitle is null ? "" : $"""<span class="m-tenant-subtitle">{E(t.Subtitle)}</span>""")
                + "</span>" + (selected ? Icon("check", "m-tenant-check") : "");
            var cls = $"m-dropdown-item m-tenant-item{(selected ? " active" : "")}";
            var aria = $""" role="option" aria-selected="{(selected ? "true" : "false")}" data-m-tenant-name="{E(t.Name)}" """;
            sb.Append(t.Href is null
                ? $"""<button type="button" class="{cls}"{aria}data-m-tenant="{E(t.Id)}">{body}</button>"""
                : $"""<a class="{cls}"{aria}href="{Url(t.Href)}">{body}</a>""");
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

    // Horizontal navbar ---------------------------------------------------------------------------------

    public static string Navbar(ShellView view)
    {
        var sb = new StringBuilder();
        sb.Append("""<nav class="m-navbar" aria-label="Main navigation"><div class="m-container"><ul class="m-hnav">""");
        foreach (var item in view.Menu)
        {
            var active = IsActive(item, view.Path);
            if (item.Children.Count == 0)
            {
                sb.Append($"""<li class="m-hnav-item{(active ? " active" : "")}"><a class="m-hnav-link" href="{Href(item)}"{(active ? " aria-current=\"page\"" : "")}>""")
                  .Append(item.Icon is null ? "" : Icon(item.Icon, "m-hnav-icon"))
                  .Append($"""<span>{E(item.Label)}</span></a></li>""");
                continue;
            }

            var sections = item.Children.Where(c => c.Children.Count > 0).ToList();
            var loose = item.Children.Where(c => c.Children.Count == 0).ToList();
            var columns = sections.Count + (loose.Count > 0 ? 1 : 0);
            sb.Append($"""<li class="m-hnav-item{(active ? " active" : "")}"><details class="m-dropdown"><summary class="m-hnav-link">""")
              .Append(item.Icon is null ? "" : Icon(item.Icon, "m-hnav-icon"))
              .Append($"""<span>{E(item.Label)}</span>""").Append(Icon("chevron-down", "m-hnav-chevron"))
              .Append($"""</summary><div class="m-dropdown-menu{(columns > 1 ? $" m-dropdown-columns m-dropdown-columns-{Math.Min(columns, 4)}" : "")}">""");
            if (loose.Count > 0)
            {
                if (columns > 1) sb.Append("""<div class="m-dropdown-col">""");
                foreach (var leaf in loose) DropdownItem(sb, leaf, view.Path);
                if (columns > 1) sb.Append("</div>");
            }
            foreach (var section in sections)
            {
                sb.Append($"""<div class="m-dropdown-col"><div class="m-dropdown-header">{E(section.Label)}</div>""");
                foreach (var leaf in section.Children) DropdownItem(sb, leaf, view.Path);
                sb.Append("</div>");
            }
            sb.Append("</div></details></li>");
        }
        sb.Append("</ul></div></nav>");
        return sb.ToString();
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
          .Append("""<div class="m-offcanvas-header"><h2 class="m-offcanvas-title" id="m-customizer-title">Theme settings</h2>""")
          .Append("""<button type="button" class="m-icon-btn" data-m-customizer-close aria-label="Close">""").Append(Icon("x")).Append("</button></div>")
          .Append("""<form class="m-offcanvas-body" data-m-customizer-form>""");

        Choice(sb, "Color mode", "mode", view.Mode ?? "system", [("light", "Light", "sun"), ("dark", "Dark", "moon"), ("system", "System", "device-desktop")]);

        sb.Append("""<fieldset class="m-setting"><legend class="m-setting-label">Color scheme</legend><div class="m-swatches">""");
        foreach (var (name, hex) in o.ColorPresets)
        {
            var css = ThemeCss.BuildForPrimary(o, hex);
            sb.Append($"""<label class="m-swatch" title="{E(Capitalize(name))}"><input type="radio" name="primary" value="{E(name)}" data-css="{E(css)}"{(name == preset ? " checked" : "")} />""")
              .Append($"""<span class="m-swatch-color" style="background:{E(hex)}"></span><span class="m-sr-only">{E(Capitalize(name))}</span></label>""");
        }
        sb.Append("</div></fieldset>");

        Choice(sb, "Layout", "layout", o.Layout == ShellLayout.Vertical ? "vertical" : "horizontal", [("horizontal", "Horizontal", "layout-navbar"), ("vertical", "Vertical", "layout-sidebar")]);
        Choice(sb, "Navigation", "nav", o.NavTheme == NavTheme.Dark ? "dark" : "light", [("light", "Light", "sun"), ("dark", "Dark", "moon")]);
        Choice(sb, "Content width", "width", o.ContentWidth == ContentWidth.Fluid ? "fluid" : "boxed", [("boxed", "Boxed", "layout"), ("fluid", "Fluid", "arrows-horizontal")]);
        Choice(sb, "Corner radius", "radius", o.BorderRadius.ToString(System.Globalization.CultureInfo.InvariantCulture), [("0", "0", null), ("4", "4", null), ("6", "6", null), ("8", "8", null), ("12", "12", null)]);

        sb.Append("""</form><div class="m-offcanvas-footer"><button type="button" class="ant-btn ant-btn-default ant-btn-block" data-m-customizer-reset><span>Reset to defaults</span></button></div>""")
          .Append("</aside></div>");
        return sb.ToString();
    }

    private static void Choice(StringBuilder sb, string label, string name, string current, (string Value, string Text, string? Icon)[] options)
    {
        sb.Append($"""<fieldset class="m-setting"><legend class="m-setting-label">{E(label)}</legend><div class="m-choices">""");
        foreach (var (value, text, icon) in options)
        {
            sb.Append($"""<label class="m-choice"><input type="radio" name="{E(name)}" value="{E(value)}"{(value == current ? " checked" : "")} />""")
              .Append("""<span class="m-choice-box">""").Append(icon is null ? "" : Icon(icon)).Append($"<span>{E(text)}</span></span></label>");
        }
        sb.Append("</div></fieldset>");
    }

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
