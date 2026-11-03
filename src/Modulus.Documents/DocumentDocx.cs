using System.Globalization;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Modulus.Documents;

/// <summary>
/// Word files in and out of the document editor, through <see cref="DocBlock"/>s and the Open XML SDK (MIT; the one ClosedXML
/// already brings). Out: titles, headings (Word's built-in styles, so the navigation pane and a table of contents see them),
/// paragraphs with bold, italic, underline, strike, size and colour, alignment, bullet and numbered lists, and tables with
/// borders, on A4. In: the same from any docx; images, headers and footers, comments and tracked deletions are left out,
/// and tables inside tables are flattened into their cell.
/// </summary>
public static class DocumentDocx
{
    /// <summary>The MIME type of a docx file, for download endpoints.</summary>
    public const string ContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    // A4 with 1-inch side margins and 0.75-inch top and bottom ones, in twentieths of a point (as the editor lays it out).
    private const int PageWidth = 11906, PageHeight = 16838, MarginX = 1440, MarginY = 1080;
    private const int ContentWidth = PageWidth - 2 * MarginX;
    private const int BulletNumbering = 1, OrderedAbstract = 2;

    /// <summary>The editor's JSON (see <see cref="UniverDocument"/>) as a docx file.</summary>
    public static byte[] Write(string? univerJson) => Write(UniverDocument.FromJson(univerJson));

    /// <summary>The HTML (a template rendered to HTML, say) as a docx file, through <see cref="HtmlBlocks.FromHtml"/>.</summary>
    public static byte[] WriteHtml(string? html) => Write(HtmlBlocks.FromHtml(html));

    /// <summary>The blocks as a docx file.</summary>
    public static byte[] Write(IReadOnlyList<DocBlock> blocks)
    {
        using var bytes = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(bytes, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.AddNewPart<StyleDefinitionsPart>().Styles = Styles();
            var writer = new Writer();
            var body = new Body();
            foreach (var element in writer.Blocks(blocks)) body.Append(element);
            body.Append(new SectionProperties(
                new PageSize { Width = PageWidth, Height = PageHeight },
                new PageMargin { Top = MarginY, Bottom = MarginY, Left = MarginX, Right = MarginX, Header = 708, Footer = 708, Gutter = 0 }));
            main.Document = new Document(body);
            main.AddNewPart<NumberingDefinitionsPart>().Numbering = writer.Numbering();
        }
        return bytes.ToArray();
    }

    /// <summary>A docx file (an upload) as blocks.</summary>
    /// <exception cref="InvalidDataException">The file is not a Word document.</exception>
    public static IReadOnlyList<DocBlock> Read(Stream docx)
    {
        WordprocessingDocument doc;
        try { doc = WordprocessingDocument.Open(docx, false); }
        catch (OpenXmlPackageException e) { throw new InvalidDataException("The file is not a Word document.", e); }
        using var _ = doc;
        var main = doc.MainDocumentPart ?? throw new InvalidDataException("The file has no document part.");
        var body = main.Document?.Body ?? throw new InvalidDataException("The document has no body.");
        return new Reader(main).Blocks(body.ChildElements, nested: false);
    }

    /// <summary>A docx file (an upload) as the editor's JSON.</summary>
    public static string ReadJson(Stream docx, string? title = null) => UniverDocument.ToJson(Read(docx), title);

    private sealed class Writer
    {
        private int _orderedLists;
        private DocBlockKind? _listKind;
        private int _numId;

        public IEnumerable<OpenXmlElement> Blocks(IReadOnlyList<DocBlock> blocks)
        {
            OpenXmlElement? last = null;
            foreach (var block in blocks)
            {
                // Two tables in a row would merge into one in Word; an empty paragraph keeps them apart.
                if (block.Kind == DocBlockKind.Table && last is Table) yield return new Paragraph();
                last = block.Kind == DocBlockKind.Table ? Table(block) : Paragraph(block);
                yield return last;
            }
        }

        private Paragraph Paragraph(DocBlock block)
        {
            var props = new ParagraphProperties();
            var style = block.Kind switch
            {
                DocBlockKind.Title => "Title",
                DocBlockKind.Subtitle => "Subtitle",
                DocBlockKind.Heading => "Heading" + Math.Clamp(block.Level, 1, 5),
                DocBlockKind.Bullet or DocBlockKind.Numbered => "ListParagraph",
                _ => null,
            };
            if (style is not null) props.ParagraphStyleId = new ParagraphStyleId { Val = style };
            if (block.Kind is DocBlockKind.Bullet or DocBlockKind.Numbered)
            {
                // Each run of numbered items is its own list, counting from 1; bullets share one definition.
                if (_listKind != block.Kind)
                {
                    _listKind = block.Kind;
                    _numId = block.Kind == DocBlockKind.Bullet ? BulletNumbering : OrderedAbstract + ++_orderedLists;
                }
                props.NumberingProperties = new NumberingProperties(
                    new NumberingLevelReference { Val = Math.Clamp(block.Level, 0, 8) }, new NumberingId { Val = _numId });
            }
            else _listKind = null;
            if (block.Align != DocAlign.Start)
                props.Justification = new Justification
                {
                    Val = block.Align switch { DocAlign.Center => JustificationValues.Center, DocAlign.End => JustificationValues.Right, _ => JustificationValues.Both },
                };

            var paragraph = new Paragraph();
            if (props.HasChildren) paragraph.Append(props);
            foreach (var run in block.Runs.Where(r => !string.IsNullOrEmpty(r.Text))) paragraph.Append(Run(run));
            return paragraph;
        }

        private static Run Run(DocRun source)
        {
            var run = new Run();
            var props = new RunProperties();
            if (source.Bold) props.Bold = new Bold();
            if (source.Italic) props.Italic = new Italic();
            if (source.Strike) props.Strike = new Strike();
            if (UniverDocument.Hex(source.Color) is { } color) props.Color = new Color { Val = color[1..] };
            if (source.FontSize is > 0 and var size)
                props.FontSize = new FontSize { Val = Math.Round(size * 2).ToString(CultureInfo.InvariantCulture) };
            if (source.Underline) props.Underline = new Underline { Val = UnderlineValues.Single };
            if (props.HasChildren) run.Append(props);
            var parts = source.Text.Split('\t');
            for (var i = 0; i < parts.Length; i++)
            {
                if (i > 0) run.Append(new TabChar());
                if (parts[i].Length > 0) run.Append(new Text(parts[i]) { Space = SpaceProcessingModeValues.Preserve });
            }
            return run;
        }

        private Table Table(DocBlock block)
        {
            _listKind = null;
            var rows = block.Rows ?? [];
            var columns = Math.Max(1, rows.Count == 0 ? 1 : rows.Max(r => r.Count));
            var width = ContentWidth / columns;
            var table = new Table(new TableProperties(
                new TableStyle { Val = "TableGrid" },
                new TableWidth { Type = TableWidthUnitValues.Dxa, Width = (width * columns).ToString(CultureInfo.InvariantCulture) },
                new TableLayout { Type = TableLayoutValues.Fixed }));
            var grid = new TableGrid();
            for (var c = 0; c < columns; c++) grid.Append(new GridColumn { Width = width.ToString(CultureInfo.InvariantCulture) });
            table.Append(grid);
            foreach (var row in rows)
            {
                var tr = new TableRow();
                for (var c = 0; c < columns; c++)
                {
                    var cell = new TableCell(new TableCellProperties(
                        new TableCellWidth { Type = TableWidthUnitValues.Dxa, Width = width.ToString(CultureInfo.InvariantCulture) }));
                    var content = c < row.Count ? row[c].Where(b => b.Kind != DocBlockKind.Table).ToList() : [];
                    foreach (var p in content) cell.Append(Paragraph(p));
                    _listKind = null;
                    if (content.Count == 0) cell.Append(new Paragraph()); // Word needs a paragraph in every cell
                    tr.Append(cell);
                }
                table.Append(tr);
            }
            return table;
        }

        // The bullet list (num 1) and one numbered list per run of numbered items (num 3, 4…), all abstract numbering
        // before the nums as the schema wants.
        public Numbering Numbering()
        {
            var numbering = new Numbering(
                Abstract(BulletNumbering, level => (NumberFormatValues.Bullet, (level % 3) switch { 0 => "•", 1 => "◦", _ => "▪" })),
                Abstract(OrderedAbstract, level => ((level % 3) switch
                {
                    0 => NumberFormatValues.Decimal,
                    1 => NumberFormatValues.LowerLetter,
                    _ => NumberFormatValues.LowerRoman,
                }, $"%{level + 1}.")),
                new NumberingInstance(new AbstractNumId { Val = BulletNumbering }) { NumberID = BulletNumbering });
            for (var i = 1; i <= _orderedLists; i++)
                numbering.Append(new NumberingInstance(
                    new AbstractNumId { Val = OrderedAbstract },
                    new LevelOverride(new StartOverrideNumberingValue { Val = 1 }) { LevelIndex = 0 })
                { NumberID = OrderedAbstract + i });
            return numbering;
        }

        private static AbstractNum Abstract(int id, Func<int, (NumberFormatValues Format, string Text)> level)
        {
            var abstractNum = new AbstractNum(new MultiLevelType { Val = MultiLevelValues.HybridMultilevel }) { AbstractNumberId = id };
            for (var l = 0; l < 9; l++)
            {
                var (format, text) = level(l);
                abstractNum.Append(new Level(
                    new StartNumberingValue { Val = 1 },
                    new NumberingFormat { Val = format },
                    new LevelText { Val = text },
                    new LevelJustification { Val = LevelJustificationValues.Left },
                    new PreviousParagraphProperties(new Indentation
                    {
                        Left = (720 * (l + 1)).ToString(CultureInfo.InvariantCulture),
                        Hanging = "360",
                    }))
                { LevelIndex = l });
            }
            return abstractNum;
        }
    }

    // Normal text, the title, subtitle and heading styles, a list paragraph and a bordered table grid.
    private static Styles Styles()
    {
        static Style Paragraph(string id, string name, int halfPoints, bool bold = false, string? color = null, int after = 160, bool heading = false)
        {
            var run = new StyleRunProperties();
            if (bold) run.Append(new Bold());
            if (color is not null) run.Append(new Color { Val = color });
            run.Append(new FontSize { Val = halfPoints.ToString(CultureInfo.InvariantCulture) });
            var paragraph = new StyleParagraphProperties();
            if (heading) paragraph.Append(new KeepNext());
            paragraph.Append(new SpacingBetweenLines { Before = heading ? "240" : "0", After = after.ToString(CultureInfo.InvariantCulture) });
            var style = new Style(new StyleName { Val = name }, new BasedOn { Val = "Normal" }, new NextParagraphStyle { Val = "Normal" },
                new PrimaryStyle(), paragraph, run)
            { Type = StyleValues.Paragraph, StyleId = id };
            return style;
        }

        var normal = new Style(new StyleName { Val = "Normal" }, new PrimaryStyle()) { Type = StyleValues.Paragraph, StyleId = "Normal", Default = true };
        var list = new Style(new StyleName { Val = "List Paragraph" }, new BasedOn { Val = "Normal" },
            new StyleParagraphProperties(new SpacingBetweenLines { After = "60" }, new ContextualSpacing()))
        { Type = StyleValues.Paragraph, StyleId = "ListParagraph" };
        var border = () => new[]
        {
            (OpenXmlElement)new TopBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
            new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
            new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
            new RightBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "BFBFBF" },
        };
        var grid = new Style(new StyleName { Val = "Table Grid" },
            new StyleParagraphProperties(new SpacingBetweenLines { After = "0" }),
            new StyleTableProperties(new TableBorders(border()),
                new TableCellMarginDefault(new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                    new TableCellLeftMargin { Width = 108, Type = TableWidthValues.Dxa },
                    new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                    new TableCellRightMargin { Width = 108, Type = TableWidthValues.Dxa })))
        { Type = StyleValues.Table, StyleId = "TableGrid" };

        return new Styles(
            new DocDefaults(
                new RunPropertiesDefault(new RunPropertiesBaseStyle(
                    new RunFonts { Ascii = "Calibri", HighAnsi = "Calibri", EastAsia = "Calibri", ComplexScript = "Calibri" },
                    new FontSize { Val = "22" }, new FontSizeComplexScript { Val = "22" })),
                new ParagraphPropertiesDefault(new ParagraphPropertiesBaseStyle(
                    new SpacingBetweenLines { After = "160", Line = "276", LineRule = LineSpacingRuleValues.Auto }))),
            normal,
            Paragraph("Title", "Title", 52, after: 120),
            Paragraph("Subtitle", "Subtitle", 30, color: "595959"),
            Paragraph("Heading1", "heading 1", 36, bold: true, heading: true),
            Paragraph("Heading2", "heading 2", 30, bold: true, heading: true),
            Paragraph("Heading3", "heading 3", 26, bold: true, heading: true),
            Paragraph("Heading4", "heading 4", 24, bold: true, heading: true),
            Paragraph("Heading5", "heading 5", 22, bold: true, heading: true),
            list,
            grid);
    }

    private sealed class Reader(MainDocumentPart main)
    {
        // Style ids to their names ("Heading1" → "heading 1"), so headings in any language's template are found by name.
        private readonly Dictionary<string, string> _styleNames = (main.StyleDefinitionsPart?.Styles?.Elements<Style>() ?? [])
            .Where(s => s.StyleId?.Value is not null)
            .GroupBy(s => s.StyleId!.Value!)
            .ToDictionary(g => g.Key, g => (g.First().StyleName?.Val?.Value ?? g.Key).ToLowerInvariant());

        // List definitions: whether a numbered paragraph shows bullets or counts.
        private readonly Numbering? _numbering = main.NumberingDefinitionsPart?.Numbering;

        public List<DocBlock> Blocks(IEnumerable<OpenXmlElement> elements, bool nested)
        {
            var blocks = new List<DocBlock>();
            foreach (var element in elements)
            {
                switch (element)
                {
                    case Paragraph p: blocks.Add(Paragraph(p)); break;
                    case Table t when nested: // tables inside tables come out as their cells' paragraphs
                        foreach (var cell in t.Descendants<TableCell>())
                            blocks.AddRange(Blocks(cell.Elements<Paragraph>(), nested: true));
                        break;
                    case Table t:
                        blocks.Add(new DocBlock(DocBlockKind.Table, [], Rows: t.Elements<TableRow>()
                            .Select(r => (IReadOnlyList<IReadOnlyList<DocBlock>>)r.Elements<TableCell>()
                                .Select(c => (IReadOnlyList<DocBlock>)Blocks(c.ChildElements, nested: true)).ToList())
                            .Where(r => r.Count > 0).ToList()));
                        break;
                    case SdtBlock sdt when sdt.SdtContentBlock is { } content: blocks.AddRange(Blocks(content.ChildElements, nested)); break;
                }
            }
            return blocks;
        }

        private DocBlock Paragraph(Paragraph p)
        {
            var props = p.ParagraphProperties;
            var runs = Runs(p);
            var align = props?.Justification?.Val?.Value is { } jc
                ? jc == JustificationValues.Center ? DocAlign.Center
                : jc == JustificationValues.Right || jc == JustificationValues.End ? DocAlign.End
                : jc == JustificationValues.Both || jc == JustificationValues.Distribute ? DocAlign.Justify
                : DocAlign.Start
                : DocAlign.Start;

            if (props?.NumberingProperties is { NumberingId.Val.Value: > 0 and var numId } numPr)
            {
                var level = Math.Clamp(numPr.NumberingLevelReference?.Val?.Value ?? 0, 0, 8);
                return new DocBlock(IsBullet(numId, level) ? DocBlockKind.Bullet : DocBlockKind.Numbered, runs, level, align);
            }

            var styleId = props?.ParagraphStyleId?.Val?.Value;
            var name = styleId is not null && _styleNames.TryGetValue(styleId, out var n) ? n : styleId?.ToLowerInvariant() ?? "";
            if (name == "title") return new DocBlock(DocBlockKind.Title, runs, Align: align);
            if (name == "subtitle") return new DocBlock(DocBlockKind.Subtitle, runs, Align: align);
            if ((name.StartsWith("heading", StringComparison.Ordinal)) && int.TryParse(name.AsSpan(7).Trim(), out var heading) && heading >= 1)
                return new DocBlock(DocBlockKind.Heading, runs, Math.Min(heading, 5), align);
            return new DocBlock(DocBlockKind.Paragraph, runs, Align: align);
        }

        private bool IsBullet(int numId, int level)
        {
            var instance = _numbering?.Elements<NumberingInstance>().FirstOrDefault(n => n.NumberID?.Value == numId);
            var abstractId = instance?.AbstractNumId?.Val?.Value;
            var abstractNum = _numbering?.Elements<AbstractNum>().FirstOrDefault(a => a.AbstractNumberId?.Value == abstractId);
            var format = abstractNum?.Elements<Level>().FirstOrDefault(l => l.LevelIndex?.Value == level)?.NumberingFormat?.Val?.Value;
            return format is null || format == NumberFormatValues.Bullet;
        }

        // The paragraph's runs (also inside links and insertions, not in text boxes), merged where the style is the same.
        private static List<DocRun> Runs(Paragraph p)
        {
            var runs = new List<DocRun>();
            foreach (var run in p.Descendants<Run>().Where(r => !r.Ancestors<TextBoxContent>().Any()))
            {
                var text = string.Concat(run.ChildElements.Select(e => e switch
                {
                    Text t => t.Text,
                    TabChar => "\t",
                    Break or CarriageReturn => " ",
                    NoBreakHyphen => "-",
                    _ => "",
                }));
                if (text.Length == 0) continue;
                var rp = run.RunProperties;
                var style = new DocRun(text,
                    Bold: On(rp?.Bold),
                    Italic: On(rp?.Italic),
                    Underline: rp?.Underline?.Val?.Value is { } u ? u != UnderlineValues.None : rp?.Underline is not null,
                    Strike: On(rp?.Strike),
                    FontSize: double.TryParse(rp?.FontSize?.Val?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var half) && half > 0 ? half / 2 : null,
                    Color: rp?.Color?.Val?.Value is { Length: 6 } hex ? UniverDocument.Hex("#" + hex) : null);
                if (runs.Count > 0 && runs[^1].SameStyle(style)) runs[^1] = runs[^1] with { Text = runs[^1].Text + text };
                else runs.Add(style);
            }
            return runs;
        }

        private static bool On(OnOffType? value) => value is not null && (value.Val is null || value.Val.Value);
    }
}
