using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>app-pdf: the viewer's markup with the source and the page it opens on.</summary>
public class PdfTagHelperTests
{
    [Fact]
    public async Task RendersTheViewer_ForTheUrlAndPage()
    {
        var helper = new PdfTagHelper { Id = "contract", Url = "/files/contract.pdf", Label = "Contract", Page = 2 };
        var output = new TagHelperOutput("app-pdf", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext("app-pdf", [], new Dictionary<object, object>(), "p1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        var html = writer.ToString();

        Assert.StartsWith("<div class=\"m-pdf\" id=\"contract\" data-m-pdf", html);
        Assert.Contains("data-src=\"/files/contract.pdf\"", html);
        Assert.Contains("data-page=\"2\"", html);
        Assert.DoesNotContain("<app-pdf", html);
    }
}
