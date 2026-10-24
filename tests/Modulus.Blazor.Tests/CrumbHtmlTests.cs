using Bunit;
using Modulus.Blazor.Components.Navigation;
using Modulus.Ui.Abstractions.Navigation;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The breadcrumb trail: the current level, the links, the labels' encoding, the collapsing menu, and the Blazor component.</summary>
public class CrumbHtmlTests : AsyncBunitContext
{
    [Fact]
    public void NoLevels_RenderNothing()
    {
        Assert.Equal("", CrumbHtml.Trail([]));
    }

    [Fact]
    public void TheCurrentPage_IsTheLastLevel_AndIsNotALink()
    {
        var html = CrumbHtml.Trail([new Crumb("Forms", "/forms"), new Crumb("Form layouts")]);

        Assert.Contains("<a href=\"/forms\">", html);
        Assert.Contains("<li class=\"m-crumb m-crumb-current\" aria-current=\"page\"><span class=\"m-crumb-label\">Form layouts</span></li>", html);
        Assert.DoesNotContain("<a href=\"/form", html.Substring(html.IndexOf("Form layouts", StringComparison.Ordinal) - 40, 40));
    }

    [Fact]
    public void ALabelIsEncoded()
    {
        var html = CrumbHtml.Trail([new Crumb("<script>x</script>")]);

        Assert.DoesNotContain("<script>", html);
        Assert.Contains("&lt;script&gt;", html);
    }

    [Fact]
    public void Levels_AreTextOnly_NoIcon()
    {
        var html = CrumbHtml.Trail([new Crumb("Home", "/", "home"), new Crumb("Page")]);

        Assert.DoesNotContain("m-crumb-icon", html);
        Assert.Contains("<span class=\"m-crumb-label\">Home</span>", html);
    }

    [Fact]
    public void ALongTrail_FoldsTheMiddleIntoAMenu()
    {
        var levels = new[] { new Crumb("App", "/app"), new Crumb("A", "/a"), new Crumb("B", "/b"), new Crumb("C", "/c"), new Crumb("D", "/d"), new Crumb("Page") };

        var html = CrumbHtml.Trail(levels, maxVisible: 4);

        Assert.Contains("m-crumb-more", html);
        Assert.Contains("<details class=\"m-dropdown m-crumb-menu-wrap\">", html);
        // Every middle level is in the menu, so a phone can reach each one.
        foreach (var label in new[] { "A", "B", "C", "D" })
            Assert.Contains($"<li><a href=\"/{label.ToLowerInvariant()}\" class=\"m-crumb-menu-item\"><span>{label}</span></a></li>", html);
        Assert.DoesNotContain("m-crumb-phone-only", html);
    }

    [Fact]
    public void AShortTrail_HasNoMenuOnAWideScreen_ButAPhoneMenuForItsMiddleLevels()
    {
        var html = CrumbHtml.Trail([new Crumb("App", "/app"), new Crumb("Section", "/s"), new Crumb("Page")]);

        Assert.Contains("m-crumb-phone-only", html);
        Assert.Contains("m-crumb-mid", html);
    }

    [Fact]
    public void TheComponent_RendersTheSameMarkup()
    {
        var levels = new List<Crumb> { new("App", "/app", "home"), new("Page") };
        var cut = Render<AppCrumbs>(p => p.Add(c => c.Items, levels));

        Assert.Equal(CrumbHtml.Trail(levels), cut.Markup.Trim());
    }
}

/// <summary>The page heading's own trail: rendered above the pretitle when there are crumbs, and absent otherwise.</summary>
public class PageHeadingCrumbTests : AsyncBunitContext
{
    [Fact]
    public void WithCrumbs_TheTrailSitsAboveTheTitle()
    {
        var crumbs = new List<Crumb> { new("Forms", "/forms"), new("Form layouts") };
        var cut = Render<Modulus.Blazor.Shell.PageHeading>(p => p.Add(c => c.Title, "Form layouts").Add(c => c.Pretitle, "Forms").Add(c => c.Crumbs, crumbs));

        var trail = cut.Find(".m-page-crumbs nav.m-crumbs");
        Assert.NotNull(trail);
        Assert.Contains("aria-current=\"page\"", cut.Markup);
    }

    [Fact]
    public void WithoutCrumbs_NoTrailIsRendered()
    {
        var cut = Render<Modulus.Blazor.Shell.PageHeading>(p => p.Add(c => c.Title, "Dashboard"));

        Assert.Empty(cut.FindAll(".m-page-crumbs"));
    }

    [Fact]
    public void ALevelWithRelatedPages_OpensThemFromItsCaret()
    {
        var related = new[] { new Crumb("Forms", "/forms"), new Crumb("Tables", "/tables", Selected: true), new Crumb("Charts") };
        var html = CrumbHtml.Trail([new Crumb("App", "/app"), new Crumb("Tables", "/tables", null, related, RelatedTitle: "Interface"), new Crumb("Page")]);

        Assert.Contains("<details class=\"m-dropdown m-crumb-switch\">", html);
        Assert.Contains("aria-label=\"Related pages in Interface\"", html);
        Assert.Contains("<div class=\"m-crumb-panel-head\">Interface</div>", html);
        // The page on screen is marked; a related level without a link is text.
        Assert.Contains("class=\"m-crumb-menu-item m-crumb-menu-selected\" aria-current=\"page\"", html);
        Assert.Contains("<span class=\"m-crumb-menu-static\">", html);
    }

    [Fact]
    public void ALevelWithoutRelatedPages_HasNoCaret()
    {
        var html = CrumbHtml.Trail([new Crumb("App", "/app"), new Crumb("Page")]);

        Assert.DoesNotContain("m-crumb-switch", html);
    }
}
