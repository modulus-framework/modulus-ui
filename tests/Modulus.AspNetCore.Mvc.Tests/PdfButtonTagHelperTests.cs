using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>app-pdf-button: the submit button that posts its form to the PDF action.</summary>
public class PdfButtonTagHelperTests
{
    [Fact]
    public async Task RendersASubmitButtonForTheFormAndAction()
    {
        var helper = new PdfButtonTagHelper { Form = "document-form", Action = "/plugins/document/pdf" };
        var output = new TagHelperOutput("app-pdf-button", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext("app-pdf-button", [], new Dictionary<object, object>(), "p1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        var html = writer.ToString();

        Assert.StartsWith("<button type=\"submit\" form=\"document-form\" formaction=\"/plugins/document/pdf\" data-m-reload", html);
        Assert.Contains("Download PDF", html);
        Assert.DoesNotContain("<app-pdf-button", html);
    }

    [Fact]
    public async Task RefusesAnActionOutsideTheSite()
    {
        var helper = new PdfButtonTagHelper { Form = "f", Action = "https://example.com/pdf" };
        var output = new TagHelperOutput("app-pdf-button", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));

        await Assert.ThrowsAsync<ArgumentException>(() => helper.ProcessAsync(new TagHelperContext("app-pdf-button", [], new Dictionary<object, object>(), "p2"), output));
    }
}
