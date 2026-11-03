using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Shell;
using Modulus.Ui.Abstractions.Theming;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The header search: the server-driven results (SearchResults) and the box that asks for them (ServerSearch).</summary>
public class ShellSearchTests
{
    private static MenuItemDefinition Page(string label, string href, string? icon = null) => new(href, label, href, icon);

    private static MenuItemDefinition Group(string label, params MenuItemDefinition[] children)
    {
        var group = new MenuItemDefinition(label, label, null, "folder");
        group.Children.AddRange(children);
        return group;
    }

    private static List<MenuItemDefinition> SampleMenu() =>
    [
        Group("Sales",
            Page("Orders", "apps/orders", "truck"),
            Group("Catalog", Page("Products", "apps/products"))),
        Page("Dashboard", "dashboards/crm", "line-chart"),
    ];

    private static int Count(string text, string needle)
    {
        var count = 0;
        for (var i = text.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = text.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) count++;
        return count;
    }

    [Fact]
    public void EmptyQuery_HasNoResults()
    {
        Assert.Equal("", ShellRenderer.SearchResults(SampleMenu(), "   "));
    }

    [Fact]
    public void Results_ArePagesOnly_WithTheirGroupTrail()
    {
        var html = ShellRenderer.SearchResults(SampleMenu(), "orders");

        Assert.Contains("href=\"/apps/orders\"", html);
        Assert.Contains("<span class=\"m-search-item-section\">Sales</span>", html);
        Assert.DoesNotContain("Products", html);
    }

    [Fact]
    public void NestedPages_ShowTheWholeTrail()
    {
        var html = ShellRenderer.SearchResults(SampleMenu(), "products");

        Assert.Contains("<span class=\"m-search-item-section\">Sales › Catalog</span>", html);
    }

    [Fact]
    public void Groups_AreNotResults()
    {
        // "Catalog" is a group; its page "Products" carries the trail, so only the page is listed.
        var html = ShellRenderer.SearchResults(SampleMenu(), "catalog");

        Assert.Contains("href=\"/apps/products\"", html);
        Assert.Equal(1, Count(html, "role=\"option\""));
    }

    [Fact]
    public void NoMatch_SaysSo()
    {
        var html = ShellRenderer.SearchResults(SampleMenu(), "zzzz");

        Assert.Contains("No pages match “zzzz”", html);
        Assert.DoesNotContain("role=\"option\"", html);
    }

    [Fact]
    public void FirstResult_IsActive_OnlyOne()
    {
        var html = ShellRenderer.SearchResults(SampleMenu(), "or");

        Assert.Equal(1, Count(html, "m-search-item active"));
        Assert.StartsWith("<a class=\"m-search-item active\"", html);
    }

    [Fact]
    public void SamePage_IsListedOnce()
    {
        var menu = new List<MenuItemDefinition> { Page("Orders", "apps/orders"), Group("Sales", Page("Orders", "apps/orders")) };

        Assert.Equal(1, Count(ShellRenderer.SearchResults(menu, "orders"), "role=\"option\""));
    }

    [Fact]
    public void Results_AreCappedAtTwelve()
    {
        var pages = Enumerable.Range(1, 15).Select(i => (MenuItemDefinition)Page($"Report {i}", $"reports/{i}")).ToList();

        Assert.Equal(12, Count(ShellRenderer.SearchResults(pages, "report"), "role=\"option\""));
    }

    [Fact]
    public void ServerSearch_BoxAsksTheServerThroughHtmx_AndAlpineRunsIt()
    {
        var view = new ShellView(new ModulusThemeOptions(), SampleMenu(), "", new ShellContent { SearchPlaceholder = "Search pages…" }, ServerSearch: true);
        var html = ShellRenderer.Topbar(view);

        Assert.Contains("data-m-search-server", html);
        Assert.Contains("hx-get=\"/_ui/search\"", html);
        Assert.Contains("x-data=\"modulusSearch\"", html);
        Assert.Contains("name=\"q\"", html);
        Assert.Contains("id=\"m-search-results\"", html);
    }

    [Fact]
    public void ClientSearch_BoxKeepsTheDomSearch()
    {
        var view = new ShellView(new ModulusThemeOptions(), SampleMenu(), "", new ShellContent { SearchPlaceholder = "Search pages…" });
        var html = ShellRenderer.Topbar(view);

        Assert.Contains("data-m-search role=\"search\"", html);
        Assert.DoesNotContain("hx-get", html);
        Assert.DoesNotContain("x-data", html);
    }

    [Fact]
    public void Icons_HaveOneClassAttribute()
    {
        var svg = ShellRenderer.Icon("file-text");

        Assert.Equal(1, Count(svg, " class=\""));
        Assert.Contains("class=\"m-ti m-ai\"", svg);
    }
}
