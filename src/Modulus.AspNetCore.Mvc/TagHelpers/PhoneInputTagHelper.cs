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
/// International phone number: a calling-code list and the national number, posted as one string ("+44 20 7946 0958").
/// <code>&lt;app-phone-input asp-for="Phone" default-country="GB" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-phone-input", TagStructure = TagStructure.WithoutEndTag)]
public class PhoneInputTagHelper : BoundControlTagHelper
{
    /// <summary>ISO code shown for an empty value, and preferred for shared codes (+1, +7).</summary>
    public string DefaultCountry { get; set; } = "US";

    /// <summary>The country list; null uses <see cref="Country.All"/>.</summary>
    public IEnumerable<Country>? Countries { get; set; }

    public string CountryAriaLabel { get; set; } = "Country calling code";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, raw) = Bound();
        var extra = output.TakeAttributes();
        var list = (Countries ?? Country.All).ToList();
        var (country, national) = PhoneNumber.Split(raw, list, DefaultCountry);
        var joined = PhoneNumber.Join(country, national);

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-input-group-wrapper", SizeClass("ant-input-group-wrapper"), StatusClass("ant-input-group-wrapper"), "m-field m-phone");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusPhone({Widgets.Config(new { country = country?.Code, national, home = Country.Find(DefaultCountry, list)?.Code, dials = list.ToDictionary(c => c.Code, c => c.DialCode) })})"));

        var sb = new StringBuilder("""<span class="ant-input-wrapper ant-input-group"><span class="ant-input-group-addon m-addon-select">""");
        sb.Append($"""<span class="m-addon-select-text" aria-hidden="true" x-text="prefix">{Html.E(country is null ? "+" : $"{country.Code} +{country.DialCode}")}</span>""");
        sb.Append($"""<select aria-label="{Html.E(CountryAriaLabel)}" x-ref="country" x-model="country" @change="sync()"{Html.Attr("disabled", Disabled)}>""");
        foreach (var c in list)
            sb.Append($"""<option value="{Html.E(c.Code)}" data-dial="{Html.E(c.DialCode)}"{Html.Attr("selected", c.Code == country?.Code)}>{Html.E(c.Name)} (+{Html.E(c.DialCode)})</option>""");
        sb.Append("</select>").Append(BusinessFields.Down()).Append("</span>");
        var input = Html.Cx("ant-input", SizeClass("ant-input"), StatusClass("ant-input"), Disabled ? "ant-input-disabled" : null);
        sb.Append($"""<input type="tel" autocomplete="tel-national" class="{input}"{Html.Attr("id", id.Length > 0 ? id : null)} value="{Html.E(national)}" x-ref="input" @change="commit()"{Html.Attr("disabled", Disabled)}{BusinessFields.Invalid(EffectiveStatus)}{extra} />""");
        sb.Append(BusinessFields.Hidden(name, joined, "value", "raw"));
        sb.Append("</span>");
        output.Content.SetHtmlContent(sb.ToString());
    }
}
