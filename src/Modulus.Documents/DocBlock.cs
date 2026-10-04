namespace Modulus.Documents;

/// <summary>What a <see cref="DocBlock"/> is: a paragraph, a title, a heading, a list item or a table.</summary>
public enum DocBlockKind { Paragraph, Title, Subtitle, Heading, Bullet, Numbered, Table }

/// <summary>Horizontal alignment of a paragraph; <see cref="Start"/> is the default left edge.</summary>
public enum DocAlign { Start, Center, End, Justify }

/// <summary>
/// A stretch of text with one style. <paramref name="FontSize"/> is in points and <paramref name="Color"/> a "#rrggbb"
/// colour; null keeps the paragraph's own. A plain string converts to an unstyled run.
/// </summary>
public sealed record DocRun(string Text, bool Bold = false, bool Italic = false, bool Underline = false, bool Strike = false,
    double? FontSize = null, string? Color = null)
{
    public static implicit operator DocRun(string text) => new(text);

    internal bool SameStyle(DocRun other) => Bold == other.Bold && Italic == other.Italic && Underline == other.Underline
        && Strike == other.Strike && FontSize == other.FontSize && Color == other.Color;
}

/// <summary>
/// One block of a document, independent of the editor and the file format: the shape <see cref="UniverDocument"/> reads and
/// writes as the editor's JSON and <see cref="DocumentDocx"/> as Word files, so application code builds and reads documents
/// (templates, letters, contracts) without either. <paramref name="Level"/> is the heading level (1–5) of a
/// <see cref="DocBlockKind.Heading"/> or the nesting depth (0–8) of a list item. A <see cref="DocBlockKind.Table"/> has
/// <paramref name="Rows"/> of cells, each a list of blocks, and no runs.
/// </summary>
public sealed record DocBlock(DocBlockKind Kind, IReadOnlyList<DocRun> Runs, int Level = 0, DocAlign Align = DocAlign.Start,
    IReadOnlyList<IReadOnlyList<IReadOnlyList<DocBlock>>>? Rows = null)
{
    /// <summary>The plain text of the block's runs (empty for a table).</summary>
    public string Text => string.Concat(Runs.Select(r => r.Text));

    public static DocBlock Paragraph(params DocRun[] runs) => new(DocBlockKind.Paragraph, runs);

    public static DocBlock Aligned(DocAlign align, params DocRun[] runs) => new(DocBlockKind.Paragraph, runs, Align: align);

    public static DocBlock Title(string text) => new(DocBlockKind.Title, [text]);

    public static DocBlock Subtitle(string text) => new(DocBlockKind.Subtitle, [text]);

    /// <summary>A heading of level 1 (largest) to 5.</summary>
    public static DocBlock Heading(int level, string text) => new(DocBlockKind.Heading, [text], Math.Clamp(level, 1, 5));

    public static DocBlock Bullet(string text, int level = 0) => new(DocBlockKind.Bullet, [text], Math.Clamp(level, 0, 8));

    /// <summary>An item of a numbered list; consecutive items count on, anything else between them starts a new list.</summary>
    public static DocBlock Numbered(string text, int level = 0) => new(DocBlockKind.Numbered, [text], Math.Clamp(level, 0, 8));

    /// <summary>A table of plain-text cells; the first row is an ordinary row (make it bold with runs for a header).</summary>
    public static DocBlock Table(IEnumerable<IEnumerable<string>> rows) =>
        TableOf(rows.Select(r => r.Select(c => (IReadOnlyList<DocBlock>)[Paragraph(c)])));

    /// <summary>A table whose cells hold blocks (styled paragraphs, lists).</summary>
    public static DocBlock TableOf(IEnumerable<IEnumerable<IReadOnlyList<DocBlock>>> rows) =>
        new(DocBlockKind.Table, [], Rows: rows.Select(r => (IReadOnlyList<IReadOnlyList<DocBlock>>)r.ToList()).ToList());
}
