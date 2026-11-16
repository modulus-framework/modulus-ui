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
/// Amount with its currency (Odoo monetary, SAP amount + currency key): right-aligned, grouped, rounded to the
/// currency's minor unit; negative amounts in the error colour.
/// <code>
/// &lt;app-money-input asp-for="Total" currency="EUR" /&gt;                     (fixed currency)
/// &lt;app-money-input asp-for="Price" currency-for="Currency" decimals="4" /&gt;  (currency list, posted too)
/// </code>
/// </summary>
[HtmlTargetElement("app-money-input", TagStructure = TagStructure.WithoutEndTag)]
public class MoneyInputTagHelper : BoundControlTagHelper
{
    /// <summary>ISO code of a fixed currency, or the initial one of the list.</summary>
    public string? Currency { get; set; }

    /// <summary>Binds the currency list to a model property (posted under its name).</summary>
    [HtmlAttributeName("currency-for")] public ModelExpression? CurrencyFor { get; set; }

    /// <summary>Posts the currency list under this name.</summary>
    public string? CurrencyName { get; set; }

    /// <summary>The currency list; null uses <see cref="Ui.Abstractions.Forms.Currency.Common"/>.</summary>
    public IEnumerable<Currency>? Currencies { get; set; }

    public decimal? Min { get; set; }
    public decimal? Max { get; set; }

    /// <summary>Overrides the currency's decimals (unit prices often carry 4).</summary>
    public int? Decimals { get; set; }

    public string CurrencyAriaLabel { get; set; } = "Currency";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, raw) = Bound();
        var extra = output.TakeAttributes();
        var list = (Currencies ?? Ui.Abstractions.Forms.Currency.Common).ToList();
        var currencyName = CurrencyFor?.Name ?? CurrencyName;
        var code = CurrencyFor?.Model?.ToString() ?? Currency;
        var current = Ui.Abstractions.Forms.Currency.Find(code, list);
        code = current?.Code ?? code ?? list.FirstOrDefault()?.Code ?? "";
        var digits = Decimals ?? current?.Decimals ?? 2;
        var value = NumberText.Parse(raw) is { } v ? NumberText.Round(v, digits) : (decimal?)null;

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-input-group-wrapper", SizeClass("ant-input-group-wrapper"), StatusClass("ant-input-group-wrapper"), "m-field m-money", value < 0 ? "m-money-negative" : null);
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusNumberField({Widgets.Config(new { value, decimals = 2, @fixed = Decimals, min = Min, max = Max, digits = list.ToDictionary(c => c.Code, c => c.Decimals), unit = code })})"));
        output.Attributes.SetAttribute(":class", "{ 'm-money-negative': value !== null && value < 0 }");

        var sb = new StringBuilder("""<span class="ant-input-wrapper ant-input-group">""");
        if (currencyName is not null)
        {
            sb.Append($"""<span class="ant-input-group-addon m-addon-select"><span class="m-addon-select-text" aria-hidden="true" x-text="unit">{Html.E(code)}</span>""");
            sb.Append($"""<select name="{Html.E(currencyName)}" aria-label="{Html.E(CurrencyAriaLabel)}"{Html.Attr("disabled", Disabled)} @change="setUnit($event.target.value)">""");
            foreach (var c in list)
                sb.Append($"""<option value="{Html.E(c.Code)}"{Html.Attr("selected", c.Code == code)}>{Html.E(c.Code)} · {Html.E(c.Name)}</option>""");
            sb.Append("</select>").Append(BusinessFields.Down()).Append("</span>");
        }
        else sb.Append($"""<span class="ant-input-group-addon">{Html.E(code)}</span>""");
        var input = Html.Cx("ant-input", SizeClass("ant-input"), StatusClass("ant-input"), Disabled ? "ant-input-disabled" : null, "m-num");
        sb.Append($"""<input type="text" inputmode="decimal" autocomplete="off" class="{input}"{Html.Attr("id", id.Length > 0 ? id : null)} value="{Html.E(NumberText.Format(value, digits))}" x-ref="input" @change="commit()"{Html.Attr("disabled", Disabled)}{BusinessFields.Invalid(EffectiveStatus)}{extra} />""");
        sb.Append(BusinessFields.Hidden(name, BusinessFields.Inv(value), "value", "raw"));
        sb.Append("</span>");
        output.Content.SetHtmlContent(sb.ToString());
    }
}
