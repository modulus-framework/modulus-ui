using Modulus.Ui.Abstractions.Qr;
using Modulus.Ui.Abstractions.Theming;
using Xunit;
using ZXing;
using ZXing.Common;

namespace Modulus.Blazor.Tests;

public class QrCodeTests
{
    // Rasterises the symbol (4 px per module, 4-module quiet zone) and reads it back with ZXing.
    private static string? Decode(QrCode code)
    {
        const int scale = 4, quiet = 4;
        var n = (code.Size + quiet * 2) * scale;
        var pixels = new byte[n * n];
        for (var py = 0; py < n; py++)
            for (var px = 0; px < n; px++)
                pixels[py * n + px] = code[px / scale - quiet, py / scale - quiet] ? (byte)0 : (byte)255;

        var source = new RGBLuminanceSource(pixels, n, n, RGBLuminanceSource.BitmapFormat.Gray8);
        var hints = new Dictionary<DecodeHintType, object> { [DecodeHintType.TRY_HARDER] = true };
        return new ZXing.QrCode.QRCodeReader().decode(new BinaryBitmap(new HybridBinarizer(source)), hints)?.Text;
    }

    public static TheoryData<string, QrErrorLevel> Payloads()
    {
        var data = new TheoryData<string, QrErrorLevel>();
        string[] texts =
        [
            "https://modulus.dev",
            "Grüße aus München — 你好 ✓",
            string.Concat(Enumerable.Range(0, 12).Select(i => $"Line {i}: the quick brown fox jumps over the lazy dog. ")),
            new string('x', 1200),
        ];
        foreach (var text in texts)
            foreach (var level in Enum.GetValues<QrErrorLevel>())
                data.Add(text, level);
        return data;
    }

    [Theory, MemberData(nameof(Payloads))]
    public void Encodes_AndDecodesBack(string text, QrErrorLevel level)
    {
        var code = QrCode.Encode(text, level);
        Assert.Equal(code.Version * 4 + 17, code.Size);
        Assert.Equal(level, code.ErrorLevel);
        Assert.Equal(text, Decode(code));
    }

    [Theory]
    [InlineData(17, QrErrorLevel.L, 1)] // version 1-L holds 17 bytes
    [InlineData(18, QrErrorLevel.L, 2)]
    [InlineData(14, QrErrorLevel.M, 1)]
    [InlineData(7, QrErrorLevel.H, 1)]
    [InlineData(2953, QrErrorLevel.L, 40)] // the largest byte-mode payload
    public void PicksTheSmallestVersion(int bytes, QrErrorLevel level, int version) =>
        Assert.Equal(version, QrCode.Encode(new byte[bytes], level).Version);

    [Fact]
    public void RejectsDataThatDoesNotFit() =>
        Assert.Throws<ArgumentException>(() => QrCode.Encode(new byte[2954], QrErrorLevel.L));

    [Fact]
    public void Svg_DrawsModules_IconAndEscapesAttributes()
    {
        var code = QrCode.Encode("hi");
        var svg = QrSvg.Render(code, color: "#1677ff", background: "#fff", size: 120, margin: 1,
            icon: "/logo.svg?a=1&b=2", label: "Scan \"me\"");
        Assert.StartsWith("<svg", svg);
        Assert.Contains("viewBox=\"0 0 23 23\"", svg);
        Assert.Contains("width=\"120\"", svg);
        Assert.Contains("fill=\"#1677ff\"", svg);
        Assert.Contains("href=\"/logo.svg?a=1&amp;b=2\"", svg);
        Assert.Contains("aria-label=\"Scan &quot;me&quot;\"", svg);
        Assert.DoesNotContain("<rect width", QrSvg.Render(code)); // transparent background is left out
    }
}

public class HsvColorTests
{
    [Theory]
    [InlineData("#1677ff", "#1677ff")]
    [InlineData("#F00", "#ff0000")]
    [InlineData("0f0", "#00ff00")]
    [InlineData("#1677ff80", "#1677ff80")]
    [InlineData("rgb(22, 119, 255)", "#1677ff")]
    [InlineData("rgba(0,0,0,0.5)", "#00000080")]
    [InlineData("#ffffff", "#ffffff")]
    [InlineData("#000", "#000000")]
    public void Parses_AndRoundTripsToHex(string input, string hex)
    {
        Assert.True(HsvColor.TryParse(input, out var color));
        Assert.Equal(hex, color.ToHex());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("#12")]
    [InlineData("#gggggg")]
    [InlineData("rgb(300,0,0)")]
    [InlineData("blue")]
    public void RejectsInvalid(string? input) => Assert.False(HsvColor.TryParse(input, out _));

    [Fact]
    public void ConvertsHsv()
    {
        Assert.True(HsvColor.TryParse("#52c41a", out var green));
        Assert.Equal(100, Math.Round(green.H));
        Assert.Equal("#00ff00", new HsvColor(120, 1, 1).ToHex());
        Assert.Equal("#ff0000", new HsvColor(0, 0.3, 0.4).HueHex());
        Assert.Equal("rgba(22, 119, 255, 0.5)", (HsvColor.Parse("#1677ff") with { A = 0.5 }).ToRgbString());
        Assert.Equal("rgb(22, 119, 255)", HsvColor.Parse("nope", "#1677ff").Format("rgb"));
    }
}
