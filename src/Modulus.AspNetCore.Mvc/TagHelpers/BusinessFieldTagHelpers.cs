using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Forms;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

// Business fields of ERP forms (Odoo, SAP, Oracle): money with its currency, quantity with its unit, durations,
// number ranges, phone numbers, masked codes, one-time codes, lookups with a "Search more" dialog, the workflow
// status bar, a signature pad and rich text. The Blazor versions live in Modulus.Blazor/Components/Fields and
// render the same DOM; the formatting rules are Modulus.Ui.Abstractions.Forms, mirrored in modulus-shell.js.
// Each field posts through a hidden input holding the invariant value ("1234.5", minutes, "+44 20 …"), so the
// model binds whatever the page shows. Money, quantity, duration, range, phone and lookup run on the Alpine
// components in modulus-widgets.js; masks, one-time codes, signatures and rich text on modulus-shell.js.

internal static class BusinessFields
{
    public static string Down() => $"""<span class="m-addon-select-arrow" aria-hidden="true">{AntIcons.Span("down")}</span>""";

    public static string Hidden(string name, string? value, string xRef, string? bind) =>
        $"""<input type="hidden"{Html.Attr("name", name.Length > 0 ? name : null)} value="{Html.E(value)}" x-ref="{xRef}"{(bind is null ? "" : $" :value=\"{bind}\"")} />""";

    public static string Invalid(string? status) => status == "error" ? " aria-invalid=\"true\"" : "";

    public static string Inv(decimal? value) => NumberText.Invariant(value);
}

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

/// <summary>
/// Duration typed as timesheets take it ("1:30", "1.5", "90m", "1h 30m"), shown as h:mm, posted as whole minutes.
/// <code>&lt;app-duration-input asp-for="Minutes" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-duration-input", TagStructure = TagStructure.WithoutEndTag)]
public class DurationInputTagHelper : BoundControlTagHelper
{
    /// <summary>Text after the value; "h" by default.</summary>
    public string Suffix { get; set; } = "h";

    /// <summary>Longest duration in minutes (24 h by default).</summary>
    public int Max { get; set; } = 24 * 60;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, raw) = Bound();
        var extra = output.TakeAttributes();
        int? value = int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var m) ? Math.Min(m, Max) : null;
        var placeholder = extra.Contains(" placeholder=", StringComparison.Ordinal) ? "" : " placeholder=\"0:00\"";

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-input-affix-wrapper", SizeClass("ant-input-affix-wrapper"), StatusClass("ant-input-affix-wrapper"),
            Disabled ? "ant-input-affix-wrapper-disabled" : null, "m-field m-duration");
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusDuration({Widgets.Config(new { value, max = Max })})"));
        output.Content.SetHtmlContent(
            $"""<span class="ant-input-prefix">{AntIcons.Span("clock-circle")}</span>""" +
            $"""<input type="text" inputmode="decimal" autocomplete="off" class="ant-input m-num"{Html.Attr("id", id.Length > 0 ? id : null)}{placeholder} title="1:30, 1.5, 90m or 1h 30m" value="{Html.E(DurationText.Format(value))}" x-ref="input" @change="commit()"{Html.Attr("disabled", Disabled)}{BusinessFields.Invalid(EffectiveStatus)}{extra} />""" +
            $"""<span class="ant-input-suffix">{Html.E(Suffix)}</span>""" +
            BusinessFields.Hidden(name, value?.ToString(CultureInfo.InvariantCulture), "value", "raw"));
    }
}

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

/// <summary>
/// Lookup (Odoo many2one, SAP value help): type-ahead suggestions and a "Search more…" dialog (F4). Posts the value.
/// <code>
/// &lt;app-lookup asp-for="CustomerId" items="Model.Customers" aria-label="Customer" /&gt;
/// &lt;app-lookup asp-for="ProductId" search-href="/api/products/lookup" label="@Model.ProductName" /&gt;
/// </code>
/// The search endpoint takes <c>?q=text&amp;max=n</c> and returns <c>[{ value, label, description, fields }]</c>.
/// The field raises <c>m-lookup-change</c> with the picked item as its detail.
/// </summary>
[HtmlTargetElement("app-lookup", TagStructure = TagStructure.WithoutEndTag)]
public class LookupTagHelper : BoundControlTagHelper
{
    public IEnumerable<LookupItem>? Items { get; set; }

    /// <summary>JSON search endpoint, used instead of Items.</summary>
    public string? SearchHref { get; set; }

    /// <summary>The label of the value when it is not in Items.</summary>
    public string? Label { get; set; }

    public IEnumerable<LookupColumn>? Columns { get; set; }

    /// <summary>The dialog's title; null for "Search: {aria-label}".</summary>
    public string? Title { get; set; }

    public int MaxSuggestions { get; set; } = 8;
    public int DialogPageSize { get; set; } = 50;
    public int DialogWidth { get; set; } = 720;
    public string SearchMoreText { get; set; } = "Search more…";
    public bool AllowClear { get; set; } = true;

    [HtmlAttributeName("aria-label")] public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, raw) = Bound();
        var extra = output.TakeAttributes();
        var items = SearchHref is null ? (Items ?? []).ToList() : null;
        var value = raw ?? "";
        var label = value.Length == 0 ? "" : items?.FirstOrDefault(i => i.Value == value)?.Label ?? Label ?? value;
        var columns = (Columns ?? LookupSearch.DefaultColumns).ToList();
        var listId = $"m-lookup-{context.UniqueId}";
        var more = $"{SearchMoreText} (F4)";
        var cfg = new
        {
            value,
            label,
            items = items?.Select(i => new { v = i.Value, l = i.Label, d = i.Description, f = i.Fields }),
            href = SearchHref,
            max = MaxSuggestions,
            page = DialogPageSize,
            columns = columns.Select(c => c.Key),
            disabled = Disabled,
        };

        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-lookup", Disabled ? "m-lookup-disabled" : null);
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusLookup({Widgets.Config(cfg)})"));
        output.Attributes.SetAttribute(":class", "{ 'm-lookup-open': open }");

        var box = Html.Cx("ant-input-affix-wrapper", "m-lookup-box", SizeClass("ant-input-affix-wrapper"), StatusClass("ant-input-affix-wrapper"), Disabled ? "ant-input-affix-wrapper-disabled" : null);
        var sb = new StringBuilder();
        sb.Append(BusinessFields.Hidden(name, value, "value", "value"));
        sb.Append($$"""<span class="{{box}}"><input type="text" class="ant-input m-lookup-input"{{Html.Attr("id", id.Length > 0 ? id : null)}} autocomplete="off"{{Html.Attr("disabled", Disabled)}} role="combobox" aria-autocomplete="list" aria-expanded="false" :aria-expanded="open ? 'true' : 'false'" aria-controls="{{listId}}" :aria-activedescendant="open && active >= 0 ? '{{listId}}-' + active : null"{{Html.Attr("aria-label", AriaLabel)}}{{BusinessFields.Invalid(EffectiveStatus)}} value="{{Html.E(label)}}" x-ref="input" x-model="text" @input="typed()" @keydown="key($event)" @blur="blur()"{{extra}} />""");
        sb.Append("""<span class="ant-input-suffix">""");
        if (AllowClear && !Disabled)
            sb.Append($"""<span class="ant-input-clear-icon" role="button" aria-label="Clear" x-show="text"{(label.Length == 0 ? " style=\"display: none;\"" : "")} @mousedown.prevent @click="pick(null)">{AntIcons.Span("close-circle", IconThemeType.Fill)}</span>""");
        sb.Append($"""<button type="button" class="m-lookup-more" aria-label="{Html.E(more)}" title="{Html.E(more)}"{Html.Attr("disabled", Disabled)} tabindex="-1" @mousedown.prevent @click="openDialog()">{AntIcons.Span("search")}</button></span></span>""");

        // Suggestions.
        sb.Append($$"""<div class="ant-select-dropdown ant-select-dropdown-placement-bottomLeft m-cselect-dropdown m-lookup-dropdown" x-show="open" x-cloak style="display: none;" @mousedown.prevent>""");
        sb.Append($$"""<div class="m-cselect-list" id="{{listId}}" role="listbox" aria-label="{{Html.E(AriaLabel ?? "Suggestions")}}">""");
        sb.Append("""<div class="m-cselect-empty" x-show="!list.length" x-text="loading ? 'Searching…' : 'No matches'"></div>""");
        sb.Append($$"""<template x-for="(item, i) in list" :key="item.v"><div class="ant-select-item ant-select-item-option" :class="{ 'ant-select-item-option-selected': item.v === value, 'ant-select-item-option-active': i === active }" :id="'{{listId}}-' + i" role="option" :aria-selected="item.v === value ? 'true' : 'false'" @click="pick(item)" @mouseenter="active = i">""");
        sb.Append("""<div class="ant-select-item-option-content"><span class="m-lookup-option-label" x-text="item.l"></span><span class="m-lookup-option-desc" x-show="item.d" x-text="item.d"></span></div></div></template></div>""");
        sb.Append($"""<div class="m-cselect-footer"><button type="button" class="ant-btn ant-btn-text ant-btn-block m-cselect-add-new m-lookup-search-more" @click="openDialog()">{AntIcons.Span("search")}<span>{Html.E(SearchMoreText)}</span></button></div></div>""");

        // "Search more" dialog.
        var titleId = $"{listId}-title";
        var title = Title ?? $"Search: {AriaLabel ?? "records"}";
        sb.Append("""<div class="m-modal m-lookup-dialog" x-show="dialog" x-cloak style="display: none;" @keydown.escape="closeDialog()"><div class="ant-modal-mask"></div>""");
        sb.Append($$"""<div class="ant-modal-wrap" role="dialog" aria-modal="true" aria-labelledby="{{titleId}}" @click.self="closeDialog()"><div class="ant-modal" style="width:{{DialogWidth}}px"><div class="ant-modal-content">""");
        sb.Append($"""<button type="button" class="ant-modal-close" aria-label="Close" @click="closeDialog()"><span class="ant-modal-close-x">{AntIcons.Span("close", IconThemeType.Outline, "ant-modal-close-icon")}</span></button>""");
        sb.Append($"""<div class="ant-modal-header"><div class="ant-modal-title" id="{titleId}">{Html.E(title)}</div></div><div class="ant-modal-body">""");
        sb.Append($"""<span class="ant-input-affix-wrapper m-lookup-search"><span class="ant-input-prefix">{AntIcons.Span("search")}</span><input type="text" class="ant-input m-lookup-search-input" placeholder="Search" aria-label="Search" autocomplete="off" x-ref="search" x-model="query" @input="search(false)" @keydown="dialogKey($event)" /></span>""");
        sb.Append("""<div class="ant-table-wrapper"><div class="ant-table ant-table-small m-lookup-table"><div class="ant-table-container"><div class="ant-table-content"><table><thead class="ant-table-thead"><tr>""");
        foreach (var c in columns) sb.Append($"""<th class="ant-table-cell">{Html.E(c.Title)}</th>""");
        sb.Append($"""</tr></thead><tbody class="ant-table-tbody"><tr class="ant-table-placeholder" x-show="!rows.length"><td class="ant-table-cell" colspan="{columns.Count}" x-text="loading ? 'Searching…' : 'No matches'"></td></tr>""");
        sb.Append("""<template x-for="(item, i) in rows" :key="item.v"><tr class="ant-table-row m-lookup-row" :class="{ 'ant-table-row-selected': item.v === value, 'm-lookup-row-active': i === row }" :aria-selected="item.v === value ? 'true' : 'false'" @click="pick(item)" @mouseenter="row = i">""");
        sb.Append("""<template x-for="c in cols"><td class="ant-table-cell" x-text="cell(item, c)"></td></template></tr></template></tbody></table></div></div></div></div>""");
        sb.Append("""<div class="m-lookup-count" x-text="countText"></div></div>""");
        sb.Append("""<div class="ant-modal-footer"><button type="button" class="ant-btn ant-btn-default" @click="closeDialog()"><span>Cancel</span></button></div></div></div></div></div>""");
        output.Content.SetHtmlContent(sb.ToString());
    }
}

/// <summary>
/// Workflow status bar (Odoo statusbar): the stages as chevrons, done ones tinted, the current one filled. Each stage
/// is a radio, so it posts with no script; set <c>clickable="false"</c> when stages change only through actions.
/// <code>&lt;app-status-bar asp-for="Stage" stages="Model.Stages" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-status-bar", TagStructure = TagStructure.WithoutEndTag)]
public class StatusBarTagHelper : BoundControlTagHelper
{
    public IEnumerable<WorkflowStage> Stages { get; set; } = [];

    public bool Clickable { get; set; } = true;

    [HtmlAttributeName("aria-label")] public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, _, raw) = Bound();
        output.TakeAttributes();
        var readOnly = !Clickable || Disabled;
        var group = name.Length > 0 ? name : $"m-statusbar-{context.UniqueId}";

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-statusbar", !Clickable ? "m-statusbar-readonly" : null);
        output.Attributes.SetAttribute("role", "radiogroup");
        output.Attributes.SetAttribute("aria-label", AriaLabel ?? "Status");

        var sb = new StringBuilder();
        foreach (var stage in WorkflowStage.Visible(Stages, raw))
        {
            sb.Append($"""<label class="m-statusbar-item"><input type="radio" class="m-statusbar-radio" name="{Html.E(group)}" value="{Html.E(stage.Key)}"{Html.Attr("checked", stage.Key == raw)}{Html.Attr("disabled", readOnly)} /><span class="m-statusbar-label">{Html.E(stage.Label)}</span></label>""");
        }
        // Disabled radios do not post: keep the stage in a hidden input.
        if (readOnly && name.Length > 0) sb.Append($"""<input type="hidden" name="{Html.E(name)}" value="{Html.E(raw)}" />""");
        output.Content.SetHtmlContent(sb.ToString());
    }
}

/// <summary>
/// Signature pad: draw or type a name; posts a PNG data URL (check it with <see cref="SignatureImage.TryDecode"/>).
/// <code>&lt;app-signature-pad asp-for="Signature" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-signature-pad", TagStructure = TagStructure.WithoutEndTag)]
public class SignaturePadTagHelper : BoundControlTagHelper
{
    public int Height { get; set; } = 160;
    public string Ink { get; set; } = "#111827";
    public bool AllowTyping { get; set; } = true;
    public string Hint { get; set; } = "Sign here";
    public string ClearText { get; set; } = "Clear";
    public string TypePlaceholder { get; set; } = "Or type your name";

    [HtmlAttributeName("aria-label")] public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, _, raw) = Bound();
        output.TakeAttributes();
        var value = SignatureImage.IsValid(raw) ? raw : "";

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-signature", string.IsNullOrEmpty(value) ? null : "m-signature-signed", Disabled ? "m-signature-disabled" : null,
            EffectiveStatus is { } s ? $"m-signature-status-{s}" : null);
        output.AddStyle($"--m-signature-height:{Height.ToString(CultureInfo.InvariantCulture)}px");
        output.Attributes.SetAttribute("data-ink", Ink);
        output.Attributes.SetAttribute("data-m-signature", "");

        var sb = new StringBuilder();
        sb.Append($"""<div class="m-signature-pad"><canvas class="m-signature-canvas" role="img" aria-label="{Html.E(AriaLabel ?? "Signature")}"></canvas><span class="m-signature-hint" aria-hidden="true">{Html.E(Hint)}</span><span class="m-signature-line" aria-hidden="true"></span></div>""");
        if (!Disabled)
        {
            sb.Append("""<div class="m-signature-actions">""");
            if (AllowTyping) sb.Append($"""<input type="text" class="ant-input ant-input-sm m-signature-type" placeholder="{Html.E(TypePlaceholder)}" aria-label="{Html.E(TypePlaceholder)}" autocomplete="name" />""");
            sb.Append($"""<button type="button" class="ant-btn ant-btn-sm ant-btn-text" data-m-signature-clear>{AntIcons.Span("clear")}<span>{Html.E(ClearText)}</span></button></div>""");
        }
        sb.Append($"""<input type="hidden"{Html.Attr("name", name.Length > 0 ? name : null)} value="{Html.E(value)}" />""");
        output.Content.SetHtmlContent(sb.ToString());
    }
}

/// <summary>
/// Rich text (notes, terms and conditions): a formatting toolbar over an editable area; posts HTML. The initial value
/// is cleaned with <see cref="RichText.Sanitize(string?)"/>; clean what is posted on the server as well.
/// <code>&lt;app-rich-text asp-for="Notes" placeholder="Terms and conditions" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-rich-text", TagStructure = TagStructure.WithoutEndTag)]
public class RichTextTagHelper : BoundControlTagHelper
{
    public string? Placeholder { get; set; }

    /// <summary>Minimum editor height in pixels.</summary>
    public int Height { get; set; } = 140;

    [HtmlAttributeName("aria-label")] public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, raw) = Bound();
        output.TakeAttributes();
        var html = RichText.Sanitize(raw);

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-richtext", html.Length == 0 ? "m-richtext-empty" : null, Disabled ? "m-richtext-disabled" : null,
            EffectiveStatus is { } s ? $"m-richtext-status-{s}" : null);

        var sb = new StringBuilder();
        if (!Disabled)
        {
            sb.Append("""<div class="m-richtext-toolbar" role="toolbar" aria-label="Formatting">""");
            foreach (var group in RichTextToolbar.Groups)
            {
                sb.Append("""<span class="m-richtext-group">""");
                foreach (var b in group)
                {
                    sb.Append($"""<button type="button" class="m-richtext-btn" data-cmd="{Html.E(b.Command)}"{Html.Attr("data-arg", b.Argument)}{(b.Toggle ? " data-state=\"\"" : "")} aria-label="{Html.E(b.Label)}" title="{Html.E(b.Title)}"{(b.Toggle ? " aria-pressed=\"false\"" : "")} tabindex="-1">""");
                    sb.Append(b.Icon is not null ? AntIcons.Span(b.Icon) : $"""<span class="m-richtext-btn-text">{Html.E(b.Text)}</span>""");
                    sb.Append("</button>");
                }
                sb.Append("</span>");
            }
            sb.Append("</div>");
        }
        sb.Append($"""<div class="ant-input m-richtext-editor"{Html.Attr("id", id.Length > 0 ? id : null)} contenteditable="{(Disabled ? "false" : "true")}" role="textbox" aria-multiline="true" aria-label="{Html.E(AriaLabel ?? "Rich text")}"{BusinessFields.Invalid(EffectiveStatus)}{Html.Attr("data-placeholder", Placeholder)} style="min-height:{Height.ToString(CultureInfo.InvariantCulture)}px">{html}</div>""");
        sb.Append($"""<input type="hidden"{Html.Attr("name", name.Length > 0 ? name : null)} value="{Html.E(html)}" />""");
        output.Content.SetHtmlContent(sb.ToString());
    }
}
