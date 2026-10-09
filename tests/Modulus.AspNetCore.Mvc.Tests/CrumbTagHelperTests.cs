using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>app-crumbs and its app-crumb levels: the trail built from the children, and the text of a level.</summary>
public class CrumbTagHelperTests
{
    [Fact]
    public async Task ChildLevels_BuildTheTrailInOrder()
    {
        var shared = new Dictionary<object, object>();
        var first = new CrumbTagHelper { Href = "/forms", Icon = "home" };
        var last = new CrumbTagHelper();

        var html = await RenderTrail(shared, async () =>
        {
            await RunChild(first, "Forms", shared);
            await RunChild(last, "Form layouts", shared);
        });

        Assert.StartsWith("<nav class=\"m-crumbs\"", html);
        Assert.Contains("<a href=\"/forms\">", html);
        Assert.Contains("Form layouts</span></li>", html);
        Assert.Contains("aria-current=\"page\"", html);
        Assert.DoesNotContain("<app-crumbs", html);
    }

    [Fact]
    public async Task ALevelsText_IsEncodedOnce()
    {
        var shared = new Dictionary<object, object>();

        var html = await RenderTrail(shared, () => RunChild(new CrumbTagHelper(), "Tom &amp; Jerry", shared));

        Assert.Contains("Tom &amp; Jerry", html);
        Assert.DoesNotContain("&amp;amp;", html);
    }

    // The parent runs its children inside its child-content call, as Razor does, and the trail is what it writes.
    private static async Task<string> RenderTrail(IDictionary<object, object> shared, Func<Task> children)
    {
        var parent = new CrumbsTagHelper();
        var output = new TagHelperOutput("app-crumbs", [], async (_, _) =>
        {
            await children();
            return new DefaultTagHelperContent();
        });
        await parent.ProcessAsync(new TagHelperContext("app-crumbs", [], shared, "p"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private static async Task RunChild(TagHelper helper, string text, IDictionary<object, object> shared)
    {
        var output = new TagHelperOutput("app-crumb", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(text)));
        await helper.ProcessAsync(new TagHelperContext("app-crumb", [], shared, "c"), output);
    }
}

public class PageHeaderCrumbTests
{
    [Fact]
    public async Task ACrumbInsideThePageHeader_IsTheTrailAboveTheTitle()
    {
        var shared = new Dictionary<object, object>();
        var header = new PageHeaderTagHelper { Title = "Form layouts", Pretitle = "Forms" };
        var output = new TagHelperOutput("app-page-header", [], async (_, _) =>
        {
            await new CrumbTagHelper { Href = "/forms" }.ProcessAsync(new TagHelperContext("app-crumb", [], shared, "c1"), Child("Forms"));
            await new CrumbTagHelper().ProcessAsync(new TagHelperContext("app-crumb", [], shared, "c2"), Child("Form layouts"));
            return new DefaultTagHelperContent();
        });
        await header.ProcessAsync(new TagHelperContext("app-page-header", [], shared, "p"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        var html = writer.ToString();

        Assert.StartsWith("<div class=\"m-page-header\"><div class=\"m-page-crumbs\"><nav class=\"m-crumbs\"", html);
        Assert.True(html.IndexOf("m-page-crumbs", StringComparison.Ordinal) < html.IndexOf("m-page-title", StringComparison.Ordinal));
        Assert.Contains("<a href=\"/forms\">", html);
    }

    [Fact]
    public async Task AHeaderWithoutCrumbs_HasNoTrail()
    {
        var output = new TagHelperOutput("app-page-header", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await new PageHeaderTagHelper { Title = "Dashboard" }.ProcessAsync(new TagHelperContext("app-page-header", [], new Dictionary<object, object>(), "p"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);

        Assert.DoesNotContain("m-page-crumbs", writer.ToString());
    }

    private static TagHelperOutput Child(string text) =>
        new("app-crumb", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(text)));
}
