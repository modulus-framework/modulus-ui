using System.Net;
using System.Text;

namespace Modulus.Ui.Abstractions.Assistant;

/// <summary>The upload field of the knowledge base: a file input for the accepted types, an upload button, and the documents already in it.</summary>
public static class UploadHtml
{
    /// <summary>The markup for a form that posts <c>name</c> as multipart files. The host puts it in a form with enctype multipart/form-data.</summary>
    public static string Field(string name, IEnumerable<KnowledgeDocument> documents, string label = "Add documents to the knowledge base", string accept = DocumentText.Accept)
    {
        var id = "upload-" + name;
        var sb = new StringBuilder();
        sb.Append("<div class=\"m-upload\">");
        sb.Append($"<label class=\"m-upload-label\" for=\"{E(id)}\">{E(label)}</label>");
        sb.Append($"<input id=\"{E(id)}\" class=\"m-upload-input\" type=\"file\" name=\"{E(name)}\" multiple accept=\"{E(accept)}\" />");
        sb.Append("<button type=\"submit\" class=\"ant-btn ant-btn-default\">Upload</button>");
        sb.Append("<ul class=\"m-upload-list\" aria-label=\"Documents in the knowledge base\">");
        var any = false;
        foreach (var document in documents)
        {
            any = true;
            sb.Append($"<li><span class=\"m-upload-name\">{E(document.Name)}</span><span class=\"m-muted\"> {document.Chunks} passage{(document.Chunks == 1 ? "" : "s")}</span></li>");
        }
        if (!any) sb.Append("<li class=\"m-muted\">No documents yet.</li>");
        sb.Append("</ul></div>");
        return sb.ToString();
    }

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
}
