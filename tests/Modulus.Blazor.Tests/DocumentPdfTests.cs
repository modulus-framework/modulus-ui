using System.Text;
using System.Text.RegularExpressions;
using Modulus.Documents;
using Modulus.Samples.Shared;
using QuestPDF.Infrastructure;
using Xunit;

namespace Modulus.Blazor.Tests;

public class DocumentPdfTests
{
    static DocumentPdfTests() => QuestPDF.Settings.License = LicenseType.Community;

    private static readonly DocBlock[] Sample =
    [
        DocBlock.Title("Offer letter"),
        DocBlock.Subtitle("Ref. HR-7"),
        DocBlock.Heading(1, "Terms"),
        DocBlock.Heading(5, "Small print"),
        DocBlock.Paragraph("Salary ", new DocRun("USD 4,000", Bold: true), " a month, ", new DocRun("paid monthly", Italic: true, Underline: true),
            " — ", new DocRun("red", Color: "#cc0000", FontSize: 14), " and ", new DocRun("gone", Strike: true)),
        DocBlock.Aligned(DocAlign.Center, "Centred"),
        DocBlock.Aligned(DocAlign.End, "Right"),
        DocBlock.Aligned(DocAlign.Justify, "Justified text that runs on to a second line so the justification has a line to spread across."),
        DocBlock.Bullet("One"),
        DocBlock.Bullet("Nested", 1),
        DocBlock.Numbered("First"),
        DocBlock.Numbered("Second"),
        DocBlock.Table([["Name", "Role"], ["Ada", "Engineer"]]),
        DocBlock.TableOf([
            [[DocBlock.Paragraph("Cell with a list")], [DocBlock.Bullet("in a cell")]],
            [[DocBlock.Paragraph("Short row")]],
        ]),
        DocBlock.Paragraph("Tab\there"),
    ];

    [Fact]
    public void TheOutputIsAPdfFile()
    {
        var pdf = DocumentPdf.Write(Sample);
        var text = Latin1(pdf);

        Assert.StartsWith("%PDF-", text);
        Assert.EndsWith("%%EOF", text.TrimEnd());
    }

    [Fact]
    public void AnEmptyDocumentIsOnePage()
    {
        Assert.Equal(1, Pages(DocumentPdf.Write(Array.Empty<DocBlock>())));
    }

    [Fact]
    public void EveryBlockKindRenders()
    {
        var pdf = DocumentPdf.Write(Sample);

        Assert.True(Pages(pdf) >= 1);
    }

    [Fact]
    public void ALongDocumentRunsOnToMorePages()
    {
        var blocks = Enumerable.Range(1, 300).Select(i => DocBlock.Paragraph("Paragraph " + i + " of a long letter.")).ToList();

        Assert.True(Pages(DocumentPdf.Write(blocks)) > 1);
    }

    [Fact]
    public void TheEditorsJsonMakesAPdf()
    {
        // The page's path: the editor posts JSON, the server writes a PDF from it.
        Assert.StartsWith("%PDF-", Latin1(DocumentPdf.Write(PluginsContent.Contract)));
    }

    [Fact]
    public void AnUnreadableEditorJsonMakesAnEmptyPdf()
    {
        Assert.Equal(1, Pages(DocumentPdf.Write("not json")));
    }

    [Fact]
    public void ANullDocumentIsRefused()
    {
        Assert.Throws<ArgumentNullException>(() => DocumentPdf.Write((IReadOnlyList<DocBlock>)null!));
    }

    [Fact]
    public void AMergedFileStartsEachDocumentOnANewPage()
    {
        var letters = Enumerable.Range(1, 3).Select(i => (IReadOnlyList<DocBlock>)[DocBlock.Title("Letter " + i)]).ToList();

        Assert.Equal(3, Pages(DocumentPdf.WriteMerged(letters)));
    }

    [Fact]
    public void HtmlBecomesAPdf()
    {
        var pdf = DocumentPdf.WriteHtml("<h1>Invoice</h1><table><tr><td>Desk</td><td>2</td></tr></table>");

        Assert.StartsWith("%PDF-", Latin1(pdf));
        Assert.Equal(1, Pages(pdf));
    }

    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    // The page objects: "/Type /Page" and not "/Pages" (the page tree).
    private static int Pages(byte[] pdf) => Regex.Matches(Latin1(pdf), @"/Type\s*/Page\b").Count;
}
