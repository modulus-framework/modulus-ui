using Modulus.Documents;
using Xunit;

namespace Modulus.Blazor.Tests;

public class HtmlBlocksTests
{
    [Fact]
    public void HeadingsAndParagraphsKeepTheirRuns()
    {
        var blocks = HtmlBlocks.FromHtml("<h1>Title</h1><p>Hello <strong>bold</strong> and <em>italic</em></p>");

        Assert.Equal(2, blocks.Count);
        Assert.Equal(DocBlockKind.Heading, blocks[0].Kind);
        Assert.Equal(1, blocks[0].Level);
        Assert.Equal("Title", blocks[0].Text);

        Assert.Equal(DocBlockKind.Paragraph, blocks[1].Kind);
        Assert.Equal("Hello bold and italic", blocks[1].Text);
        Assert.Contains(blocks[1].Runs, r => r.Text == "bold" && r.Bold);
        Assert.Contains(blocks[1].Runs, r => r.Text == "italic" && r.Italic);
        Assert.DoesNotContain(blocks[1].Runs, r => r.Text == "Hello " && r.Bold);
    }

    [Fact]
    public void NestedListsGoOneLevelDown()
    {
        var blocks = HtmlBlocks.FromHtml("<ol><li>One<ul><li>Note</li></ul></li><li>Two</li></ol>");

        Assert.Equal([DocBlockKind.Numbered, DocBlockKind.Bullet, DocBlockKind.Numbered], blocks.Select(b => b.Kind));
        Assert.Equal([0, 1, 0], blocks.Select(b => b.Level));
        Assert.Equal(["One", "Note", "Two"], blocks.Select(b => b.Text));
    }

    [Fact]
    public void ATableKeepsItsCellsAndBoldsTheHeader()
    {
        var blocks = HtmlBlocks.FromHtml(
            "<table><thead><tr><th>Item</th><th>Qty</th></tr></thead><tbody><tr><td>Desk</td><td>2</td></tr></tbody></table>");

        var table = Assert.Single(blocks);
        Assert.Equal(DocBlockKind.Table, table.Kind);
        Assert.Equal(2, table.Rows!.Count);
        Assert.Equal("Item", table.Rows[0][0][0].Text);
        Assert.True(table.Rows[0][0][0].Runs[0].Bold);
        Assert.False(table.Rows[1][0][0].Runs[0].Bold);
        Assert.Equal("2", table.Rows[1][1][0].Text);
    }

    [Fact]
    public void ScriptsAreDroppedAndEntitiesDecoded()
    {
        var blocks = HtmlBlocks.FromHtml("<p>Tom &amp; Jerry &lt;3</p><script>alert(1)</script><p>There</p>");

        Assert.Equal(["Tom & Jerry <3", "There"], blocks.Select(b => b.Text));
    }

    [Fact]
    public void ImagesAreDroppedAndWhitespaceCollapsed()
    {
        var blocks = HtmlBlocks.FromHtml("<p>  lots   of\n  <img src=\"x.png\" alt=\"pic\"> space </p>");

        var paragraph = Assert.Single(blocks);
        Assert.Equal("lots of space", paragraph.Text);
    }

    [Fact]
    public void AListItemsTextIsNotDuplicatedAsAParagraph()
    {
        var blocks = HtmlBlocks.FromHtml("<ul><li><p>Inside a paragraph</p></li></ul>");

        var item = Assert.Single(blocks);
        Assert.Equal(DocBlockKind.Bullet, item.Kind);
        Assert.Equal("Inside a paragraph", item.Text);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NoHtmlMeansNoBlocks(string? html) => Assert.Empty(HtmlBlocks.FromHtml(html));

    [Fact]
    public void AStrayCloseTagIsIgnored()
    {
        var blocks = HtmlBlocks.FromHtml("</div><p>Kept</p></span>");

        Assert.Equal(["Kept"], blocks.Select(b => b.Text));
    }
}
