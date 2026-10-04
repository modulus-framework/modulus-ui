using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.Ui.Abstractions.Plugins;

/// <summary>A button of the WYSIWYG editor toolbar: a Tiptap command (<c>bold</c>, <c>list</c> + <c>bullet</c>, …) or command.</summary>
public sealed record EditorButton(string Format, string Label, string Icon, string? Argument = null, string? Shortcut = null)
{
    public string Title => Shortcut is null ? Label : $"{Label} ({Shortcut})";
}

/// <summary>
/// Markup of the WYSIWYG editor (<c>AppEditor</c> in Blazor, <c>&lt;app-editor&gt;</c> in MVC), a Tiptap editor with an Ant Design
/// toolbar. modulus-plugins.js ([data-m-editor]) loads the bundled Tiptap build and keeps <c>[data-m-editor-value]</c> in step with
/// the HTML, so a form posts it and Blazor reads it from a change event. Clean what comes back with
/// <see cref="Forms.RichText.Sanitize(string?, bool)"/> (images allowed).
/// </summary>
public static class EditorHtml
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>The toolbar, in groups; the heading list comes first.</summary>
    public static IReadOnlyList<IReadOnlyList<EditorButton>> Groups { get; } =
    [
        [
            new("bold", "Bold", "bold", Shortcut: "Ctrl+B"),
            new("italic", "Italic", "italic", Shortcut: "Ctrl+I"),
            new("underline", "Underline", "underline", Shortcut: "Ctrl+U"),
            new("strike", "Strikethrough", "strikethrough"),
        ],
        [
            new("list", "Numbered list", "ordered-list", "ordered"),
            new("list", "Bulleted list", "unordered-list", "bullet"),
            new("blockquote", "Quote", "message"),
            new("code-block", "Code block", "code"),
        ],
        [
            new("link", "Link", "link", Shortcut: "Ctrl+K"),
            new("image", "Image from a URL", "picture"),
        ],
        [
            new("clean", "Clear formatting", "clear"),
            new("undo", "Undo", "undo", Shortcut: "Ctrl+Z"),
            new("redo", "Redo", "redo", Shortcut: "Ctrl+Y"),
        ],
    ];

    /// <summary>The toolbar markup (heading list, button groups).</summary>
    public static string Toolbar(bool disabled = false)
    {
        var sb = new StringBuilder("""<div class="m-editor-toolbar" role="toolbar" aria-label="Formatting">""");
        sb.Append($"""<select class="m-editor-heading" data-m-ed-heading aria-label="Text style"{(disabled ? " disabled" : "")}>""")
          .Append("""<option value="">Normal</option><option value="1">Heading 1</option><option value="2">Heading 2</option><option value="3">Heading 3</option></select>""");
        foreach (var group in Groups)
        {
            sb.Append("""<span class="m-editor-sep" aria-hidden="true"></span><span class="m-editor-group">""");
            foreach (var b in group)
            {
                sb.Append($"""<button type="button" class="m-editor-btn" data-m-ed="{E(b.Format)}"{(b.Argument is null ? "" : $" data-arg=\"{E(b.Argument)}\"")} title="{E(b.Title)}" aria-label="{E(b.Label)}""").Append('"')
                  .Append(b.Format is "link" or "image" or "clean" or "undo" or "redo" ? "" : " aria-pressed=\"false\"")
                  .Append(disabled ? " disabled" : "")
                  .Append('>').Append(AntIconSvg.Svg(b.Icon) ?? "").Append("</button>");
            }
            sb.Append("</span>");
        }
        return sb.Append("</div>").ToString();
    }

    /// <summary>The link / image address bar under the toolbar (hidden until needed).</summary>
    public const string Bar =
        """<div class="m-editor-bar" data-m-editor-bar hidden><span class="m-editor-bar-title" data-m-editor-bar-title>Link</span>""" +
        """<input type="url" class="ant-input ant-input-sm" autocomplete="off" />""" +
        """<button type="button" class="ant-btn ant-btn-primary ant-btn-sm" data-m-editor-bar-apply><span>Apply</span></button>""" +
        """<button type="button" class="ant-btn ant-btn-default ant-btn-sm" data-m-editor-bar-remove hidden><span>Remove</span></button>""" +
        """<button type="button" class="ant-btn ant-btn-text ant-btn-sm" data-m-editor-bar-cancel><span>Cancel</span></button></div>""";

    /// <summary>The footer: word count (filled in by the script) and a hint.</summary>
    public const string Footer =
        """<div class="m-editor-footer"><span data-m-editor-count></span><span class="m-editor-hint">Ctrl+K adds a link</span></div>""";

    /// <summary>Attributes of the editor root (<c>data-m-editor</c>, placeholder, label).</summary>
    public static string RootAttributes(string? placeholder, string? label, string? labelledBy, bool readOnly, int minHeight) =>
        $""" data-m-editor data-m-debounce{(placeholder is null ? "" : $" data-placeholder=\"{E(placeholder)}\"")}{(label is null ? "" : $" data-label=\"{E(label)}\"")}""" +
        $"""{(labelledBy is null ? "" : $" data-labelledby=\"{E(labelledBy)}\"")}{(readOnly ? " data-readonly" : "")} style="--m-editor-min-height: {minHeight}px" """;

    /// <summary>
    /// The whole editor for MVC: toolbar, host with the starting HTML (cleaned with <see cref="Forms.RichText.Sanitize(string?, bool)"/>),
    /// address bar, footer and the hidden value. <paramref name="label"/> names the editing area for screen readers.
    /// </summary>
    public static string Render(string name, string? id, string? html, string? placeholder = null, string? label = null, bool readOnly = false,
        int minHeight = 220, bool invalid = false, string? cssClass = null)
    {
        var value = Forms.RichText.Sanitize(html, images: true);
        return $"""<div class="m-editor{(invalid ? " m-editor-status-error" : "")}{(cssClass is { Length: > 0 } c ? " " + E(c) : "")}"{RootAttributes(placeholder, label, null, readOnly, minHeight)}>""" +
               Toolbar(readOnly) + Bar +
               $"""<div class="m-editor-host">{value}</div>""" +
               Footer +
               $"""<input type="hidden" name="{E(name)}"{(id is null ? "" : $" id=\"{E(id)}\"")} value="{E(value)}" data-m-editor-value /></div>""";
    }
}
