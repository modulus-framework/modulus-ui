using System.Net;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.Ui.Abstractions.Plugins;

/// <summary>
/// The "Download PDF" button of a form, shared by the Blazor <c>AppPdfButton</c> and the MVC <c>app-pdf-button</c>. The button
/// submits the form to its action, and the server answers with the PDF (the server side is <c>DocumentPdf</c>, from the
/// <c>Modulus.Documents</c> package). <c>data-m-reload</c> lets the page's script reset its state after the download.
/// </summary>
public static class PdfDownloadHtml
{
    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");

    /// <summary>
    /// A submit button that posts the form <paramref name="formId"/> to <paramref name="action"/>. The action is a same-site path
    /// (starting with <c>/</c>, not <c>//</c>): the form's data, a document's JSON for one, never goes to another site, and a
    /// <c>javascript:</c> or <c>data:</c> address is refused.
    /// </summary>
    public static string Button(string formId, string action, string label = "Download PDF", string? cssClass = null)
    {
        if (string.IsNullOrWhiteSpace(formId)) throw new ArgumentException("The button needs the id of the form it submits.", nameof(formId));
        if (!action.StartsWith('/') || action.StartsWith("//", StringComparison.Ordinal))
            throw new ArgumentException("A PDF is made from a same-site path.", nameof(action));

        var cls = "ant-btn ant-btn-default" + (cssClass is { Length: > 0 } c ? " " + E(c) : "");
        return $"""<button type="submit" form="{E(formId)}" formaction="{E(action)}" data-m-reload class="{cls}">{ShellRenderer.Icon("download")}<span>{E(label)}</span></button>""";
    }
}
