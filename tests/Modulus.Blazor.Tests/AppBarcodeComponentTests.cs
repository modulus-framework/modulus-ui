using Bunit;
using Modulus.Blazor.Components.Extras;
using Modulus.Ui.Abstractions.Barcode;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The Blazor AppBarcode draws the same SVG as the MVC tag helper: the symbol, the value under the bars, the default white background.</summary>
public class AppBarcodeComponentTests : AsyncBunitContext
{
    [Fact]
    public void DrawsTheSymbol_WithTheValueAndAWhiteBackground()
    {
        var cut = Render<AppBarcode>(p => p.Add(x => x.Value, "ABC-123").Add(x => x.Kind, BarcodeKind.Code128).Add(x => x.AriaLabel, "Shipment"));

        var svg = cut.Find("svg.m-barcode");
        Assert.Equal("img", svg.GetAttribute("role"));
        Assert.Equal("Shipment", svg.GetAttribute("aria-label"));
        Assert.Contains("ABC-123", cut.Find("text").TextContent);
        Assert.Contains("fill=\"#fff\"", cut.Markup);
    }

    [Fact]
    public void AValueTheKindCannotDraw_ShowsAMessage()
    {
        var cut = Render<AppBarcode>(p => p.Add(x => x.Value, "12ab").Add(x => x.Kind, BarcodeKind.Ean13));

        Assert.Contains("m-barcode-error", cut.Markup);
        Assert.Contains("EAN-13 takes 12 digits", cut.Markup);
    }
}
