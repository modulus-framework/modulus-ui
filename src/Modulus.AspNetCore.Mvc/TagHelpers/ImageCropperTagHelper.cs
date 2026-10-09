using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Plugins;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// An image cropper (Cropper.js 2), the same markup as the Blazor <c>AppImageCropper</c> (<see cref="CropperHtml.Editor"/>). The
/// crop posts as JSON in <c>name</c>: <c>{ x, y, width, height }</c> in the picture's natural pixels.
/// <code>&lt;app-image-cropper id="avatar" src="/media/avatar.jpg" alt="Your photo" name="crop" ratio="1" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-image-cropper", TagStructure = TagStructure.WithoutEndTag)]
public class ImageCropperTagHelper : TagHelper
{
    public string Id { get; set; } = "cropper";

    /// <summary>A same-site path or an http(s) address of the picture.</summary>
    public string Src { get; set; } = "";

    public string Alt { get; set; } = "";

    /// <summary>Form field name of the hidden input that holds the crop.</summary>
    public string Name { get; set; } = "crop";

    /// <summary>The crop the picture starts with, as the JSON the script posts; empty for none.</summary>
    public string? Crop { get; set; }

    /// <summary>The starting shape: free, 1, 4/3 or 16/9.</summary>
    public string Ratio { get; set; } = "free";

    public string Height { get; set; } = "360px";

    public string? Label { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        output.TagName = null;
        output.Content.SetHtmlContent(CropperHtml.Editor(Id, Src, Alt, Name, Label, Ratio, Height, cls, CropperHtml.ParseBox(Crop)));
    }
}
