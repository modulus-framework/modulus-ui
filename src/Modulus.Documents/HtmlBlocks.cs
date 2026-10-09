using System.Net;
using System.Text.RegularExpressions;
using Modulus.Ui.Abstractions.Forms;

namespace Modulus.Documents;

/// <summary>
/// Reads HTML into <see cref="DocBlock"/>s, so a template rendered to HTML (a Liquid page, say) becomes a PDF or a docx. The HTML
/// first goes through <see cref="RichText.Sanitize(string?)"/>, so only the rich-text tags come through: paragraphs and divs,
/// headings 1–4, bold, italic, underline and strike, bulleted and numbered lists (nested by level), tables, and links (kept as
/// their text; the address is not printed). Images are dropped, as is any tag outside that set (its text is kept). A table's
/// column and row spans are flattened: each cell is one cell. A line break (<c>&lt;br&gt;</c>) becomes a space, as it does in the
/// Word output, so a line break in a block needs its own paragraph. Inline code is printed in the body font.
/// </summary>
public static partial class HtmlBlocks
{
    private sealed record Style(bool Bold = false, bool Italic = false, bool Underline = false, bool Strike = false);

    private sealed class Node
    {
        public string? Name { get; init; }
        public string? Text { get; init; }
        public List<Node> Children { get; } = [];
    }

    private static readonly HashSet<string> Inline = new(StringComparer.OrdinalIgnoreCase)
    {
        "b", "strong", "i", "em", "u", "s", "strike", "sub", "sup", "code", "a", "br", "span",
    };

    private static readonly HashSet<string> Void = new(StringComparer.OrdinalIgnoreCase) { "br", "hr" };

    /// <summary>The blocks of an HTML fragment, after sanitizing. Empty or null input gives no blocks.</summary>
    public static IReadOnlyList<DocBlock> FromHtml(string? html)
    {
        var output = new List<DocBlock>();
        if (string.IsNullOrWhiteSpace(html)) return output;

        Flow(Parse(RichText.Sanitize(html)), new Style(), output);
        return output;
    }

    // A block-level container: its inline text becomes paragraphs, and its block children are read in turn.
    private static void Flow(Node parent, Style style, List<DocBlock> output)
    {
        var runs = new List<DocRun>();
        foreach (var child in parent.Children)
        {
            if (child.Text is not null || IsInline(child.Name))
            {
                InlineRuns(child, style, runs);
                continue;
            }

            Flush(runs, output);
            switch (child.Name?.ToLowerInvariant())
            {
                case "h1" or "h2" or "h3" or "h4":
                    var heading = new List<DocRun>();
                    InlineRuns(child, style, heading);
                    if (Trim(heading) is { Count: > 0 } headingRuns)
                        output.Add(new DocBlock(DocBlockKind.Heading, headingRuns, Level: child.Name![1] - '0'));
                    break;

                case "ul":
                    Items(child, 0, DocBlockKind.Bullet, style, output);
                    break;

                case "ol":
                    Items(child, 0, DocBlockKind.Numbered, style, output);
                    break;

                case "table":
                    output.Add(Table(child));
                    break;

                case "hr":
                    break;

                default: // p, div, blockquote, pre and anything else: their content, as blocks
                    Flow(child, style, output);
                    break;
            }
        }
        Flush(runs, output);
    }

    // The items of a list: each item's text, then the lists nested inside it one level down.
    private static void Items(Node list, int level, DocBlockKind kind, Style style, List<DocBlock> output)
    {
        foreach (var item in list.Children.Where(c => c.Name?.Equals("li", StringComparison.OrdinalIgnoreCase) == true))
        {
            var runs = new List<DocRun>();
            var nested = new List<Node>();
            foreach (var child in item.Children)
            {
                if (child.Name is "ul" or "ol") nested.Add(child);
                else InlineRuns(child, style, runs);
            }
            output.Add(new DocBlock(kind, Trim(runs), level));
            foreach (var sub in nested)
                Items(sub, level + 1, sub.Name == "ol" ? DocBlockKind.Numbered : DocBlockKind.Bullet, style, output);
        }
    }

    private static DocBlock Table(Node table)
    {
        var rows = new List<List<IReadOnlyList<DocBlock>>>();
        foreach (var row in Rows(table))
        {
            var cells = new List<IReadOnlyList<DocBlock>>();
            foreach (var cell in row.Children.Where(c => c.Name is "td" or "th"))
            {
                var blocks = new List<DocBlock>();
                Flow(cell, cell.Name == "th" ? new Style(Bold: true) : new Style(), blocks);
                cells.Add(blocks);
            }
            rows.Add(cells);
        }
        return DocBlock.TableOf(rows);
    }

    // The rows of a table, directly or in its head, body and foot groups.
    private static IEnumerable<Node> Rows(Node table)
    {
        foreach (var child in table.Children)
        {
            if (child.Name == "tr") yield return child;
            else if (child.Name is "thead" or "tbody" or "tfoot")
                foreach (var row in child.Children.Where(c => c.Name == "tr")) yield return row;
        }
    }

    // The text of an inline node with its style (a nested inline node adds its own), in document order.
    private static void InlineRuns(Node node, Style style, List<DocRun> runs)
    {
        if (node.Text is { } text)
        {
            runs.Add(new DocRun(Spaces().Replace(text, " "), style.Bold, style.Italic, style.Underline, style.Strike));
            return;
        }
        if (node.Name == "br")
        {
            runs.Add(new DocRun(" "));
            return;
        }
        var inner = node.Name?.ToLowerInvariant() switch
        {
            "b" or "strong" => style with { Bold = true },
            "i" or "em" => style with { Italic = true },
            "u" => style with { Underline = true },
            "s" or "strike" => style with { Strike = true },
            _ => style,
        };
        foreach (var child in node.Children) InlineRuns(child, inner, runs);
    }

    private static void Flush(List<DocRun> runs, List<DocBlock> output)
    {
        if (Trim(runs) is { Count: > 0 } trimmed) output.Add(new DocBlock(DocBlockKind.Paragraph, trimmed));
        runs.Clear();
    }

    // HTML collapses a space that follows another space, even across two runs, and drops the spaces at the start and end of a
    // block. Empty runs are dropped.
    private static List<DocRun> Trim(List<DocRun> runs)
    {
        var list = new List<DocRun>();
        var afterSpace = true;
        foreach (var run in runs)
        {
            var text = afterSpace ? run.Text.TrimStart() : run.Text;
            if (text.Length == 0) continue;
            list.Add(run with { Text = text });
            afterSpace = text.EndsWith(' ');
        }
        if (list.Count > 0) list[^1] = list[^1] with { Text = list[^1].Text.TrimEnd() };
        list.RemoveAll(r => r.Text.Length == 0);
        return list;
    }

    private static bool IsInline(string? name) => name is not null && Inline.Contains(name);

    // The sanitized HTML is well formed (every tag it writes is closed, and attribute values are encoded), so this reads it
    // straight: a tag ends at the first '>', and text is decoded when it is read.
    private static Node Parse(string html)
    {
        var root = new Node();
        var stack = new List<Node> { root };
        var i = 0;
        while (i < html.Length)
        {
            if (html[i] != '<')
            {
                var next = html.IndexOf('<', i);
                if (next < 0) next = html.Length;
                stack[^1].Children.Add(new Node { Text = WebUtility.HtmlDecode(html[i..next]) });
                i = next;
                continue;
            }

            var end = html.IndexOf('>', i);
            if (end < 0) break;
            var tag = html[(i + 1)..end];
            i = end + 1;

            var closing = tag.StartsWith('/');
            var name = Name().Match(closing ? tag[1..] : tag).Value.ToLowerInvariant();
            if (name.Length == 0) continue;

            if (closing)
            {
                // Close the nearest open element of that name (and the ones opened inside it); an unmatched close is ignored.
                var at = stack.FindLastIndex(n => n.Name == name);
                if (at > 0) stack.RemoveRange(at, stack.Count - at);
                continue;
            }

            var node = new Node { Name = name };
            stack[^1].Children.Add(node);
            if (!Void.Contains(name)) stack.Add(node);
        }
        return root;
    }

    [GeneratedRegex("^[a-zA-Z][a-zA-Z0-9]*")]
    private static partial Regex Name();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
