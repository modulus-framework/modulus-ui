using Bunit;
using Modulus.Blazor.Components.Extras;
using Modulus.Ui.Abstractions.Plugins;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The "Download PDF" button: the form it submits, the action it posts to, the refused addresses, and the Blazor component.</summary>
public class PdfDownloadButtonTests : AsyncBunitContext
{
    [Fact]
    public void TheButton_SubmitsTheFormToTheAction()
    {
        var html = PdfDownloadHtml.Button("document-form", "/plugins/document/pdf");

        Assert.Contains("type=\"submit\"", html);
        Assert.Contains("form=\"document-form\"", html);
        Assert.Contains("formaction=\"/plugins/document/pdf\"", html);
        Assert.Contains("data-m-reload", html);
        Assert.Contains("<span>Download PDF</span>", html);
    }

    [Fact]
    public void TheLabelAndClassAreEncoded()
    {
        var html = PdfDownloadHtml.Button("f", "/x", "Export <b>", "m-wide");

        Assert.Contains("<span>Export &lt;b&gt;</span>", html);
        Assert.Contains("class=\"ant-btn ant-btn-default m-wide\"", html);
    }

    [Theory]
    [InlineData("https://example.com/pdf")]
    [InlineData("//example.com/pdf")]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:application/pdf;base64,AA==")]
    [InlineData("pdf")]
    public void AnActionThatIsNotASameSitePathIsRefused(string action) =>
        Assert.Throws<ArgumentException>(() => PdfDownloadHtml.Button("f", action));

    [Fact]
    public void AFormIdIsNeeded()
    {
        Assert.Throws<ArgumentException>(() => PdfDownloadHtml.Button(" ", "/x.pdf"));
    }

    [Fact]
    public void TheComponent_RendersTheButtonForItsForm()
    {
        var cut = Render<AppPdfButton>(p => p.Add(c => c.FormId, "contract-form").Add(c => c.Action, "/documents/contract.pdf"));

        var button = cut.Find("button[type=submit]");
        Assert.Equal("contract-form", button.GetAttribute("form"));
        Assert.Equal("/documents/contract.pdf", button.GetAttribute("formaction"));
        Assert.Contains("Download PDF", button.TextContent);
    }
}
