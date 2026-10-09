using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Modulus.Documents;

/// <summary>
/// PDF output of a document, through <see cref="DocBlock"/>s and QuestPDF (Community licence, or a licence the host sets; see
/// <c>DEPENDENCIES.md</c>). The page is A4 with the margins of the Word output, and each page is numbered in the footer. Titles,
/// headings (five sizes, bold), paragraphs with bold, italic, underline, strike, size and colour, alignment, bullet and numbered
/// lists (nested by level, and a numbered list counts on from the item before it), and tables with thin borders. A table's cells
/// hold the same blocks, so a cell can carry a list. The layout is QuestPDF's, so a page break falls where the content fills a
/// page, not at a block; a row is not split across pages.
/// </summary>
/// <remarks>
/// QuestPDF needs the licence to be set before the first document is made: the host sets <c>QuestPDF.Settings.License</c> (the
/// Community licence is free for individuals and businesses under USD 1,000,000 in annual gross revenue, and not for public-sector
/// or publicly traded companies). Modulus does not choose the licence for the host.
/// </remarks>
public static class DocumentPdf
{
    /// <summary>The MIME type of a PDF file, for download endpoints.</summary>
    public const string ContentType = "application/pdf";

    // The Word output's page: A4 with 1-inch side margins and 0.75-inch top and bottom ones (in points).
    private const float MarginX = 72, MarginY = 54;
    private const float BulletWidth = 16, ListIndent = 18;
    private const string Rule = "#bbbbbb", Muted = "#555555";

    // Heading sizes by level (1 is the largest), in points.
    private static readonly float[] HeadingSizes = [18, 15, 13, 12, 11];

    /// <summary>The editor's JSON (see <see cref="UniverDocument"/>) as a PDF file.</summary>
    public static byte[] Write(string? univerJson) => Write(UniverDocument.FromJson(univerJson));

    /// <summary>The blocks as a PDF file.</summary>
    public static byte[] Write(IReadOnlyList<DocBlock> blocks)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        return Document.Create(container => AddDocument(container, blocks)).GeneratePdf();
    }

    /// <summary>The HTML (a template rendered to HTML, say) as a PDF file, through <see cref="HtmlBlocks.FromHtml"/>.</summary>
    public static byte[] WriteHtml(string? html) => Write(HtmlBlocks.FromHtml(html));

    /// <summary>
    /// Several documents as one PDF, each starting on a new page: the mail merge's output, one letter after another. The page
    /// numbers count through the whole file.
    /// </summary>
    public static byte[] WriteMerged(IEnumerable<IReadOnlyList<DocBlock>> documents)
    {
        ArgumentNullException.ThrowIfNull(documents);
        var list = documents.ToList();
        return Document.Create(container =>
        {
            foreach (var blocks in list) AddDocument(container, blocks);
        }).GeneratePdf();
    }

    // One document: its pages, A4 with the margins and the footer.
    private static void AddDocument(IDocumentContainer container, IReadOnlyList<DocBlock> blocks) =>
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.MarginHorizontal(MarginX);
            page.MarginVertical(MarginY);
            page.DefaultTextStyle(style => style.FontSize(10.5f).LineHeight(1.3f));
            page.Content().Column(column => Blocks(column, blocks));
            page.Footer().AlignCenter().Text(text =>
            {
                text.Span("Page ").FontColor(Muted);
                text.CurrentPageNumber().FontColor(Muted);
                text.Span(" of ").FontColor(Muted);
                text.TotalPages().FontColor(Muted);
            });
        });

    // Lays out a run of blocks. Numbers count per level: an item continues the count of the item before it at its level, and
    // a bullet or a deeper item does not stop it (a numbered list may hold a bulleted note); any other block starts a new list.
    private static void Blocks(ColumnDescriptor column, IEnumerable<DocBlock> blocks)
    {
        var counts = new Dictionary<int, int>();
        foreach (var block in blocks)
        {
            switch (block.Kind)
            {
                case DocBlockKind.Numbered:
                    var number = counts.TryGetValue(block.Level, out var last) ? last + 1 : 1;
                    counts[block.Level] = number;
                    RemoveDeeper(counts, block.Level);
                    ListItem(column, block, $"{number}.");
                    break;

                case DocBlockKind.Bullet:
                    counts.Remove(block.Level);
                    RemoveDeeper(counts, block.Level);
                    ListItem(column, block, "•");
                    break;

                default:
                    counts.Clear();
                    Block(column, block);
                    break;
            }
        }
    }

    private static void RemoveDeeper(Dictionary<int, int> counts, int level)
    {
        foreach (var key in counts.Keys.Where(k => k > level).ToList()) counts.Remove(key);
    }

    private static void Block(ColumnDescriptor column, DocBlock block)
    {
        switch (block.Kind)
        {
            case DocBlockKind.Title:
                column.Item().PaddingBottom(8).DefaultTextStyle(style => style.FontSize(22).Bold()).Text(text => Runs(text, block));
                break;

            case DocBlockKind.Subtitle:
                column.Item().PaddingBottom(10).DefaultTextStyle(style => style.FontSize(12).FontColor(Muted)).Text(text => Runs(text, block));
                break;

            case DocBlockKind.Heading:
                var size = HeadingSizes[Math.Clamp(block.Level, 1, HeadingSizes.Length) - 1];
                column.Item().PaddingTop(8).PaddingBottom(4).DefaultTextStyle(style => style.FontSize(size).Bold())
                    .Text(text => Runs(text, block));
                break;

            case DocBlockKind.Table:
                Table(column, block);
                break;

            default:
                column.Item().PaddingBottom(6).Text(text => Runs(text, block));
                break;
        }
    }

    // A bullet or a number in a fixed column, with the item's text beside it and indented by its level.
    private static void ListItem(ColumnDescriptor column, DocBlock block, string marker) =>
        column.Item().PaddingLeft(block.Level * ListIndent).PaddingBottom(3).Row(row =>
        {
            row.ConstantItem(BulletWidth).Text(marker);
            row.RelativeItem().Text(text => Runs(text, block));
        });

    private static void Table(ColumnDescriptor column, DocBlock block)
    {
        var rows = block.Rows ?? [];
        var columns = rows.Count == 0 ? 0 : rows.Max(r => r.Count);
        if (columns == 0) return;

        column.Item().PaddingTop(4).PaddingBottom(8).Table(table =>
        {
            // A column's share follows its widest text in points (about 5.6 per character at the table's 9 pt, plus the padding), so a short
            // column such as "#" stays narrow and a code such as "ST-00123" does not wrap.
            var widths = Enumerable.Range(0, columns).Select(i => (float)Math.Max(14, rows.Max(r => i < r.Count ? Length(r[i]) : 0) * 5.6 + 9)).ToArray();
            table.ColumnsDefinition(definition =>
            {
                foreach (var width in widths) definition.RelativeColumn(width);
            });
            foreach (var row in rows)
            {
                // A short row gets empty cells, so every row has the same columns.
                for (var i = 0; i < columns; i++)
                {
                    IReadOnlyList<DocBlock> cell = i < row.Count ? row[i] : [];
                    table.Cell().Border(0.5f).BorderColor(Rule).Padding(4).DefaultTextStyle(style => style.FontSize(9)).Column(content => Blocks(content, cell));
                }
            }
        });
    }

    // The characters of a cell's text (the blocks' runs, joined), for sizing its column.
    private static int Length(IReadOnlyList<DocBlock> cell) => cell.Sum(b => b.Runs.Sum(r => r.Text.Length));

    // The runs of a block, each with its own style, and the block's alignment.
    private static void Runs(TextDescriptor text, DocBlock block)
    {
        switch (block.Align)
        {
            case DocAlign.Center: text.AlignCenter(); break;
            case DocAlign.End: text.AlignRight(); break;
            case DocAlign.Justify: text.Justify(); break;
        }
        foreach (var run in block.Runs)
        {
            var span = text.Span(run.Text);
            if (run.Bold) span.Bold();
            if (run.Italic) span.Italic();
            if (run.Underline) span.Underline();
            if (run.Strike) span.Strikethrough();
            if (run.FontSize is { } size) span.FontSize((float)size);
            if (run.Color is { } color) span.FontColor(color);
        }
    }
}
