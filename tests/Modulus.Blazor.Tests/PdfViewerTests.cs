using Bunit;
using Modulus.Blazor.Components.Extras;
using Modulus.Ui.Abstractions.Plugins;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The PDF viewer's markup: the source it loads, the page it opens on, the refused addresses, and the Blazor component.</summary>
public class PdfViewerTests : BunitContext
{
    [Fact]
    public void TheViewer_CarriesItsSourceAndPage_WithTheControlsPdfJsDrives()
    {
        var html = PdfHtml.Viewer("doc", "/samples/sample.pdf", "Sample", "480px", page: 2);

        Assert.Contains("id=\"doc\"", html);
        Assert.Contains("data-m-pdf", html);
        Assert.Contains("data-src=\"/samples/sample.pdf\"", html);
        Assert.Contains("data-page=\"2\"", html);
        Assert.Contains("aria-label=\"Sample\"", html);
        Assert.Contains("style=\"height: 480px\"", html);
        foreach (var hook in new[] { "data-m-pdf-prev", "data-m-pdf-next", "data-m-pdf-page", "data-m-pdf-count", "data-m-pdf-zoom-in", "data-m-pdf-zoom-out", "data-m-pdf-canvas", "data-m-pdf-loading", "data-m-pdf-download" })
            Assert.Contains(hook, html);
    }

    [Fact]
    public void TheToolbar_StartsWithItsPageAndTheRightButtonsOff_SoItDoesNotChangeOnLoad()
    {
        var html = PdfHtml.Viewer("doc", "/a.pdf", page: 2);

        Assert.Contains("data-m-pdf-page min=\"1\" value=\"2\"", html);
        Assert.Contains("data-m-pdf-prev aria-label=\"Previous page\" title=\"Previous page\" disabled", html);
        Assert.Contains("data-m-pdf-next aria-label=\"Next page\" title=\"Next page\" disabled", html);
    }

    [Fact]
    public void ThePage_IsAtLeastOne()
    {
        Assert.Contains("data-page=\"1\"", PdfHtml.Viewer("doc", "/a.pdf", page: 0));
    }

    [Theory]
    [InlineData("https://example.com/a.pdf")]
    [InlineData("http://example.com/a.pdf")]
    [InlineData("/files/a.pdf")]
    public void AnHttpOrSameSiteAddress_IsAccepted(string url) =>
        Assert.Contains($"data-src=\"{url}\"", PdfHtml.Viewer("doc", url));

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:application/pdf;base64,AAAA")]
    [InlineData("//evil.example/a.pdf")]
    [InlineData("a.pdf")]
    public void AnyOtherAddress_IsRefused(string url) =>
        Assert.Throws<ArgumentException>(() => PdfHtml.Viewer("doc", url));

    [Fact]
    public void TheLabelIsEncoded()
    {
        var html = PdfHtml.Viewer("doc", "/a.pdf", "<b>Contract</b>");

        Assert.Contains("aria-label=\"&lt;b&gt;Contract&lt;/b&gt;\"", html);
        Assert.DoesNotContain("<b>Contract</b>", html);
    }

    [Fact]
    public void TheBlazorComponent_RendersTheViewer()
    {
        var cut = Render<AppPdf>(p => p.Add(x => x.Id, "doc").Add(x => x.Url, "/samples/sample.pdf").Add(x => x.Page, 3));

        Assert.NotNull(cut.Find("[data-m-pdf]"));
        Assert.Contains("data-page=\"3\"", cut.Markup);
    }
}
