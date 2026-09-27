using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Modulus.Ui.Abstractions.Forms;

/// <summary>
/// Allow-list HTML cleaning for the rich text field (notes, terms and conditions, descriptions). Keeps paragraphs,
/// line breaks, headings, emphasis, lists, quotes, code and links with an http(s), mailto, tel or relative href;
/// drops every other tag (and the content of script, style and embedded documents), every attribute but
/// <c>href</c>, and comments. Tags are balanced, so stored HTML can never close the page's own elements.
/// Run it on the server before storing or rendering what a browser posted.
/// </summary>
public static partial class RichText
{
    private static readonly HashSet<string> Allowed = new(StringComparer.OrdinalIgnoreCase)
    {
        "p", "div", "br", "hr", "b", "strong", "i", "em", "u", "s", "strike", "sub", "sup", "code", "pre",
        "h1", "h2", "h3", "h4", "blockquote", "ul", "ol", "li", "a",
    };

    private static readonly HashSet<string> Void = new(StringComparer.OrdinalIgnoreCase) { "br", "hr" };

    private static readonly HashSet<string> DropWithContent = new(StringComparer.OrdinalIgnoreCase)
    {
        "script", "style", "iframe", "object", "embed", "template", "noscript", "textarea", "select", "svg", "math", "head", "title", "frameset", "frame", "xml",
    };

    public static string Sanitize(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        var sb = new StringBuilder(html.Length);
        var open = new List<string>();
        string? skip = null;
        var i = 0;
        while (i < html.Length)
        {
            if (html[i] != '<')
            {
                var next = html.IndexOf('<', i);
                if (next < 0) next = html.Length;
                if (skip is null) sb.Append(WebUtility.HtmlEncode(WebUtility.HtmlDecode(html[i..next])));
                i = next;
                continue;
            }
            if (string.CompareOrdinal(html, i, "<!--", 0, 4) == 0)
            {
                var end = html.IndexOf("-->", i + 4, StringComparison.Ordinal);
                i = end < 0 ? html.Length : end + 3;
                continue;
            }
            var m = Tag().Match(html, i);
            if (!m.Success)
            {
                if (i + 1 < html.Length && html[i + 1] is '!' or '?')
                {
                    var end = html.IndexOf('>', i);
                    i = end < 0 ? html.Length : end + 1;
                    continue;
                }
                if (skip is null) sb.Append("&lt;");
                i++;
                continue;
            }
            i += m.Length;
            var closing = m.Groups[1].Length > 0;
            var name = m.Groups[2].Value.ToLowerInvariant();

            if (skip is not null)
            {
                if (closing && name == skip) skip = null;
                continue;
            }
            if (DropWithContent.Contains(name))
            {
                if (!closing && !m.Groups[3].Value.TrimEnd().EndsWith('/')) skip = name;
                continue;
            }
            if (!Allowed.Contains(name)) continue;

            if (closing)
            {
                var at = open.LastIndexOf(name);
                if (at < 0) continue;
                for (var k = open.Count - 1; k >= at; k--) sb.Append("</").Append(open[k]).Append('>');
                open.RemoveRange(at, open.Count - at);
                continue;
            }
            if (Void.Contains(name))
            {
                sb.Append('<').Append(name).Append('>');
                continue;
            }
            if (name == "a")
            {
                var href = SafeHref(m.Groups[3].Value);
                sb.Append(href is null ? "<a>" : $"<a href=\"{WebUtility.HtmlEncode(href)}\" rel=\"noopener noreferrer\">");
            }
            else sb.Append('<').Append(name).Append('>');
            open.Add(name);
        }
        for (var k = open.Count - 1; k >= 0; k--) sb.Append("</").Append(open[k]).Append('>');
        return sb.ToString();
    }

    /// <summary>The text without markup, whitespace collapsed (for "required" checks, previews and search).</summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrEmpty(html)) return "";
        // Block ends become spaces, inline tags vanish, and only then are entities decoded (so typed "&lt;b&gt;" stays text).
        var text = WebUtility.HtmlDecode(AnyTag().Replace(Breaks().Replace(Sanitize(html), " "), ""));
        return Spaces().Replace(text, " ").Trim();
    }

    private static string? SafeHref(string attributes)
    {
        var m = Href().Match(attributes);
        if (!m.Success) return null;
        var raw = m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Success ? m.Groups[3].Value : m.Groups[4].Value;
        var href = WebUtility.HtmlDecode(raw).Trim();
        var compact = string.Concat(href.Where(c => c > ' '));
        var colon = compact.IndexOf(':');
        var boundary = compact.IndexOfAny(['/', '?', '#']);
        if (colon >= 0 && (boundary < 0 || colon < boundary))
        {
            var scheme = compact[..colon].ToLowerInvariant();
            return scheme is "http" or "https" or "mailto" or "tel" ? compact : null;
        }
        return compact.Length == 0 ? null : compact;
    }

    [GeneratedRegex("""\G<(/?)([a-zA-Z][a-zA-Z0-9]*)((?:[^>"']|"[^"]*"|'[^']*')*)>""")]
    private static partial Regex Tag();

    [GeneratedRegex("""\bhref\s*=\s*("([^"]*)"|'([^']*)'|([^\s>"']+))""", RegexOptions.IgnoreCase)]
    private static partial Regex Href();

    [GeneratedRegex("<br>|</p>|</div>|</li>|</h[1-4]>")]
    private static partial Regex Breaks();

    [GeneratedRegex("<[^>]*>")]
    private static partial Regex AnyTag();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}

/// <summary>Signatures drawn in the signature pad arrive as a PNG data URL.</summary>
public static class SignatureImage
{
    private const string Prefix = "data:image/png;base64,";

    /// <summary>The largest data URL accepted: 1 MB of text.</summary>
    public const int MaxLength = 1_000_000;

    /// <summary>Decodes a posted signature; false when it is empty, too large or not a PNG data URL.</summary>
    public static bool TryDecode(string? dataUrl, out byte[] png)
    {
        png = [];
        if (string.IsNullOrEmpty(dataUrl) || dataUrl.Length > MaxLength || !dataUrl.StartsWith(Prefix, StringComparison.Ordinal)) return false;
        try
        {
            png = Convert.FromBase64String(dataUrl[Prefix.Length..]);
        }
        catch (FormatException)
        {
            return false;
        }
        // The PNG signature: 89 50 4E 47 0D 0A 1A 0A.
        return png.Length > 8 && png[0] == 0x89 && png[1] == 0x50 && png[2] == 0x4E && png[3] == 0x47;
    }

    /// <summary>True for a value the pad could have produced (see <see cref="TryDecode"/>).</summary>
    public static bool IsValid(string? dataUrl) => TryDecode(dataUrl, out _);
}

/// <summary>A rich text toolbar button: a browser editing command (<c>bold</c>, <c>formatBlock</c> + <c>h3</c>, …).</summary>
public sealed record RichTextButton(string Command, string Label, string? Icon, string? Text = null, string? Argument = null)
{
    /// <summary>Shows pressed while the selection has the format (bold, lists).</summary>
    public bool Toggle { get; init; }

    public string? Shortcut { get; init; }

    public string Title => Shortcut is null ? Label : $"{Label} ({Shortcut})";
}

/// <summary>The toolbar of AppRichText and &lt;app-rich-text&gt;, in groups.</summary>
public static class RichTextToolbar
{
    public static IReadOnlyList<IReadOnlyList<RichTextButton>> Groups { get; } =
    [
        [
            new("bold", "Bold", "bold") { Toggle = true, Shortcut = "Ctrl+B" },
            new("italic", "Italic", "italic") { Toggle = true, Shortcut = "Ctrl+I" },
            new("underline", "Underline", "underline") { Toggle = true, Shortcut = "Ctrl+U" },
            new("strikeThrough", "Strikethrough", "strikethrough") { Toggle = true },
        ],
        [
            new("formatBlock", "Heading", null, "H", "h3"),
            new("formatBlock", "Paragraph", null, "¶", "p"),
            new("formatBlock", "Quote", null, "❝", "blockquote"),
        ],
        [
            new("insertUnorderedList", "Bulleted list", "unordered-list") { Toggle = true },
            new("insertOrderedList", "Numbered list", "ordered-list") { Toggle = true },
            new("outdent", "Decrease indent", "menu-fold"),
            new("indent", "Increase indent", "menu-unfold"),
        ],
        [
            new("createLink", "Link", "link") { Shortcut = "Ctrl+K" },
            new("unlink", "Remove link", "disconnect"),
        ],
        [
            new("removeFormat", "Clear formatting", "clear"),
            new("undo", "Undo", "undo") { Shortcut = "Ctrl+Z" },
            new("redo", "Redo", "redo") { Shortcut = "Ctrl+Y" },
        ],
    ];
}
