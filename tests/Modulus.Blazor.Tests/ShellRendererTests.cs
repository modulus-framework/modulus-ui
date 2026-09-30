using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Modulus.Blazor.Shell;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Shell;
using Modulus.Ui.Abstractions.Theming;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>Header app switcher and tenant switcher (ShellRenderer, shared by AppShell and _ModulusLayout).</summary>
public class ShellRendererTests
{
    private static readonly ShellTenant[] Tenants =
    [
        new("acme", "Acme Corporation", "Enterprise"),
        new("globex", "Globex"),
        new("initech", "Initech", Href: "/t/initech"),
    ];

    private static ShellView View(ShellContent content, string? tenantCookie = null) =>
        new(new ModulusThemeOptions(), [], "", content, TenantId: tenantCookie);

    [Fact]
    public void NoAppsOrTenants_RendersNeitherSwitcher()
    {
        var html = ShellRenderer.Topbar(View(new ShellContent()));
        Assert.DoesNotContain("m-apps", html);
        Assert.DoesNotContain("m-tenant", html);
        Assert.Null(ShellRenderer.CurrentTenant(View(new ShellContent { Tenants = [] })));
    }

    [Fact]
    public void AppSwitcher_RendersTintedTilesAndTheViewAllLink()
    {
        var html = ShellRenderer.Topbar(View(new ShellContent
        {
            Apps = [new("Email", "apps/email", "mail", "blue", "Inbox & sent"), new("Chat", "https://chat.example.com", "messages")],
            AppsHref = "apps",
        }));

        Assert.Contains("""<details class="m-dropdown m-apps"><summary class="m-icon-btn" aria-label="Apps">""", html);
        Assert.Contains("""<a class="m-app-tile" href="/apps/email" title="Inbox &amp; sent"><span class="m-icon-tile m-tile-blue-lt">""", html);
        Assert.Contains("""<a class="m-app-tile" href="https://chat.example.com"><span class="m-icon-tile m-tile-primary-lt">""", html);
        Assert.Contains("""<span class="m-app-name">Chat</span>""", html);
        Assert.Contains("""<a class="m-dropdown-card-footer" href="/apps">View all apps</a>""", html);
        // Before the theme toggle, after the search button.
        Assert.True(html.IndexOf("m-apps", StringComparison.Ordinal) < html.IndexOf("data-m-theme-toggle", StringComparison.Ordinal));
    }

    [Fact]
    public void AppSwitchers_WithManyApps_AddAFilter_FileAppsUnderGroups_AndWiden()
    {
        var sales = new MenuItemDefinition("sales", "Sales", null, "shopping-cart");
        sales.Children.Add(new MenuItemDefinition("orders", "Orders", "apps/orders"));
        var apps = Enumerable.Range(1, 14)
            .Select(i => new ShellApp($"App {i}", $"apps/{i}", "box") { Group = i <= 7 ? "Operations" : "Finance" })
            .Prepend(new ShellApp("Sales", "apps/orders", "shopping-cart", "orange") { MenuKey = "sales", Group = "Operations" })
            .ToList();
        var view = new ShellView(new ModulusThemeOptions(), [sales], "apps/orders", new ShellContent { Apps = apps, AppLauncher = AppLauncherStyle.Overlay });

        var navbar = ShellRenderer.Navbar(view);
        Assert.Contains("""<div class="m-dropdown-menu m-module-menu m-module-menu-wide" data-m-app-menu>""", navbar);
        Assert.Contains("""<span class="m-app-menu-count">15 apps</span>""", navbar);
        Assert.Contains("data-m-app-filter", navbar);
        Assert.Equal(2, navbar.Split("""<section class="m-app-menu-group" """).Length - 1);
        Assert.Contains("""<a class="m-module-item active" href="/apps/orders" style="--m-app-tone: var(--m-orange)" aria-current="true">""", navbar);
        Assert.Contains("data-m-launcher-open", navbar);

        var topbar = ShellRenderer.Topbar(View(new ShellContent { Apps = apps }));
        Assert.Contains("m-apps-menu m-apps-menu-wide", topbar);
        Assert.Contains("""<div class="m-dropdown-header">Finance</div>""", topbar);

        // A handful of apps: no filter, one column, no group headings when no app has a group.
        var few = ShellRenderer.Topbar(View(new ShellContent { Apps = apps.Take(3).Select(a => a with { Group = null }).ToList() }));
        Assert.DoesNotContain("data-m-app-filter", few);
        Assert.DoesNotContain("m-apps-menu-wide", few);
        Assert.DoesNotContain("m-dropdown-header", few);
    }

    [Fact]
    public void OverlayLauncher_ShowsOneGridWithGroupsTogether_AndMarksTheCurrentApp()
    {
        var sales = new MenuItemDefinition("sales", "Sales", null, "shopping-cart");
        sales.Children.Add(new MenuItemDefinition("orders", "Orders", "apps/orders"));
        var view = new ShellView(new ModulusThemeOptions(), [sales], "apps/orders", new ShellContent
        {
            AppLauncher = AppLauncherStyle.Overlay,
            Apps =
            [
                new("Sales", "apps/orders", "shopping-cart", "orange", "Orders & customers") { MenuKey = "sales", Group = "Sales & finance" },
                new("Email", "apps/email", "mail", "azure") { Group = "Productivity" },
                new("Invoicing", "apps/invoices", "file-invoice", "teal") { Group = "Sales & finance" },
            ],
        });

        var html = ShellRenderer.AppLauncher(view);

        // One grid, no headings or greeting (Odoo's home menu); Invoicing joins Sales although Email comes between them.
        Assert.Equal(1, html.Split("""<div class="m-launcher-grid">""").Length - 1);
        Assert.DoesNotContain("<h2", html);
        Assert.DoesNotContain("<h3", html);
        Assert.True(html.IndexOf(">Invoicing<", StringComparison.Ordinal) < html.IndexOf(">Email<", StringComparison.Ordinal));
        Assert.Contains("""<a class="m-launcher-app active" href="/apps/orders" style="--m-app-tone: var(--m-orange); --m-i: 0" data-m-launcher-name="Sales Orders &amp; customers Sales &amp; finance" title="Orders &amp; customers" aria-current="true">""", html);
        Assert.Contains("""<span class="m-launcher-name">Sales</span></a>""", html);
        Assert.Empty(ShellRenderer.AppLauncher(View(new ShellContent { Apps = view.Content.Apps })));
    }

    [Fact]
    public void TenantSwitcher_ShowsTheCurrentTenant_ButtonsForCookieTenants_LinksForTheRest()
    {
        var html = ShellRenderer.Topbar(View(new ShellContent
        {
            Tenants = Tenants,
            TenantLinks = [new("Manage tenants", "settings/tenants", "settings")],
        }));

        Assert.Contains("""<details class="m-dropdown m-tenant" data-m-tenants><summary class="m-tenant-toggle" aria-label="Switch tenant" aria-haspopup="listbox"><span class="m-avatar m-avatar-sm m-tenant-mark m-tenant-mark-solid m-av-1">AC</span>""", html);
        Assert.Contains("""<span class="m-tenant-name">Acme Corporation</span><span class="m-tenant-subtitle">Enterprise</span>""", html);
        Assert.Contains("""<button type="button" class="m-dropdown-item m-tenant-item active" role="option" aria-selected="true" data-m-tenant-name="Acme Corporation" data-m-tenant="acme">""", html);
        Assert.Contains("""<button type="button" class="m-dropdown-item m-tenant-item" role="option" aria-selected="false" data-m-tenant-name="Globex" data-m-tenant="globex"><span class="m-avatar m-avatar-sm m-tenant-mark m-av-2">G</span>""", html);
        Assert.Contains("""<a class="m-dropdown-item m-tenant-item" role="option" aria-selected="false" data-m-tenant-name="Initech" href="/t/initech">""", html);
        Assert.Contains("""<a class="m-dropdown-item" href="/settings/tenants">""", html);
        Assert.DoesNotContain("data-m-tenant-filter", html); // three tenants: no filter box
        // In the header's actions (Odoo's systray), just before the account menu.
        var tenant = html.IndexOf("m-tenant", StringComparison.Ordinal);
        Assert.InRange(tenant, html.IndexOf("m-topbar-actions", StringComparison.Ordinal), html.IndexOf("m-dropdown m-user", StringComparison.Ordinal));
    }

    [Fact]
    public void CurrentTenant_PrefersTheContent_ThenTheCookie_ThenTheFirst()
    {
        Assert.Equal("globex", ShellRenderer.CurrentTenant(View(new ShellContent { Tenants = Tenants }, "globex"))!.Id);
        Assert.Equal("initech", ShellRenderer.CurrentTenant(View(new ShellContent { Tenants = Tenants, CurrentTenantId = "initech" }, "globex"))!.Id);
        Assert.Equal("acme", ShellRenderer.CurrentTenant(View(new ShellContent { Tenants = Tenants }, "deleted"))!.Id);

        var html = ShellRenderer.Topbar(View(new ShellContent { Tenants = Tenants }, "globex"));
        Assert.Contains("""<span class="m-tenant-name">Globex</span></span><svg class="m-ti m-tenant-chevron""", html);
        Assert.Contains("""aria-selected="true" data-m-tenant-name="Globex" """, html);
    }

    [Fact]
    public void TenantSwitcher_ManyTenantsGetAFilter_LogosReplaceInitials()
    {
        var many = Enumerable.Range(1, ShellRenderer.TenantFilterThreshold + 1)
            .Select(i => new ShellTenant($"t{i}", $"Tenant {i}", LogoUrl: i == 1 ? "/logo.png" : null)).ToList();
        var html = ShellRenderer.Topbar(View(new ShellContent { Tenants = many }));

        Assert.Contains("""<input type="search" class="m-tenant-filter-input" data-m-tenant-filter""", html);
        Assert.Contains("""<div class="m-dropdown-empty m-tenant-empty" hidden>No tenants match.</div>""", html);
        Assert.Contains("""<span class="m-avatar m-avatar-sm m-tenant-mark" style="background-image:url('/logo.png')"></span>""", html);
        Assert.Contains("m-av-6\">T6</span>", html);
    }

    [Fact]
    public void AppShell_PassesTheTenantIdToTheRenderer()
    {
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<IMenuProvider>(sp => new MenuProvider([], sp));
        ctx.Services.AddSingleton<IShellContentProvider, DefaultShellContentProvider>();
        ctx.Services.AddSingleton(Options.Create(new ModulusThemeOptions()));

        var cut = ctx.Render<AppShell>(p => p
            .Add(s => s.Content, new ShellContent { Tenants = Tenants })
            .Add(s => s.TenantId, "initech"));

        Assert.Equal("Initech", cut.Find(".m-tenant-toggle .m-tenant-name").TextContent);
    }
}
