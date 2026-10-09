using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Plugins;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// A PDF viewer (pdf.js), the same markup as the Blazor <c>AppPdf</c> (<see cref="PdfHtml.Viewer"/>):
/// <code>&lt;app-pdf id="contract" url="/files/contract.pdf" label="Contract" height="640px" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-pdf", TagStructure = TagStructure.WithoutEndTag)]
public class PdfTagHelper : TagHelper
{
    public string Id { get; set; } = "pdf";

    /// <summary>A same-site path or an http(s) address of the PDF.</summary>
    public string Url { get; set; } = "";

    public string? Label { get; set; }

    public string Height { get; set; } = "640px";

    /// <summary>The page the viewer opens on.</summary>
    public int Page { get; set; } = 1;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        output.TagName = null;
        output.Content.SetHtmlContent(PdfHtml.Viewer(Id, Url, Label, Height, Page, cls));
    }
}
