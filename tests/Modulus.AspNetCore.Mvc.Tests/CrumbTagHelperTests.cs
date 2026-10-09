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
