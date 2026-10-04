using System.Net;
using System.Text;

namespace Modulus.Ui.Abstractions.Plugins;

/// <summary>
/// Markup of the document editor (<c>AppDocument</c> in Blazor, <c>&lt;app-document&gt;</c> in MVC): a Word-style page editor on
/// Univer Docs with a ribbon (headings, fonts, lists, tables, images, links, alignment) and paged A4 layout.
/// modulus-plugins.js starts Univer on <c>[data-m-doc]</c> when it appears; edits land in the hidden <c>[data-m-doc-value]</c>
/// input as the document's JSON (a Univer Docs snapshot), so the editor posts with its form and Blazor binds it without
/// interop. Build or read that JSON on the server with <c>Modulus.Documents.UniverDocument</c>, and convert it to and from
/// Word files with <c>Modulus.Documents.DocumentDocx</c>. To print the document (on its page size and margins), use the
/// ribbon's Print button, Ctrl+P in the editor, or any button with <c>data-m-print="{id}"</c>, which also works read-only.
/// </summary>
public static class DocumentHtml
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>A document editor.</summary>
    /// <param name="id">Element id of the editor.</param>
    /// <param name="document">The document's JSON; null or empty starts a blank page.</param>
    /// <param name="editable">True (default) shows the ribbon; false shows the pages to read.</param>
    /// <param name="height">CSS height, e.g. "640px" or "100%".</param>
    /// <param name="name">Form field name; the hidden input posts the edited document's JSON under it.</param>
    public static string Render(string id, string? document, bool editable = true,
        string height = "640px", string? name = null, string? label = null, string? cssClass = null)
    {
        var data = document ?? "";
        var sb = new StringBuilder($"""<div class="m-doc{(cssClass is { Length: > 0 } c ? " " + E(c) : "")}" id="{E(id)}" data-m-doc data-doc="{E(data)}" """);
        if (!editable) sb.Append("data-readonly ");
        sb.Append($"""role="region" aria-label="{E(label ?? "Document")}" style="height: {E(height)}">""");
        sb.Append("""<div class="m-sheet-loading">Loading document…</div>""");
        if (name is { Length: > 0 })
            sb.Append($"""<input type="hidden" name="{E(name)}" id="{E(id)}-value" value="{E(data)}" data-m-doc-value>""");
        sb.Append("</div>");
        return sb.ToString();
    }
}
