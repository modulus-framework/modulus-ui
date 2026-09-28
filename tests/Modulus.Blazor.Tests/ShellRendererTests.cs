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
    public void OverlayLauncher_GreetsTheUser_FilesAppsUnderTheirGroups_AndMarksTheCurrentApp()
    {
        var sales = new MenuItemDefinition("sales", "Sales", null, "shopping-cart");
        sales.Children.Add(new MenuItemDefinition("orders", "Orders", "apps/orders"));
        var view = new ShellView(new ModulusThemeOptions(), [sales], "apps/orders", new ShellContent
        {
            User = new ShellUser("Serena Park"),
            AppLauncher = AppLauncherStyle.Overlay,
            Apps =
            [
                new("Sales", "apps/orders", "shopping-cart", "orange", "Orders & customers") { MenuKey = "sales", Group = "Sales & finance" },
                new("Email", "apps/email", "mail", "azure") { Group = "Productivity" },
                new("Invoicing", "apps/invoices", "file-invoice", "teal") { Group = "Sales & finance" },
            ],
        });

        var html = ShellRenderer.AppLauncher(view);

        Assert.Contains("""<h2 class="m-launcher-title" id="m-launcher-title" data-m-greeting="Serena">Welcome back, Serena</h2>""", html);
        // One section per group, in order of first use; Invoicing joins Sales although Email comes between them.
        Assert.Equal(2, html.Split("""<section class="m-launcher-group">""").Length - 1);
        var finance = html.IndexOf("Sales &amp; finance</h3>", StringComparison.Ordinal);
        Assert.True(finance < html.IndexOf(">Invoicing<", StringComparison.Ordinal));
        Assert.True(html.IndexOf(">Invoicing<", StringComparison.Ordinal) < html.IndexOf("Productivity</h3>", StringComparison.Ordinal));
        Assert.Contains("""<a class="m-launcher-app active" href="/apps/orders" data-m-launcher-name="Sales Orders &amp; customers Sales &amp; finance" aria-current="true">""", html);
        Assert.Contains("""<span class="m-launcher-desc">Orders &amp; customers</span></span><span class="m-launcher-open-tag">Open</span>""", html);
        Assert.Contains("""<aside class="m-launcher-rail" data-m-recent hidden>""", html);
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
