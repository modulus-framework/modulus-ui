using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Modulus.Documents;

/// <summary>
/// The document editor's JSON (a Univer Docs snapshot, what <c>AppDocument</c> / <c>app-document</c> take and post) to and
/// from <see cref="DocBlock"/>s. Univer keeps a document as one text stream with marker characters (\r ends a paragraph,
/// \n a section, \u001A…\u000F a table) plus style ranges over it; this walks that stream. What the blocks cannot hold
/// (images, links, page headers and footers, cell colours) is left out on the way to blocks; the editor's own JSON keeps it.
/// </summary>
public static class UniverDocument
{
    // An A4 page at 96 dpi with 1-inch side margins, laid out as pages (Univer's "traditional" flavour).
    private const double PageWidth = 793.7, PageHeight = 1122.5, MarginX = 96, MarginY = 72;
    private const double ContentWidth = PageWidth - 2 * MarginX;

    private const char Paragraph = '\r', Section = '\n', TableStart = '\u001A', RowStart = '\u001B', CellStart = '\u001C',
        CellEnd = '\u001D', RowEnd = '\u000E', TableEnd = '\u000F', DocEnd = '\0';

    /// <summary>The blocks as the editor's JSON: an A4 document of headings, paragraphs, lists and tables.</summary>
    public static string ToJson(IEnumerable<DocBlock> blocks, string? title = null)
    {
        var w = new Writer();
        var list = blocks.ToList();
        w.Blocks(list);
        if (list.Count == 0 || list[^1].Kind == DocBlockKind.Table) w.Paragraph(DocBlock.Paragraph()); // Univer ends on a paragraph
        w.Section();

        var doc = new JsonObject
        {
            ["id"] = "doc",
            ["title"] = title ?? "",
            ["tableSource"] = w.TableSource,
            ["body"] = new JsonObject
            {
                ["dataStream"] = w.Stream.ToString(),
                ["textRuns"] = w.TextRuns,
                ["paragraphs"] = w.Paragraphs,
                ["sectionBreaks"] = w.SectionBreaks,
                ["tables"] = w.Tables,
            },
            ["documentStyle"] = new JsonObject
            {
                ["pageSize"] = new JsonObject { ["width"] = PageWidth, ["height"] = PageHeight },
                ["documentFlavor"] = 1,
                ["marginTop"] = MarginY,
                ["marginBottom"] = MarginY,
                ["marginLeft"] = MarginX,
                ["marginRight"] = MarginX,
                // Word's rhythm: a little air below each paragraph, slightly open lines.
                ["defaultParagraphStyle"] = new JsonObject
                {
                    ["spaceAbove"] = new JsonObject { ["v"] = 0 },
                    ["lineSpacing"] = 1.15,
                    ["spaceBelow"] = new JsonObject { ["v"] = 8 },
                },
            },
        };
        return doc.ToJsonString();
    }

    /// <summary>The editor's JSON as blocks; empty when the JSON is empty or not a document.</summary>
    public static IReadOnlyList<DocBlock> FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        JsonNode? root;
        try { root = JsonNode.Parse(json); }
        catch (JsonException) { return []; }
        if (root?["body"] is not JsonObject body || Str(body["dataStream"]) is not { } stream) return [];
        return new Reader(stream, body).Read();
    }

    /// <summary>The editor's JSON as plain text: one line per paragraph, table cells separated by tabs.</summary>
    public static string ToText(string? json)
    {
        var sb = new StringBuilder();
        void Lines(IEnumerable<DocBlock> blocks)
        {
            foreach (var b in blocks)
            {
                if (b.Rows is { } rows)
                    foreach (var row in rows) sb.AppendLine(string.Join('\t', row.Select(c => string.Join(' ', c.Select(p => p.Text)))));
                else sb.AppendLine(b.Text);
            }
        }
        Lines(FromJson(json));
        return sb.ToString().TrimEnd();
    }

    private sealed class Writer
    {
        public readonly StringBuilder Stream = new();
        public readonly JsonArray TextRuns = [], Paragraphs = [], SectionBreaks = [], Tables = [];
        public readonly JsonObject TableSource = [];
        private int _ids;
        private DocBlockKind? _listKind;
        private string? _listId;

        private string Id(string prefix) => prefix + (++_ids).ToString(CultureInfo.InvariantCulture);

        public void Blocks(IReadOnlyList<DocBlock> blocks)
        {
            foreach (var block in blocks)
            {
                if (block.Kind == DocBlockKind.Table) Table(block);
                else Paragraph(block);
            }
        }

        public void Section()
        {
            SectionBreaks.Add(new JsonObject { ["startIndex"] = Stream.Length, ["sectionId"] = Id("s") });
            Stream.Append(UniverDocument.Section);
        }

        public void Paragraph(DocBlock block, JsonObject? style = null)
        {
            foreach (var run in block.Runs)
            {
                var text = Clean(run.Text);
                if (text.Length == 0) continue;
                var start = Stream.Length;
                Stream.Append(text);
                if (RunStyle(run) is { } ts)
                    TextRuns.Add(new JsonObject { ["st"] = start, ["ed"] = Stream.Length, ["ts"] = ts });
            }

            style ??= [];
            switch (block.Kind)
            {
                case DocBlockKind.Title: style["namedStyleType"] = 2; break;
                case DocBlockKind.Subtitle: style["namedStyleType"] = 3; break;
                case DocBlockKind.Heading: style["namedStyleType"] = 3 + Math.Clamp(block.Level, 1, 5); break;
            }
            if (block.Align != DocAlign.Start)
                style["horizontalAlign"] = block.Align switch { DocAlign.Center => 2, DocAlign.End => 3, _ => 4 };

            var paragraph = new JsonObject { ["startIndex"] = Stream.Length, ["paragraphId"] = Id("p"), ["paragraphStyle"] = style };
            if (block.Kind is DocBlockKind.Bullet or DocBlockKind.Numbered)
            {
                if (_listKind != block.Kind) { _listKind = block.Kind; _listId = Id("l"); }
                paragraph["bullet"] = new JsonObject
                {
                    ["listType"] = block.Kind == DocBlockKind.Bullet ? "BULLET_LIST" : "ORDER_LIST",
                    ["listId"] = _listId,
                    ["nestingLevel"] = Math.Clamp(block.Level, 0, 8),
                };
            }
            else _listKind = null;
            Paragraphs.Add(paragraph);
            Stream.Append(UniverDocument.Paragraph);
        }

        // A table as Univer's empty-table command builds one: every cell holds paragraphs and a section break, the
        // columns share the content width, and rows are padded to the widest one.
        private void Table(DocBlock block)
        {
            _listKind = null;
            var rows = block.Rows ?? [];
            if (rows.Count == 0) return;
            var columns = Math.Max(1, rows.Max(r => r.Count));
            var id = Id("t");
            var start = Stream.Length;
            Stream.Append(TableStart);
            var tableRows = new JsonArray();
            foreach (var row in rows)
            {
                Stream.Append(RowStart);
                var cells = new JsonArray();
                for (var c = 0; c < columns; c++)
                {
                    Stream.Append(CellStart);
                    var content = c < row.Count ? row[c].Where(b => b.Kind != DocBlockKind.Table).ToList() : [];
                    if (content.Count == 0) content.Add(DocBlock.Paragraph());
                    foreach (var p in content) Paragraph(p, CellParagraph());
                    _listKind = null;
                    Section();
                    Stream.Append(CellEnd);
                    cells.Add(new JsonObject { ["margin"] = Margin() });
                }
                Stream.Append(RowEnd);
                tableRows.Add(new JsonObject
                {
                    ["tableCells"] = cells,
                    ["trHeight"] = new JsonObject { ["val"] = new JsonObject { ["v"] = 30 }, ["hRule"] = 0 },
                });
            }
            Stream.Append(TableEnd);
            Tables.Add(new JsonObject { ["startIndex"] = start, ["endIndex"] = Stream.Length, ["tableId"] = id });

            var tableColumns = new JsonArray();
            for (var c = 0; c < columns; c++)
                tableColumns.Add(new JsonObject { ["size"] = Size(1, ContentWidth / columns) });
            TableSource[id] = new JsonObject
            {
                ["tableId"] = id,
                ["tableRows"] = tableRows,
                ["tableColumns"] = tableColumns,
                ["align"] = 0,
                ["indent"] = new JsonObject { ["v"] = 0 },
                ["textWrap"] = 0,
                ["position"] = new JsonObject
                {
                    ["positionH"] = new JsonObject { ["relativeFrom"] = 0, ["posOffset"] = 0 },
                    ["positionV"] = new JsonObject { ["relativeFrom"] = 0, ["posOffset"] = 0 },
                },
                ["dist"] = new JsonObject { ["distB"] = 0, ["distL"] = 0, ["distR"] = 0, ["distT"] = 0 },
                ["cellMargin"] = Margin(),
                ["size"] = Size(0, ContentWidth),
            };
        }

        private static JsonObject CellParagraph() => new()
        {
            ["spaceAbove"] = new JsonObject { ["v"] = 3 },
            ["lineSpacing"] = 1.15,
            ["spaceBelow"] = new JsonObject { ["v"] = 3 },
        };

        private static JsonObject Margin() => new()
        {
            ["start"] = new JsonObject { ["v"] = 10 },
            ["end"] = new JsonObject { ["v"] = 10 },
            ["top"] = new JsonObject { ["v"] = 5 },
            ["bottom"] = new JsonObject { ["v"] = 5 },
        };

        private static JsonObject Size(int type, double width) => new() { ["type"] = type, ["width"] = new JsonObject { ["v"] = width } };

        private static JsonObject? RunStyle(DocRun run)
        {
            var ts = new JsonObject();
            if (run.Bold) ts["bl"] = 1;
            if (run.Italic) ts["it"] = 1;
            if (run.Underline) ts["ul"] = new JsonObject { ["s"] = 1 };
            if (run.Strike) ts["st"] = new JsonObject { ["s"] = 1 };
            if (run.FontSize is > 0 and var size) ts["fs"] = size;
            if (Hex(run.Color) is { } color) ts["cl"] = new JsonObject { ["rgb"] = color };
            return ts.Count == 0 ? null : ts;
        }

        // Marker characters and line breaks inside a run would break the stream apart; they become spaces (tabs stay).
        private static string Clean(string? text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            var chars = text.ToCharArray();
            for (var i = 0; i < chars.Length; i++)
                if (chars[i] < ' ' && chars[i] != '\t') chars[i] = ' ';
            return new string(chars);
        }
    }

    private sealed class Reader(string stream, JsonObject body)
    {
        private readonly Dictionary<int, JsonObject> _paragraphs = (body["paragraphs"] as JsonArray ?? [])
            .OfType<JsonObject>().Where(p => p["startIndex"] is not null)
            .GroupBy(p => Int(p["startIndex"])).ToDictionary(g => g.Key, g => g.First());

        private readonly (int St, int Ed, JsonObject Ts)[] _runs = (body["textRuns"] as JsonArray ?? [])
            .OfType<JsonObject>().Where(r => r["ts"] is JsonObject)
            .Select(r => (Int(r["st"]), Int(r["ed"]), (JsonObject)r["ts"]!)).OrderBy(r => r.Item1).ToArray();

        private int _i;

        public IReadOnlyList<DocBlock> Read() => Blocks(inCell: false);

        private List<DocBlock> Blocks(bool inCell)
        {
            var blocks = new List<DocBlock>();
            var start = _i;
            while (_i < stream.Length)
            {
                var c = stream[_i];
                if (c == CellEnd && inCell) break;
                if (c == DocEnd) { _i = stream.Length; break; }
                if (c == TableStart)
                {
                    var table = Table();
                    if (inCell) blocks.AddRange(table.Rows!.SelectMany(r => r.SelectMany(cell => cell))); // nested: flattened
                    else blocks.Add(table);
                    start = _i;
                    continue;
                }
                if (c == UniverDocument.Paragraph)
                {
                    blocks.Add(Block(start, _i));
                    start = ++_i;
                    continue;
                }
                if (c == UniverDocument.Section) start = _i + 1;
                _i++;
            }
            return blocks;
        }

        private DocBlock Table()
        {
            _i++; // TABLE_START
            var rows = new List<IReadOnlyList<IReadOnlyList<DocBlock>>>();
            List<IReadOnlyList<DocBlock>>? row = null;
            while (_i < stream.Length)
            {
                var c = stream[_i];
                if (c == TableEnd) { _i++; break; }
                if (c == RowStart) { row = []; _i++; }
                else if (c == RowEnd) { if (row is not null) rows.Add(row); row = null; _i++; }
                else if (c == CellStart)
                {
                    _i++;
                    var cell = Blocks(inCell: true);
                    if (_i < stream.Length && stream[_i] == CellEnd) _i++;
                    (row ??= []).Add(cell);
                }
                else _i++; // stray characters between the markers
            }
            return new DocBlock(DocBlockKind.Table, [], Rows: rows);
        }

        private DocBlock Block(int start, int end)
        {
            var runs = Runs(start, end);
            if (!_paragraphs.TryGetValue(end, out var p)) return new DocBlock(DocBlockKind.Paragraph, runs);
            var style = p["paragraphStyle"] as JsonObject;
            var align = Int(style?["horizontalAlign"]) switch { 2 => DocAlign.Center, 3 => DocAlign.End, 4 or 5 => DocAlign.Justify, _ => DocAlign.Start };
            if (p["bullet"] is JsonObject bullet)
            {
                // ORDER_LIST… count; BULLET_LIST… and CHECK_LIST… do not.
                var ordered = (Str(bullet["listType"]) ?? "").Contains("ORDER", StringComparison.OrdinalIgnoreCase);
                return new DocBlock(ordered ? DocBlockKind.Numbered : DocBlockKind.Bullet, runs, Math.Clamp(Int(bullet["nestingLevel"]), 0, 8), align);
            }
            return Int(style?["namedStyleType"]) switch
            {
                2 => new DocBlock(DocBlockKind.Title, runs, Align: align),
                3 => new DocBlock(DocBlockKind.Subtitle, runs, Align: align),
                >= 4 and <= 8 and var n => new DocBlock(DocBlockKind.Heading, runs, n - 3, align),
                _ => new DocBlock(DocBlockKind.Paragraph, runs, Align: align),
            };
        }

        // The text between two paragraph marks, cut into runs where the style changes; marker characters (links,
        // images) are dropped.
        private List<DocRun> Runs(int start, int end)
        {
            var runs = new List<DocRun>();
            var sb = new StringBuilder();
            DocRun? style = null;
            var r = 0;
            for (var i = start; i < end; i++)
            {
                var c = stream[i];
                if (c < ' ' && c != '\t') continue;
                while (r < _runs.Length && _runs[r].Ed <= i) r++;
                var next = r < _runs.Length && _runs[r].St <= i ? Style(_runs[r].Ts) : Plain;
                if (style is not null && !style.SameStyle(next))
                {
                    runs.Add(style with { Text = sb.ToString() });
                    sb.Clear();
                }
                style = next;
                sb.Append(c);
            }
            if (style is not null && sb.Length > 0) runs.Add(style with { Text = sb.ToString() });
            return runs;
        }

        private static readonly DocRun Plain = new("");

        private static DocRun Style(JsonObject ts) => new("",
            Bold: Int(ts["bl"]) == 1,
            Italic: Int(ts["it"]) == 1,
            Underline: Int(ts["ul"]?["s"]) == 1,
            Strike: Int(ts["st"]?["s"]) == 1,
            FontSize: ts["fs"] is JsonValue fs && fs.TryGetValue<double>(out var size) && size > 0 ? size : null,
            Color: Hex(Str(ts["cl"]?["rgb"])));

        private static int Int(JsonNode? node) =>
            node is JsonValue v && v.TryGetValue<double>(out var d) ? (int)d : 0;
    }

    private static string? Str(JsonNode? node) => node is JsonValue v && v.TryGetValue<string>(out var s) ? s : null;

    /// <summary>"#rgb", "#rrggbb" or "rgb(r, g, b)" as "#rrggbb"; null for anything else.</summary>
    public static string? Hex(string? color)
    {
        if (string.IsNullOrWhiteSpace(color)) return null;
        color = color.Trim();
        if (color.StartsWith('#'))
        {
            var hex = color[1..];
            if (hex.Length == 3) hex = string.Concat(hex.Select(h => $"{h}{h}"));
            return hex.Length == 6 && hex.All(Uri.IsHexDigit) ? "#" + hex.ToLowerInvariant() : null;
        }
        if (color.StartsWith("rgb", StringComparison.OrdinalIgnoreCase) && color.IndexOf('(') is > 0 and var open)
        {
            var parts = color[(open + 1)..].TrimEnd(')').Split(',', StringSplitOptions.TrimEntries);
            if (parts.Length >= 3 && parts.Take(3).All(p => byte.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out _)))
                return "#" + string.Concat(parts.Take(3).Select(p => byte.Parse(p, CultureInfo.InvariantCulture).ToString("x2", CultureInfo.InvariantCulture)));
        }
        return null;
    }
}
