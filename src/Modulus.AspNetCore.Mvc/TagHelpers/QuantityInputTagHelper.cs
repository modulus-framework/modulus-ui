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
/// Quantity with its unit of measure (Odoo product_uom_qty + UoM): rounded to the unit's precision; the unit list is
/// grouped by category.
/// <code>
/// &lt;app-quantity-input asp-for="Qty" unit="kg" /&gt;
/// &lt;app-quantity-input asp-for="Qty" unit-for="Uom" /&gt;
/// </code>
/// </summary>
[HtmlTargetElement("app-quantity-input", TagStructure = TagStructure.WithoutEndTag)]
public class QuantityInputTagHelper : BoundControlTagHelper
{
    public string? Unit { get; set; }

    [HtmlAttributeName("unit-for")] public ModelExpression? UnitFor { get; set; }

    public string? UnitName { get; set; }

    /// <summary>The unit list; null uses <see cref="UnitOfMeasure.Common"/>.</summary>
    public IEnumerable<UnitOfMeasure>? Units { get; set; }

    /// <summary>Lowest quantity; 0 by default.</summary>
    public decimal? Min { get; set; } = 0;

    public decimal? Max { get; set; }

    /// <summary>Allows negative quantities (returns) by lifting the default minimum of 0.</summary>
    public bool AllowNegative { get; set; }

    public int? Decimals { get; set; }

    public string UnitAriaLabel { get; set; } = "Unit of measure";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, raw) = Bound();
        var extra = output.TakeAttributes();
        var list = (Units ?? UnitOfMeasure.Common).ToList();
        var unitName = UnitFor?.Name ?? UnitName;
        var code = UnitFor?.Model?.ToString() ?? Unit;
        var current = UnitOfMeasure.Find(code, list) ?? (code is null ? list.FirstOrDefault() : null);
        var digits = Decimals ?? current?.Decimals ?? 0;
        var value = NumberText.Parse(raw) is { } v ? NumberText.Round(v, digits) : (decimal?)null;
        var label = current?.Label ?? code ?? "";
        var min = AllowNegative ? null : Min;

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-input-group-wrapper", SizeClass("ant-input-group-wrapper"), StatusClass("ant-input-group-wrapper"), "m-field m-quantity");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusNumberField({Widgets.Config(new { value, decimals = 0, @fixed = Decimals, min, max = Max, digits = list.ToDictionary(u => u.Code, u => u.Decimals), labels = list.ToDictionary(u => u.Code, u => u.Label), unit = current?.Code ?? code })})"));

        var sb = new StringBuilder("""<span class="ant-input-wrapper ant-input-group">""");
        var input = Html.Cx("ant-input", SizeClass("ant-input"), StatusClass("ant-input"), Disabled ? "ant-input-disabled" : null, "m-num");
        sb.Append($"""<input type="text" inputmode="decimal" autocomplete="off" class="{input}"{Html.Attr("id", id.Length > 0 ? id : null)} value="{Html.E(NumberText.Format(value, digits))}" x-ref="input" @change="commit()"{Html.Attr("disabled", Disabled)}{BusinessFields.Invalid(EffectiveStatus)}{extra} />""");
        if (unitName is not null)
        {
            sb.Append($"""<span class="ant-input-group-addon m-addon-select"><span class="m-addon-select-text" aria-hidden="true" x-text="unitText">{Html.E(label)}</span>""");
            sb.Append($"""<select name="{Html.E(unitName)}" aria-label="{Html.E(UnitAriaLabel)}"{Html.Attr("disabled", Disabled)} @change="setUnit($event.target.value)">""");
            foreach (var group in list.GroupBy(u => u.Category))
            {
                if (group.Key is not null) sb.Append($"""<optgroup label="{Html.E(group.Key)}">""");
                foreach (var u in group)
                    sb.Append($"""<option value="{Html.E(u.Code)}"{Html.Attr("selected", u.Code == current?.Code)}>{Html.E(u.Label)}</option>""");
                if (group.Key is not null) sb.Append("</optgroup>");
            }
            sb.Append("</select>").Append(BusinessFields.Down()).Append("</span>");
        }
        else sb.Append($"""<span class="ant-input-group-addon">{Html.E(label)}</span>""");
        sb.Append(BusinessFields.Hidden(name, BusinessFields.Inv(value), "value", "raw"));
        sb.Append("</span>");
        output.Content.SetHtmlContent(sb.ToString());
    }
}
