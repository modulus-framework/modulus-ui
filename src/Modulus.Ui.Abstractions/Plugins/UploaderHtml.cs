using System.Net;
using System.Text;

namespace Modulus.Ui.Abstractions.Plugins;

/// <summary>
/// The drop area on Uppy, shared by the Blazor <c>AppUploader</c> and the MVC <c>app-uploader</c>. Uppy (modulus-plugins.js)
/// draws the Dashboard into the host: a drop area, the file list with previews and progress, and removal. Each file uploads
/// as soon as it is added: one POST per file, under the form field the markup names, to the action it names. The limits are
/// checked in the browser first and then again by the endpoint, which must still refuse anything it does not accept.
/// </summary>
public static class UploaderHtml
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    private static string Attr(string name, string value) => $" {name}=\"{E(value)}\"";

    /// <summary>
    /// The uploader for <paramref name="action"/>. <paramref name="accept"/> takes extensions (<c>.pdf</c>) and MIME patterns
    /// (<c>image/*</c>), comma separated; <paramref name="maxSize"/> is in bytes and <paramref name="maxCount"/> is the most
    /// files the drop area takes (0 is no limit). A <paramref name="action"/> that is not a same-site path or an http(s)
    /// address is refused, so a file never goes to a <c>javascript:</c> or <c>data:</c> URL.
    /// </summary>
    public static string Dashboard(string id, string action, string name = "file", string? accept = null, long maxSize = 0, int maxCount = 0,
        string? note = null, string? label = null, string? cssClass = null)
    {
        if (maxSize < 0) throw new ArgumentOutOfRangeException(nameof(maxSize), "A size limit is not negative.");
        if (maxCount < 0) throw new ArgumentOutOfRangeException(nameof(maxCount), "A count limit is not negative.");
        var target = Target(action);
        var sb = new StringBuilder();
        sb.Append("<div class=\"m-uploader");
        if (cssClass is { Length: > 0 }) sb.Append(' ').Append(E(cssClass));
        sb.Append('"').Append(Attr("id", id)).Append(" data-m-uploader role=\"region\"").Append(Attr("aria-label", label ?? "File upload"));
        sb.Append(Attr("data-action", target)).Append(Attr("data-name", name));
        if (!string.IsNullOrWhiteSpace(accept)) sb.Append(Attr("data-accept", accept));
        if (maxSize > 0) sb.Append(Attr("data-max-size", maxSize.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (maxCount > 0) sb.Append(Attr("data-max-count", maxCount.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        if (!string.IsNullOrWhiteSpace(note)) sb.Append(Attr("data-note", note));
        sb.Append('>');
        sb.Append("""<div class="m-uploader-host" data-m-uploader-host></div>""");
        sb.Append("""<p class="m-uploader-fallback" data-m-uploader-fallback>Loading the file uploader&hellip;</p>""");
        sb.Append("""<p class="m-uploader-result" data-m-uploader-result role="status" aria-live="polite"></p>""");
        sb.Append("</div>");
        return sb.ToString();
    }

    private static string Target(string action)
    {
        if (action.StartsWith('/') && !action.StartsWith("//", StringComparison.Ordinal)) return action;
        if (Uri.TryCreate(action, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps)) return action;
        throw new ArgumentException("An upload goes to a same-site path or an http(s) address.", nameof(action));
    }
}
