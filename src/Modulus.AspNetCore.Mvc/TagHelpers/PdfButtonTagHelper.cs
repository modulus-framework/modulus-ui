using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Plugins;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// A "Download PDF" button for a form, the same markup as the Blazor <c>AppPdfButton</c> (<see cref="PdfDownloadHtml.Button"/>):
/// <code>&lt;app-pdf-button form="document-form" action="/plugins/document/pdf" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-pdf-button", TagStructure = TagStructure.WithoutEndTag)]
public class PdfButtonTagHelper : TagHelper
{
    /// <summary>The id of the form the button submits.</summary>
    public string Form { get; set; } = "";

    /// <summary>A same-site path that answers with the PDF.</summary>
    public string Action { get; set; } = "";

    public string Label { get; set; } = "Download PDF";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        output.TagName = null;
        output.Content.SetHtmlContent(PdfDownloadHtml.Button(Form, Action, Label, cls));
    }
}
