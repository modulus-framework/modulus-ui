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
/// From–to number range in one box (SAP select-options, "between" filters); an inverted range shows as an error.
/// <code>&lt;app-number-range from-for="MinAmount" to-for="MaxAmount" suffix="USD" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-number-range", TagStructure = TagStructure.WithoutEndTag)]
public class NumberRangeTagHelper : TagHelper
{
    [HtmlAttributeName("from-for")] public ModelExpression? FromFor { get; set; }
    [HtmlAttributeName("to-for")] public ModelExpression? ToFor { get; set; }
    public string? FromName { get; set; }
    public string? ToName { get; set; }
    public decimal? From { get; set; }
    public decimal? To { get; set; }
    public int Decimals { get; set; } = 2;
    public string FromPlaceholder { get; set; } = "Min";
    public string ToPlaceholder { get; set; } = "Max";

    /// <summary>Text after the numbers, such as a currency code or unit.</summary>
    public string? Suffix { get; set; }

    /// <summary>small | middle | large</summary>
    public string? Size { get; set; }

    /// <summary>error | warning; an inverted range is always an error.</summary>
    public string? Status { get; set; }

    public bool Disabled { get; set; }

    [HtmlAttributeName("aria-label")] public string? AriaLabel { get; set; }

    [ViewContext, HtmlAttributeNotBound] public ViewContext? ViewContext { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (fromName, fromId, fromRaw) = Html.Bind(FromFor, FromName, From is { } f ? NumberText.Invariant(f) : null);
        var (toName, _, toRaw) = Html.Bind(ToFor, ToName, To is { } t ? NumberText.Invariant(t) : null);
        var from = NumberText.Parse(fromRaw) is { } a ? NumberText.Round(a, Decimals) : (decimal?)null;
        var to = NumberText.Parse(toRaw) is { } b ? NumberText.Round(b, Decimals) : (decimal?)null;
        var invalid = from > to;
        var status = invalid ? "error" : Status ?? (Html.HasError(ViewContext, FromFor) || Html.HasError(ViewContext, ToFor) ? "error" : null);
        var label = AriaLabel ?? "Range";

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-picker ant-picker-range", Size == "small" ? "ant-picker-small" : Size == "large" ? "ant-picker-large" : null,
            status is null ? null : $"ant-picker-status-{status}", Disabled ? "ant-picker-disabled" : null, "m-field m-number-range");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusNumberRange({Widgets.Config(new { from, to, decimals = Decimals })})"));
        output.Attributes.SetAttribute(":class", "{ 'ant-picker-status-error': invalid }");

        string Input(string which, string? id, string placeholder, decimal? value, string aria) =>
            $"""<div class="ant-picker-input"><input type="text" inputmode="decimal" autocomplete="off" class="m-num"{Html.Attr("id", id)} placeholder="{Html.E(placeholder)}" aria-label="{Html.E(aria)}"{(invalid ? " aria-invalid=\"true\"" : "")} :aria-invalid="invalid ? 'true' : null" value="{Html.E(NumberText.Format(value, Decimals))}" x-ref="{which}" @change="commit('{which}')"{Html.Attr("disabled", Disabled)} /></div>""";

        output.Content.SetHtmlContent(
            Input("from", fromId.Length > 0 ? fromId : null, FromPlaceholder, from, $"{label} from") +
            $"""<div class="ant-picker-range-separator"><span class="ant-picker-separator" aria-hidden="true">{AntIcons.Span("swap-right")}</span></div>""" +
            Input("to", null, ToPlaceholder, to, $"{label} to") +
            (Suffix is null ? "" : $"""<span class="ant-picker-suffix">{Html.E(Suffix)}</span>""") +
            BusinessFields.Hidden(fromName, BusinessFields.Inv(from), "fromValue", "raw(from)") +
            BusinessFields.Hidden(toName, BusinessFields.Inv(to), "toValue", "raw(to)"));
    }
}
