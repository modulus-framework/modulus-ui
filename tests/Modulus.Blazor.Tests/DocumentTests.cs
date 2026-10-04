using System.Text.Json;
using Modulus.Documents;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Plugins;
using Xunit;

namespace Modulus.Blazor.Tests;

public class DocumentTests
{
    private static readonly DocBlock[] Sample =
    [
        DocBlock.Title("Offer letter"),
        DocBlock.Subtitle("Ref. HR-7"),
        DocBlock.Heading(2, "Terms"),
        DocBlock.Paragraph("Salary ", new DocRun("USD 4,000", Bold: true), " a month, ", new DocRun("paid monthly", Italic: true, Underline: true),
            " — ", new DocRun("red", Color: "#cc0000", FontSize: 14), " and ", new DocRun("gone", Strike: true)),
        DocBlock.Aligned(DocAlign.Center, "Centred"),
        DocBlock.Bullet("One"),
        DocBlock.Bullet("Nested", 1),
        DocBlock.Numbered("First"),
        DocBlock.Numbered("Second"),
        DocBlock.Table([["Name", "Role"], ["Ada", "Engineer"]]),
        DocBlock.Paragraph("Tab\there"),
    ];

    [Fact]
    public void UniverJsonRoundTripsEveryBlockKind()
    {
        var back = UniverDocument.FromJson(UniverDocument.ToJson(Sample, "Offer"));

        AssertSample(back);
    }

    [Fact]
    public void DocxRoundTripsEveryBlockKind()
    {
        var back = DocumentDocx.Read(new MemoryStream(DocumentDocx.Write(Sample)));

        AssertSample(back);
    }

    [Fact]
    public void EditorJsonToDocxAndBack()
    {
        // The page's path: the editor posts JSON, the server writes a docx, an import reads it into JSON again.
        var docx = DocumentDocx.Write(PluginsContent.Contract);
        var json = DocumentDocx.ReadJson(new MemoryStream(docx), "Contract");

        Assert.Equal(UniverDocument.ToText(PluginsContent.Contract), UniverDocument.ToText(json));
    }

    [Fact]
    public void TheStreamIsWellFormed()
    {
        using var doc = JsonDocument.Parse(UniverDocument.ToJson(Sample));
        var body = doc.RootElement.GetProperty("body");
        var stream = body.GetProperty("dataStream").GetString()!;

        Assert.EndsWith("\r\n", stream); // the last paragraph and the section break
        foreach (var p in body.GetProperty("paragraphs").EnumerateArray())
            Assert.Equal('\r', stream[p.GetProperty("startIndex").GetInt32()]);
        foreach (var s in body.GetProperty("sectionBreaks").EnumerateArray())
            Assert.Equal('\n', stream[s.GetProperty("startIndex").GetInt32()]);
        var table = Assert.Single(body.GetProperty("tables").EnumerateArray());
        Assert.Equal('\u001A', stream[table.GetProperty("startIndex").GetInt32()]);
        Assert.Equal('\u000F', stream[table.GetProperty("endIndex").GetInt32() - 1]);
        Assert.True(doc.RootElement.GetProperty("tableSource").TryGetProperty(table.GetProperty("tableId").GetString()!, out _));
    }

    [Fact]
    public void ADocumentEndingInATableGetsAClosingParagraph()
    {
        var json = UniverDocument.ToJson([DocBlock.Table([["a"]])]);
        var stream = JsonDocument.Parse(json).RootElement.GetProperty("body").GetProperty("dataStream").GetString()!;

        Assert.EndsWith("\u000F\r\n", stream);
    }

    [Fact]
    public void LineBreaksAndMarkersInTextBecomeSpaces()
    {
        var back = UniverDocument.FromJson(UniverDocument.ToJson([DocBlock.Paragraph("a\r\nb\u001Ac")]));

        Assert.Equal("a  b c", Assert.Single(back).Text);
    }

    [Fact]
    public void AnEmptyDocumentHasOneEmptyParagraph()
    {
        var back = UniverDocument.FromJson(UniverDocument.ToJson([]));

        Assert.Equal("", Assert.Single(back).Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("""{"id":"x"}""")]
    public void JunkReadsAsNoBlocks(string? json) => Assert.Empty(UniverDocument.FromJson(json));

    [Fact]
    public void ANonDocxReportsTheImportMessage()
    {
        var (document, error) = PluginsContent.ImportDocument(new MemoryStream([1, 2, 3, 4]));

        Assert.Null(document);
        Assert.Equal("That file is not a readable Word document.", error);
    }

    [Theory]
    [InlineData("#ABC", "#aabbcc")]
    [InlineData("#066FD1", "#066fd1")]
    [InlineData("rgb(6, 111, 209)", "#066fd1")]
    [InlineData("blue", null)]
    [InlineData("#12345", null)]
    public void ColoursNormalise(string input, string? expected) => Assert.Equal(expected, UniverDocument.Hex(input));

    [Fact]
    public void PostedDocumentNeedsABody()
    {
        Assert.NotNull(PluginsContent.PostedDocument(PluginsContent.Memo));
        Assert.Null(PluginsContent.PostedDocument("""{"id":"x"}"""));
        Assert.Null(PluginsContent.PostedDocument("{"));
        Assert.Null(PluginsContent.PostedDocument(null));
    }

    [Fact]
    public void MarkupCarriesTheDocumentAndTheField()
    {
        var html = DocumentHtml.Render("doc", """{"body":{"dataStream":"a\r\n"}}""", name: "Doc", height: "100%");

        Assert.Contains("""class="m-doc" id="doc" data-m-doc data-doc="{&quot;body&quot;""", html);
        Assert.Contains("""style="height: 100%""", html);
        Assert.Contains("""<input type="hidden" name="Doc" id="doc-value" """, html);
        Assert.Contains("data-m-doc-value", html);
        Assert.DoesNotContain("data-readonly", html);
    }

    [Fact]
    public void ReadOnlyMarkupHasNoField()
    {
        var html = DocumentHtml.Render("doc", null, editable: false, label: "Contract");

        Assert.Contains("data-readonly", html);
        Assert.Contains("""aria-label="Contract""", html);
        Assert.DoesNotContain("<input", html);
    }

    private static void AssertSample(IReadOnlyList<DocBlock> back)
    {
        Assert.Equal(Sample.Length, back.Count);
        Assert.Equal(DocBlockKind.Title, back[0].Kind);
        Assert.Equal("Offer letter", back[0].Text);
        Assert.Equal(DocBlockKind.Subtitle, back[1].Kind);
        Assert.Equal((DocBlockKind.Heading, 2), (back[2].Kind, back[2].Level));

        var runs = back[3].Runs;
        Assert.Equal(Sample[3].Text, back[3].Text);
        Assert.Contains(runs, r => r is { Text: "USD 4,000", Bold: true, Italic: false });
        Assert.Contains(runs, r => r is { Text: "paid monthly", Italic: true, Underline: true });
        Assert.Contains(runs, r => r is { Text: "red", Color: "#cc0000", FontSize: 14 });
        Assert.Contains(runs, r => r is { Text: "gone", Strike: true });
        Assert.Contains(runs, r => r is { Text: "Salary ", Bold: false });

        Assert.Equal(DocAlign.Center, back[4].Align);
        Assert.Equal((DocBlockKind.Bullet, 0), (back[5].Kind, back[5].Level));
        Assert.Equal((DocBlockKind.Bullet, 1), (back[6].Kind, back[6].Level));
        Assert.Equal(DocBlockKind.Numbered, back[7].Kind);
        Assert.Equal(DocBlockKind.Numbered, back[8].Kind);

        var table = back[9];
        Assert.Equal(DocBlockKind.Table, table.Kind);
        Assert.Equal(2, table.Rows!.Count);
        Assert.Equal("Engineer", Assert.Single(table.Rows[1][1]).Text);
        Assert.Equal("Tab\there", back[10].Text);
    }
}
