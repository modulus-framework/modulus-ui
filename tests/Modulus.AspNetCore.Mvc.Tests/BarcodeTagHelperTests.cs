using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Barcode;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>app-barcode: the SVG for the kind and value, with the value printed under the bars, and a message for a value the kind cannot draw.</summary>
public class BarcodeTagHelperTests
{
    private static async Task<string> RenderAsync(BarcodeTagHelper helper)
    {
        var output = new TagHelperOutput("app-barcode", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext("app-barcode", [], new Dictionary<object, object>(), "b1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task DrawsAnEan13_WithItsCheckDigitUnderTheBars()
    {
        var html = await RenderAsync(new BarcodeTagHelper { Value = "590123412345", Kind = BarcodeKind.Ean13, Height = 70, AriaLabel = "Product" });

        Assert.StartsWith("<svg class=\"m-barcode\" role=\"img\" aria-label=\"Product\"", html);
        Assert.Contains(">5901234123457</text>", html);
        Assert.Contains("height=\"88\"", html);
        Assert.DoesNotContain("<app-barcode", html);
    }

    [Fact]
    public async Task ACode128_IsTheDefaultKind_AndLeavesOutTheTextWhenAsked()
    {
        var html = await RenderAsync(new BarcodeTagHelper { Value = "SHP-1", ShowText = false });

        Assert.Contains("aria-label=\"Code 128 SHP-1\"", html);
        Assert.DoesNotContain("<text", html);
    }

    [Fact]
    public async Task AValueTheKindCannotDraw_ShowsTheProblem()
    {
        var html = await RenderAsync(new BarcodeTagHelper { Value = "5901234123458", Kind = BarcodeKind.Ean13 });

        Assert.Contains("The EAN-13 check digit is wrong.", html);
        Assert.Contains("m-barcode-error", html);
    }
}
