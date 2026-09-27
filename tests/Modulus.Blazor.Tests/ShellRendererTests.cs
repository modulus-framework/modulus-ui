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
    public void TenantSwitcher_ShowsTheCurrentTenant_ButtonsForCookieTenants_LinksForTheRest()
    {
        var html = ShellRenderer.Topbar(View(new ShellContent
        {
            Tenants = Tenants,
            TenantLinks = [new("Manage tenants", "settings/tenants", "settings")],
        }));

        Assert.Contains("""<details class="m-dropdown m-tenant" data-m-tenants><summary class="m-tenant-toggle" aria-label="Switch tenant" aria-haspopup="listbox"><span class="m-avatar m-avatar-sm m-tenant-mark m-av-1">AC</span>""", html);
        Assert.Contains("""<span class="m-tenant-name">Acme Corporation</span><span class="m-tenant-subtitle">Enterprise</span>""", html);
        Assert.Contains("""<button type="button" class="m-dropdown-item m-tenant-item active" role="option" aria-selected="true" data-m-tenant-name="Acme Corporation" data-m-tenant="acme">""", html);
        Assert.Contains("""<button type="button" class="m-dropdown-item m-tenant-item" role="option" aria-selected="false" data-m-tenant-name="Globex" data-m-tenant="globex"><span class="m-avatar m-avatar-sm m-tenant-mark m-av-2">G</span>""", html);
        Assert.Contains("""<a class="m-dropdown-item m-tenant-item" role="option" aria-selected="false" data-m-tenant-name="Initech" href="/t/initech">""", html);
        Assert.Contains("""<a class="m-dropdown-item" href="/settings/tenants">""", html);
        Assert.DoesNotContain("data-m-tenant-filter", html); // three tenants: no filter box
        // Right after the brand, before the search box.
        var brand = html.IndexOf("m-topbar-brand", StringComparison.Ordinal);
        Assert.InRange(html.IndexOf("m-tenant", StringComparison.Ordinal), brand, html.IndexOf("data-m-search", StringComparison.Ordinal));
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
