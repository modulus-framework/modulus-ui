using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Barcode;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// A barcode as inline SVG (<see cref="BarcodeSvg"/>), the same markup as the Blazor <c>AppBarcode</c>:
/// <code>&lt;app-barcode value="5901234123457" kind="Ean13" height="70" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-barcode", TagStructure = TagStructure.WithoutEndTag)]
public class BarcodeTagHelper : TagHelper
{
    /// <summary>The value to draw.</summary>
    public string Value { get; set; } = "";

    /// <summary><c>Code128</c> (the default) or <c>Ean13</c>.</summary>
    public BarcodeKind Kind { get; set; } = BarcodeKind.Code128;

    public int Height { get; set; } = 80;

    [HtmlAttributeName("module-width")] public int ModuleWidth { get; set; } = 2;

    [HtmlAttributeName("show-text")] public bool ShowText { get; set; } = true;

    public string Color { get; set; } = "#000";

    public string Background { get; set; } = "#fff";

    [HtmlAttributeName("aria-label")] public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        output.TagName = null;
        output.Content.SetHtmlContent(BarcodeSvg.Render(Kind, Value, Height, ModuleWidth, ShowText, Color, Background, AriaLabel, cls));
    }
}
