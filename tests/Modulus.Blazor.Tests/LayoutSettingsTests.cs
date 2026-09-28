using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Shell;
using Modulus.Ui.Abstractions.Theming;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>Layout settings of the theme settings panel: cookies, script defaults, presets, header wrapper.</summary>
public class LayoutSettingsTests
{
    private static ShellView View(ModulusThemeOptions options, IReadOnlyList<MenuItemDefinition>? menu = null) =>
        new(options, menu ?? [], "", new ShellContent());

    [Fact]
    public void ResolveOptions_AppliesTheLayoutCookies()
    {
        var cookies = new Dictionary<string, string>
        {
            [ThemeCss.LayoutCookie] = "condensed",
            [ThemeCss.WidthCookie] = "framed",
            [ThemeCss.SideCookie] = "right",
            [ThemeCss.SidebarCookie] = "floating",
            [ThemeCss.FoldCookie] = "hover",
            [ThemeCss.HeaderCookie] = "sticky",
            [ThemeCss.TopbarCookie] = "dark",
            [ThemeCss.DirCookie] = "rtl",
        };
        var o = ThemeCss.ResolveOptions(new ModulusThemeOptions(), name => cookies.GetValueOrDefault(name));

        Assert.Equal(ShellLayout.Condensed, o.Layout);
        Assert.Equal(ContentWidth.Framed, o.ContentWidth);
        Assert.Equal(SidebarPosition.Right, o.SidebarPosition);
        Assert.Equal(SidebarStyle.Floating, o.SidebarStyle);
        Assert.Equal(SidebarFold.Hover, o.SidebarFold);
        Assert.Equal(HeaderMode.Sticky, o.Header);
        Assert.Equal(HeaderTheme.Dark, o.HeaderTheme);
        Assert.Equal(TextDirection.Rtl, o.Direction);
    }

    [Fact]
    public void ScriptAttributes_CarryTheConfiguredLayoutDefaults()
    {
        var attrs = ThemeCss.ScriptAttributes(new ModulusThemeOptions { SidebarFold = SidebarFold.Folded, Direction = TextDirection.Rtl });
        Assert.Contains("data-fold=\"folded\"", attrs);
        Assert.Contains("data-dir=\"rtl\"", attrs);
        Assert.Contains("data-header=\"fixed\"", attrs);
        Assert.EndsWith("\"", attrs);
    }

    [Fact]
    public void Customizer_OffersTheLayoutPresetsAndChecksTheMatchingOne()
    {
        var html = ShellRenderer.Customizer(View(new ModulusThemeOptions { Layout = ShellLayout.Vertical, SidebarFold = SidebarFold.Hover }));

        foreach (var preset in new[] { "horizontal", "condensed", "fluid", "boxed", "navbar-dark", "navbar-sticky", "navbar-floating", "vertical",
                     "fluid-vertical", "right-vertical", "vertical-transparent", "vertical-floating", "folded", "folded-hover", "rtl" })
            Assert.Contains($"name=\"preset\" value=\"{preset}\"", html);
        Assert.Contains("name=\"preset\" value=\"folded-hover\" data-preset=\"{&quot;layout&quot;:&quot;vertical&quot;", html);
        Assert.Single(System.Text.RegularExpressions.Regex.Matches(html, "name=\"preset\"[^>]* checked"));
        Assert.Matches("value=\"folded-hover\"[^>]* checked", html);
        foreach (var name in new[] { "layout", "width", "header", "topbar", "side", "sidebar", "fold", "dir" })
            Assert.Contains($"name=\"{name}\"", html);
    }

    [Fact]
    public void Customizer_ChecksNoPresetForACustomMix()
    {
        var html = ShellRenderer.Customizer(View(new ModulusThemeOptions { Layout = ShellLayout.Condensed, HeaderTheme = HeaderTheme.Dark }));
        Assert.DoesNotMatch("name=\"preset\"[^>]* checked", html);
    }

    [Fact]
    public void Header_WrapsTopbarAndNavbar_AndTheSidebarHasFoldedFallbacks()
    {
        var apps = new MenuItemDefinition("apps", "Apps", icon: "apps");
        apps.Children.Add(new MenuItemDefinition("mail", "Mail", "/mail"));
        var menu = new List<MenuItemDefinition> { new("home", "home", "/"), apps };
        var view = View(new ModulusThemeOptions(), menu);

        var header = ShellRenderer.Header(view);
        Assert.StartsWith("<div class=\"m-header\"><header class=\"m-topbar\">", header);
        Assert.Contains("data-m-fold-toggle", header);
        Assert.EndsWith("</nav></div>", header);

        var sidebar = ShellRenderer.Sidebar(view);
        Assert.Contains("<span class=\"m-snav-initial\" aria-hidden=\"true\">H</span>", sidebar);
        Assert.Contains("<li class=\"m-snav-flyout-title\" aria-hidden=\"true\">Apps</li>", sidebar);
    }
}
