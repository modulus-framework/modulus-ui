using Modulus.Ui.Abstractions.Barcode;
using Xunit;
using ZXing;
using ZXing.Common;
using ZXing.OneD;

namespace Modulus.Blazor.Tests;

/// <summary>The barcode encoder against ZXing.Net: the same modules where both encode the value, and a decode back to the text.</summary>
public class BarcodeTests
{
    // Rasterises the modules (3 px each, a 10-module quiet zone) and reads the symbol back with ZXing.
    private static string? Decode(IReadOnlyList<bool> modules, BarcodeFormat format)
    {
        const int scale = 3, quiet = BarcodeEncoder.QuietZone, height = 40;
        var width = (modules.Count + quiet * 2) * scale;
        var pixels = new byte[width * height];
        for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var m = x / scale - quiet;
                pixels[y * width + x] = m >= 0 && m < modules.Count && modules[m] ? (byte)0 : (byte)255;
            }

        var source = new RGBLuminanceSource(pixels, width, height, RGBLuminanceSource.BitmapFormat.Gray8);
        var hints = new Dictionary<DecodeHintType, object> { [DecodeHintType.TRY_HARDER] = true, [DecodeHintType.POSSIBLE_FORMATS] = new List<BarcodeFormat> { format } };
        return new MultiFormatReader().decode(new BinaryBitmap(new HybridBinarizer(source)), hints)?.Text;
    }

    [Theory]
    [InlineData("ABC-123")]
    [InlineData("Hello, World! ~{}")]
    [InlineData("12345")]
    [InlineData("0123456789")]
    [InlineData("a")]
    [InlineData(" ")]
    public void Code128_DecodesBackToTheValue(string value) =>
        Assert.Equal(value, Decode(BarcodeEncoder.Modules(BarcodeKind.Code128, value), BarcodeFormat.CODE_128));

    [Fact]
    public void Code128_Long_DecodesBack()
    {
        var value = string.Concat(Enumerable.Repeat("SKU-1043/B ", 8))[..BarcodeEncoder.MaxCode128Length];

        Assert.Equal(value, Decode(BarcodeEncoder.Modules(BarcodeKind.Code128, value), BarcodeFormat.CODE_128));
    }

    // Both encoders use set B for text and set C for an even run of digits; a value that mixes them can differ (ZXing switches
    // sets mid-value), and decodes the same either way, so it is left to the decode test.
    [Theory]
    [InlineData("ABC-XYZ")]
    [InlineData("0123456789")]
    [InlineData("99")]
    public void Code128_MatchesZXing_ModuleForModule_WhereBothPickTheSameSet(string value)
    {
        var ours = BarcodeEncoder.Modules(BarcodeKind.Code128, value);
        var theirs = new Code128Writer().encode(value);

        Assert.Equal(theirs, ours.ToArray());
    }

    [Theory]
    [InlineData("5901234123457")]
    [InlineData("590123412345")]
    [InlineData("4006381333931")]
    [InlineData("0000000000000")]
    public void Ean13_MatchesZXing_AndDecodesBack(string value)
    {
        var text = BarcodeEncoder.Text(BarcodeKind.Ean13, value);
        var ours = BarcodeEncoder.Modules(BarcodeKind.Ean13, value);

        Assert.Equal(text, Decode(ours, BarcodeFormat.EAN_13));
        Assert.Equal(new EAN13Writer().encode(text).ToArray(), ours.ToArray());
    }

    [Fact]
    public void Ean13_CheckDigit_IsComputedFromTheTwelve()
    {
        Assert.Equal(7, BarcodeEncoder.CheckDigit("590123412345"));
        Assert.Equal("5901234123457", BarcodeEncoder.Text(BarcodeKind.Ean13, "590123412345"));
    }

    [Theory]
    [InlineData(BarcodeKind.Ean13, "5901234123458", "The EAN-13 check digit is wrong.")]
    [InlineData(BarcodeKind.Ean13, "12345", "EAN-13 takes 12 digits, or 13 with the check digit.")]
    [InlineData(BarcodeKind.Ean13, "59012341234A", "EAN-13 takes 12 digits, or 13 with the check digit.")]
    [InlineData(BarcodeKind.Code128, "", "Give the barcode a value.")]
    [InlineData(BarcodeKind.Code128, "tab\there", "Code 128 takes printable ASCII characters only.")]
    public void AValueTheKindCannotDraw_HasAProblem(BarcodeKind kind, string value, string problem)
    {
        Assert.Equal(problem, BarcodeEncoder.Problem(kind, value));
        Assert.Throws<ArgumentException>(() => BarcodeEncoder.Modules(kind, value));
    }

    [Fact]
    public void Code128_RefusesMoreThanTheLimit()
    {
        Assert.Equal($"Use {BarcodeEncoder.MaxCode128Length} characters or fewer.",
            BarcodeEncoder.Problem(BarcodeKind.Code128, new string('A', BarcodeEncoder.MaxCode128Length + 1)));
    }

    [Fact]
    public void Svg_DrawsTheBarsWithTheValueUnderneath()
    {
        var svg = BarcodeSvg.Render(BarcodeKind.Ean13, "5901234123457", barHeight: 60, moduleWidth: 2, label: "Product EAN");

        Assert.StartsWith("<svg class=\"m-barcode\" role=\"img\" aria-label=\"Product EAN\"", svg);
        Assert.Contains(">5901234123457</text>", svg);
        Assert.True(svg.Split("<rect").Length > 10);
    }

    [Fact]
    public void Svg_ForAValueTheKindCannotDraw_SaysWhy()
    {
        var svg = BarcodeSvg.Render(BarcodeKind.Ean13, "123");

        Assert.Contains("m-barcode-error", svg);
        Assert.DoesNotContain("<rect", svg);
        Assert.Contains("EAN-13 takes 12 digits", svg);
    }

    [Fact]
    public void Svg_EncodesTheLabelAndTheValue()
    {
        var svg = BarcodeSvg.Render(BarcodeKind.Code128, "<b>x</b>", label: "a & b");

        Assert.Contains("aria-label=\"a &amp; b\"", svg);
        Assert.Contains("&lt;b&gt;x&lt;/b&gt;", svg);
    }
}
