using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Files;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>app-file-manager: the breadcrumb and folder links, the search, the create, rename and delete posts, and the refresh.</summary>
public class FileManagerTagHelperTests
{
    private static readonly DateTime Now = new(2026, 10, 9, 12, 0, 0);

    private static async Task<string> RenderAsync(FileManagerView view)
    {
        var helper = new FileManagerTagHelper { View = view, Url = "/apps/files" };
        var output = new TagHelperOutput("app-file-manager", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext("app-file-manager", [], new Dictionary<object, object>(), "f1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task ListsAFolder_WithItsBreadcrumbLinksAndActions()
    {
        var entries = new[]
        {
            new FileEntry(1, null, "Projects", true),
            new FileEntry(2, 1, "Plan <final>.pdf", false, 2048, new DateTime(2026, 10, 9, 8, 5, 0)),
        };
        var view = FileManagerModel.Build(entries, 1, null, Now);

        var html = await RenderAsync(view);

        Assert.StartsWith("<div id=\"file-manager\" class=\"m-files\" data-m-files", html);
        Assert.Contains("href=\"/apps/files?folder=1\" aria-current=\"page\"", html);
        Assert.Contains("hx-get=\"/apps/files?folder=1\" hx-trigger=\"files:refresh from:body\"", html);
        Assert.Contains("Plan &lt;final&gt;.pdf", html);
        Assert.Contains("2 KB", html);
        Assert.Contains("hx-post=\"/apps/files/rename\"", html);
        Assert.Contains("hx-post=\"/apps/files/delete/2\"", html);
        Assert.Contains("hx-confirm=\"Delete &quot;Plan &lt;final&gt;.pdf&quot;? This can", html);
        Assert.Contains("<input type=\"hidden\" name=\"ParentId\" value=\"1\" />", html);
    }

    [Fact]
    public async Task AFolderLink_IsBoostedGetWithItsFolder_AndASearchKeepsItsText()
    {
        var view = FileManagerModel.Build([new FileEntry(1, null, "Docs", true), new FileEntry(2, null, "Zebra", true)], null, "b&c", Now);

        var html = await RenderAsync(view);

        Assert.Contains("class=\"m-files-search\" method=\"get\" action=\"/apps/files\"", html);
        Assert.Contains("name=\"q\" value=\"b&amp;c\"", html);
        Assert.Contains("class=\"m-files-empty\" role=\"row\">Nothing here matches &quot;b&amp;c&quot;.", html);
        Assert.DoesNotContain("hx-post=\"/apps/files/delete", html);
    }

    [Fact]
    public async Task AnEmptyFolder_SaysSo()
    {
        var html = await RenderAsync(FileManagerModel.Build([], null, null, Now));

        Assert.Contains("This folder is empty.", html);
        Assert.DoesNotContain("<app-file-manager", html);
    }
}
