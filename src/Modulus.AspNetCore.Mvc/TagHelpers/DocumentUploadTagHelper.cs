using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Assistant;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// The upload field of a knowledge base: a file input for the accepted types, an upload button, and the documents in it. The
/// host puts it in a form with <c>enctype="multipart/form-data"</c> and <c>asp-antiforgery</c>; the handler reads the files from
/// the field named <c>name</c>. The same field is <c>AppDocumentUpload</c> in Blazor.
/// </summary>
/// <code>
/// &lt;form method="post" action="/apps/assistant/upload" enctype="multipart/form-data" asp-antiforgery="true"&gt;
///   &lt;app-document-upload documents="Model.Documents" /&gt;
/// &lt;/form&gt;
/// </code>
[HtmlTargetElement("app-document-upload", TagStructure = TagStructure.WithoutEndTag)]
public class DocumentUploadTagHelper : TagHelper
{
    /// <summary>The field name the files are posted in.</summary>
    public string Name { get; set; } = "documents";

    /// <summary>The documents already in the knowledge base.</summary>
    public IEnumerable<KnowledgeDocument>? Documents { get; set; }

    /// <summary>The label above the field.</summary>
    public string Label { get; set; } = "Add documents to the knowledge base";

    /// <summary>The file types the input accepts.</summary>
    public string Accept { get; set; } = DocumentText.Accept;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "section";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", "m-upload-section");
        output.Content.SetHtmlContent(UploadHtml.Field(Name, Documents ?? [], Label, Accept));
    }
}
