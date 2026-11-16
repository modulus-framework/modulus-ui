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
/// One-time code (Ant Design 5 Input.OTP): one box per character, posted joined.
/// <code>&lt;app-otp-input asp-for="Code" length="6" separator="3" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-otp-input", TagStructure = TagStructure.WithoutEndTag)]
public class OtpInputTagHelper : BoundControlTagHelper
{
    public int Length { get; set; } = 6;

    /// <summary>Digits only (default); false takes letters too (upper-cased).</summary>
    public bool Numeric { get; set; } = true;

    /// <summary>Hides the characters, like a password.</summary>
    public bool Mask { get; set; }

    /// <summary>Draws a dash before this box (3 splits 6 boxes 3–3); 0 for none.</summary>
    public int Separator { get; set; }

    [HtmlAttributeName("aria-label")] public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, raw) = Bound();
        output.TakeAttributes();
        var value = raw ?? "";
        var box = Html.Cx("ant-input", "ant-otp-input", SizeClass("ant-input"), StatusClass("ant-input"), Disabled ? "ant-input-disabled" : null);

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-otp", Size == "small" ? "ant-otp-sm" : Size == "large" ? "ant-otp-lg" : null);
        output.Attributes.SetAttribute("role", "group");
        output.Attributes.SetAttribute("aria-label", AriaLabel ?? "Verification code");
        output.Attributes.SetAttribute("data-m-otp", Numeric ? "numeric" : "text");

        var sb = new StringBuilder();
        for (var i = 0; i < Length; i++)
        {
            if (Separator > 0 && i == Separator) sb.Append("""<span class="ant-otp-separator" aria-hidden="true">–</span>""");
            var c = i < value.Length ? value[i].ToString() : "";
            sb.Append($"""<input type="{(Mask ? "password" : "text")}" class="{box}"{(i == 0 && id.Length > 0 ? $" id=\"{Html.E(id)}\"" : "")} inputmode="{(Numeric ? "numeric" : "text")}" autocomplete="{(i == 0 ? "one-time-code" : "off")}" aria-label="Character {i + 1} of {Length}"{Html.Attr("disabled", Disabled)} value="{Html.E(c)}" />""");
        }
        sb.Append($"""<input type="hidden"{Html.Attr("name", name.Length > 0 ? name : null)} value="{Html.E(value)}" />""");
        output.Content.SetHtmlContent(sb.ToString());
    }
}
