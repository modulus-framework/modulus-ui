using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Plugins;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// A drop area on Uppy, the same markup as the Blazor <c>AppUploader</c> (<see cref="UploaderHtml.Dashboard"/>). Files upload as
/// they are dropped, one POST each to <c>action</c>; <c>max-size</c> is in bytes.
/// <code>&lt;app-uploader id="docs" action="/files/upload" accept=".pdf,image/*" max-size="5242880" max-count="6" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-uploader", TagStructure = TagStructure.WithoutEndTag)]
public class UploaderTagHelper : TagHelper
{
    public string Id { get; set; } = "uploader";

    /// <summary>Where each file is posted: a same-site path or an http(s) address.</summary>
    public string Action { get; set; } = "";

    /// <summary>Form field name of each file.</summary>
    public string Name { get; set; } = "file";

    /// <summary>Extensions (.pdf) and MIME patterns (image/*), comma separated.</summary>
    public string? Accept { get; set; }

    /// <summary>The largest file in bytes; 0 is no limit.</summary>
    public long MaxSize { get; set; }

    /// <summary>The most files the drop area takes; 0 is no limit.</summary>
    public int MaxCount { get; set; }

    public string? Note { get; set; }

    public string? Label { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        output.TagName = null;
        output.Content.SetHtmlContent(UploaderHtml.Dashboard(Id, Action, Name, Accept, MaxSize, MaxCount, Note, Label, cls));
    }
}
