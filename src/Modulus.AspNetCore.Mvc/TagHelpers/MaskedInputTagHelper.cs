using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Forms;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Fixed-format input (tax ids, postal codes, IBANs): <c>9</c> digit, <c>A</c> letter, <c>*</c> either, <c>\</c> escapes.
/// modulus-shell.js fits the text to the mask as it is typed.
/// <code>&lt;app-masked-input asp-for="TaxId" mask="99-9999999" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-masked-input", TagStructure = TagStructure.WithoutEndTag)]
public class MaskedInputTagHelper : BoundControlTagHelper
{
    public string Mask { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, raw) = Bound();
        var hasPlaceholder = output.Attributes.ContainsName("placeholder");
        var hasId = output.Attributes.ContainsName("id");
        var slots = InputMask.Placeholder(Mask);
        var numeric = !Mask.Replace("\\9", "").Any(c => c is 'A' or '*');

        output.TagName = "input";
        output.TagMode = TagMode.SelfClosing;
        output.AddClasses("ant-input", SizeClass("ant-input"), StatusClass("ant-input"), Disabled ? "ant-input-disabled" : null, "m-masked");
        output.Attributes.SetAttribute("type", "text");
        if (name.Length > 0) output.Attributes.SetAttribute("name", name);
        if (!hasId && id.Length > 0) output.Attributes.SetAttribute("id", id);
        output.Attributes.SetAttribute("data-m-mask", Mask);
        if (!hasPlaceholder) output.Attributes.SetAttribute("placeholder", slots);
        output.Attributes.SetAttribute("maxlength", slots.Length.ToString(CultureInfo.InvariantCulture));
        output.Attributes.SetAttribute("spellcheck", "false");
        output.Attributes.SetAttribute("autocomplete", "off");
        output.Attributes.SetAttribute("inputmode", numeric ? "numeric" : "text");
        output.Attributes.SetAttribute("value", InputMask.Apply(Mask, raw));
        if (Disabled) output.Attributes.SetAttribute("disabled", "disabled");
        if (EffectiveStatus == "error") output.Attributes.SetAttribute("aria-invalid", "true");
    }
}
