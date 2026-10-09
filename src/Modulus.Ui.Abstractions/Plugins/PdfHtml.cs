using System.Net;
using System.Text;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.Ui.Abstractions.Plugins;

/// <summary>
/// The PDF viewer's markup, shared by the Blazor <c>AppPdf</c> and the MVC <c>app-pdf</c>. pdf.js (modulus-plugins.js) draws
/// the page into the canvas and owns the controls: previous and next, a page box, zoom (1 is fit to the width) and the download
/// link. The markup is the same in both front ends, so the script works from either.
/// </summary>
public static class PdfHtml
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>
    /// A PDF viewer for <paramref name="url"/>: a same-site path (starting with <c>/</c>) or an <c>http(s)</c> address. Any other
    /// address is refused, so a viewer never loads a <c>javascript:</c> or <c>data:</c> URL. The viewer opens on
    /// <paramref name="page"/>, clamped to the document's pages once it has loaded.
    /// </summary>
    public static string Viewer(string id, string url, string? label = null, string height = "640px", int page = 1, string? cssClass = null)
    {
        var source = Source(url);
        var sb = new StringBuilder();
        sb.Append($"""<div class="m-pdf{(cssClass is { Length: > 0 } c ? " " + E(c) : "")}" id="{E(id)}" data-m-pdf role="region" aria-label="{E(label ?? "PDF document")}" data-src="{E(source)}" data-page="{Math.Max(1, page)}" style="height: {E(height)}">""");
        sb.Append("""<div class="m-pdf-toolbar" role="toolbar" aria-label="PDF pages">""");
        sb.Append($"""<button type="button" class="ant-btn ant-btn-default ant-btn-icon-only" data-m-pdf-prev aria-label="Previous page" title="Previous page" disabled>{ShellRenderer.Icon("left")}</button>""");
        sb.Append($"""<label class="m-pdf-page"><span class="m-sr-only">Page</span><input type="number" class="ant-input" data-m-pdf-page min="1" value="{Math.Max(1, page)}" aria-label="Page" /><span>of <span data-m-pdf-count>&ndash;</span></span></label>""");
        sb.Append($"""<button type="button" class="ant-btn ant-btn-default ant-btn-icon-only" data-m-pdf-next aria-label="Next page" title="Next page" disabled>{ShellRenderer.Icon("right")}</button>""");
        sb.Append("""<span class="m-spacer"></span>""");
        sb.Append("""<button type="button" class="ant-btn ant-btn-default ant-btn-icon-only" data-m-pdf-zoom-out aria-label="Zoom out" title="Zoom out">&minus;</button>""");
        sb.Append("""<span class="m-pdf-zoom" data-m-pdf-zoom-label aria-live="polite">100%</span>""");
        sb.Append("""<button type="button" class="ant-btn ant-btn-default ant-btn-icon-only" data-m-pdf-zoom-in aria-label="Zoom in" title="Zoom in">+</button>""");
        sb.Append($"""<a class="ant-btn ant-btn-default" href="{E(source)}" download data-m-pdf-download><span>Download</span></a>""");
        sb.Append("</div>");
        sb.Append("""<div class="m-pdf-scroll" data-m-pdf-scroll>""");
        sb.Append("""<div class="m-pdf-loading" data-m-pdf-loading role="status">Loading PDF&hellip;</div>""");
        sb.Append("""<div class="m-pdf-canvas" data-m-pdf-canvas></div>""");
        sb.Append("</div></div>");
        return sb.ToString();
    }

    private static string Source(string url)
    {
        if (url.StartsWith('/') && !url.StartsWith("//", StringComparison.Ordinal)) return url;
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) return url;
        throw new ArgumentException("A PDF is a same-site path or an http(s) address.", nameof(url));
    }
}
