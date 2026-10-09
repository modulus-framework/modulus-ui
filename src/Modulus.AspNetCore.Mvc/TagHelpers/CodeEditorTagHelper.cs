using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Plugins;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// A code editor (CodeMirror 6), the same markup as the Blazor <c>AppCodeEditor</c> (<see cref="CodeEditorHtml.Editor"/>). The code
/// is the value (<c>asp-for</c>, <c>value</c>); it posts as text under the field name. <c>language</c> is one of
/// javascript, typescript, json, css or html, and is plain text when empty.
/// <code>&lt;app-code-editor asp-for="Rule" language="json" height="260px" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-code-editor", TagStructure = TagStructure.WithoutEndTag)]
public class CodeEditorTagHelper : BoundControlTagHelper
{
    /// <summary>One of <see cref="CodeEditorHtml.Languages"/>; empty is plain text.</summary>
    public string? Language { get; set; }

    public string? Label { get; set; }

    /// <summary>A read-only view of the code.</summary>
    [HtmlAttributeName("read-only")] public bool ReadOnly { get; set; }

    public string Height { get; set; } = "360px";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, raw) = Bound();
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        output.TagName = null;
        output.Content.SetHtmlContent(CodeEditorHtml.Editor(id, name, raw, Language ?? "", Label, ReadOnly || Disabled, Height, cls));
    }
}
