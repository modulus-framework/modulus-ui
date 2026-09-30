using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

// Data-entry controls. Every control renders a real form element (input, textarea, hidden input),
// so forms post normally, work with htmx (hx-post / hx-include) and bind back to the model.
// Unbound attributes (placeholder, maxlength, required, autocomplete, hx-*, x-*, aria-*) go on the
// real control; class and style stay on the outer element.

/// <summary>Base for controls bound with <c>asp-for</c> or a plain <c>name</c>.</summary>
public abstract class BoundControlTagHelper : TagHelper
{
    [HtmlAttributeName("asp-for")] public ModelExpression? For { get; set; }

    public string? Name { get; set; }
    public string? Value { get; set; }

    /// <summary>small | middle | large</summary>
    public string? Size { get; set; }

    /// <summary>error | warning. Defaults to error when ModelState has errors for <c>asp-for</c>.</summary>
    public string? Status { get; set; }

    public bool Disabled { get; set; }

    [ViewContext, HtmlAttributeNotBound] public ViewContext? ViewContext { get; set; }

    protected string? EffectiveStatus => Status ?? (Html.HasError(ViewContext, For) ? "error" : null);

    protected string? SizeClass(string prefix) => Size switch { "small" => $"{prefix}-sm", "large" => $"{prefix}-lg", _ => null };

    protected string? StatusClass(string prefix) => EffectiveStatus is { } s ? $"{prefix}-status-{s}" : null;

    protected (string Name, string Id, string? Value) Bound() => Html.Bind(For, Name, Value);

    /// <summary>Removes and returns the author's class and style, for controls rendered without a wrapper.</summary>
    protected static (string? Class, string? Style) TakeClassAndStyle(TagHelperOutput output)
    {
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        var style = output.Attributes.TryGetAttribute("style", out var st) ? st.Value?.ToString() : null;
        output.Attributes.RemoveAll("class");
        output.Attributes.RemoveAll("style");
        return (cls, style);
    }

    /// <summary>name, id, value, disabled and the author's pass-through attributes for the inner control.</summary>
    protected string ControlAttributes(TagHelperOutput output, bool includeValue = true)
    {
        var (name, id, value) = Bound();
        var passThrough = output.TakeAttributes();
        var hasId = passThrough.Contains(" id=\"", StringComparison.Ordinal);
        return Html.Attr("name", name.Length > 0 ? name : null) + (hasId || id.Length == 0 ? "" : Html.Attr("id", id)) +
               (includeValue ? Html.Attr("value", value) : "") + Html.Attr("disabled", Disabled) +
               (EffectiveStatus == "error" ? " aria-invalid=\"true\"" : "") + passThrough;
    }
}

/// <summary>
/// Text input with Ant Design's affixes and addons.
/// <code>
/// &lt;app-input asp-for="Name" placeholder="Name" prefix-icon="user" allow-clear /&gt;
/// &lt;app-input asp-for="Website" addon-before="https://" addon-after=".com" /&gt;
/// &lt;app-input asp-for="Password" type="password" /&gt;              (with show/hide toggle)
/// &lt;app-input name="city" items="Model.Cities" /&gt;                  (AutoComplete via &lt;datalist&gt;)
/// </code>
/// </summary>
[HtmlTargetElement("app-input", TagStructure = TagStructure.WithoutEndTag)]
public class InputTagHelper : BoundControlTagHelper
{
    /// <summary>text | password | email | url | tel | number | search</summary>
    public string Type { get; set; } = "text";

    public string? PrefixIcon { get; set; }
    public string? SuffixIcon { get; set; }
    public string? Prefix { get; set; }
    public string? Suffix { get; set; }
    public string? AddonBefore { get; set; }
    public string? AddonAfter { get; set; }

    /// <summary>Shows a clear button while the field has a value.</summary>
    public bool AllowClear { get; set; }

    /// <summary>Borderless variant.</summary>
    public bool Bordered { get; set; } = true;

    /// <summary>Suggestions shown as the user types (Ant Design AutoComplete, via a native datalist).</summary>
    public IEnumerable<string>? Items { get; set; }

    private const string ClearScript =
        "value = ''; $refs.input.value = ''; $refs.input.dispatchEvent(new Event('input', { bubbles: true })); $refs.input.focus()";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var addon = AddonBefore is not null || AddonAfter is not null;
        // Without addons the control itself is the outermost element and receives the author's class/style.
        var (outerClass, outerStyle) = addon ? (null, null) : TakeClassAndStyle(output);
        var password = Type == "password";
        var datalist = Items is null ? null : $"{context.UniqueId}-list";
        var attrs = ControlAttributes(output) + (datalist is null ? "" : Html.Attr("list", datalist));
        var affix = password || AllowClear || PrefixIcon is not null || SuffixIcon is not null || Prefix is not null || Suffix is not null;

        string control;
        if (!affix)
        {
            control = $"""<input type="{Html.E(Type)}" class="{Html.Cx("ant-input", SizeClass("ant-input"), StatusClass("ant-input"), Bordered ? null : "ant-input-borderless", Disabled ? "ant-input-disabled" : null, outerClass)}"{Html.Attr("style", outerStyle)}{attrs} />""";
        }
        else
        {
            var prefix = PrefixIcon is not null ? AntIcons.Span(PrefixIcon) : Prefix is not null ? Html.E(Prefix) : null;
            var suffix = new StringBuilder();
            if (AllowClear)
                suffix.Append($"""<button type="button" class="ant-input-clear-icon m-input-clear" aria-label="Clear" x-show="value" @click="{ClearScript}" tabindex="-1">{AntIcons.Svg("close-circle", IconThemeType.Fill)}</button>""");
            if (password)
                suffix.Append($"""<button type="button" class="ant-input-password-icon m-input-reveal" :aria-label="reveal ? 'Hide password' : 'Show password'" aria-label="Show password" @click="reveal = !reveal"><span x-show="!reveal">{AntIcons.Span("eye-invisible")}</span><span x-show="reveal" x-cloak>{AntIcons.Span("eye")}</span></button>""");
            if (SuffixIcon is not null) suffix.Append(AntIcons.Span(SuffixIcon));
            else if (Suffix is not null) suffix.Append(Html.E(Suffix));

            var typeBinding = password ? " :type=\"reveal ? 'text' : 'password'\"" : "";
            var model = AllowClear ? " @input=\"value = $el.value\"" : "";
            var data = AllowClear || password ? " x-data=\"{ reveal: false, value: " + (string.IsNullOrEmpty(Bound().Value) ? "''" : "true") + " }\" " : " ";
            control =
                $"""<span class="{Html.Cx("ant-input-affix-wrapper", SizeClass("ant-input-affix-wrapper"), StatusClass("ant-input-affix-wrapper"), password ? "ant-input-password" : null, Bordered ? null : "ant-input-affix-wrapper-borderless", Disabled ? "ant-input-affix-wrapper-disabled" : null, outerClass)}"{Html.Attr("style", outerStyle)}{data.TrimEnd()}>""" +
                (prefix is null ? "" : $"""<span class="ant-input-prefix">{prefix}</span>""") +
                $"""<input type="{Html.E(Type)}" class="ant-input" x-ref="input"{typeBinding}{model}{attrs} />""" +
                (suffix.Length == 0 ? "" : $"""<span class="ant-input-suffix">{suffix}</span>""") + "</span>";
        }

        if (addon)
        {
            control =
                $"""<span class="ant-input-wrapper ant-input-group">""" +
                (AddonBefore is null ? "" : $"""<span class="ant-input-group-addon">{Html.E(AddonBefore)}</span>""") + control +
                (AddonAfter is null ? "" : $"""<span class="ant-input-group-addon">{Html.E(AddonAfter)}</span>""") + "</span>";
            output.TagName = "span";
            output.AddClasses("ant-input-group-wrapper", SizeClass("ant-input-group-wrapper"), StatusClass("ant-input-group-wrapper"));
        }
        else
        {
            output.TagName = null;
        }

        if (datalist is not null)
            control += $"""<datalist id="{datalist}">{string.Concat(Items!.Select(i => $"<option value=\"{Html.E(i)}\"></option>"))}</datalist>""";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Content.SetHtmlContent(control);
    }
}

/// <summary>
/// Search box with a search button: <c>&lt;app-input-search name="q" placeholder="Search" enter-button /&gt;</c>.
/// The button submits the enclosing form (or triggers htmx on the form).
/// </summary>
[HtmlTargetElement("app-input-search", TagStructure = TagStructure.WithoutEndTag)]
public class InputSearchTagHelper : BoundControlTagHelper
{
    /// <summary>Primary search button (true), a button with this text, or an icon-only default button (false).</summary>
    public string? EnterButton { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var attrs = ControlAttributes(output);
        var primary = EnterButton is not null && EnterButton != "false";
        var text = primary && EnterButton is not ("" or "true") ? $"<span>{Html.E(EnterButton)}</span>" : null;
        var button = $"""<button type="submit" aria-label="Search" class="{Html.Cx("ant-btn", primary ? "ant-btn-primary" : "ant-btn-default", SizeClass("ant-btn"), text is null ? "ant-btn-icon-only" : null, "ant-input-search-button")}">{AntIcons.Span("search")}{text}</button>""";
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-input-group-wrapper", "ant-input-search", primary ? "ant-input-search-enter-button" : null, SizeClass("ant-input-search"), SizeClass("ant-input-group-wrapper"));
        output.Content.SetHtmlContent(
            $"""<span class="ant-input-wrapper ant-input-group"><input type="search" class="{Html.Cx("ant-input", SizeClass("ant-input"), StatusClass("ant-input"))}"{attrs} /><span class="ant-input-group-addon">{button}</span></span>""");
    }
}

/// <summary>Multi-line input: <c>&lt;app-textarea asp-for="Notes" rows="4" maxlength="200" show-count /&gt;</c>.</summary>
[HtmlTargetElement("app-textarea", TagStructure = TagStructure.WithoutEndTag)]
public class TextAreaTagHelper : BoundControlTagHelper
{
    public int Rows { get; set; } = 3;

    /// <summary>Shows "n / maxlength" under the field.</summary>
    public bool ShowCount { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var value = Bound().Value ?? "";
        var max = output.Attributes.TryGetAttribute("maxlength", out var m) ? m.Value?.ToString() : null;
        var attrs = ControlAttributes(output, includeValue: false);
        var cls = Html.Cx("ant-input", SizeClass("ant-input"), StatusClass("ant-input"), Disabled ? "ant-input-disabled" : null);
        if (!ShowCount)
        {
            var (outerClass, outerStyle) = TakeClassAndStyle(output);
            output.TagName = null;
            output.Content.SetHtmlContent($"""<textarea class="{Html.Cx(cls, outerClass)}"{Html.Attr("style", outerStyle)} rows="{Rows}"{attrs}>{Html.E(value)}</textarea>""");
            return;
        }
        // Same structure as Blazor's <TextArea ShowCount>: the textarea sits in an affix wrapper with the
        // height antd's autosize computes (rows × line height + padding), and inside a form item the counter
        // hangs into the help row instead of adding height.
        var suffix = max is null ? "" : $" / {max}";
        var height = (Rows * 22.001 + 14).ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-input-textarea", "ant-input-textarea-show-count", context.Items.ContainsKey(FormItemTagHelper.ContextKey) ? "ant-input-textarea-in-form-item" : null);
        output.Attributes.SetAttribute("x-data", $"{{ n: {value.Length} }}");
        output.Attributes.SetAttribute("data-count", $"{value.Length}{suffix}");
        output.Attributes.SetAttribute(":data-count", $"n + '{suffix}'");
        output.Content.SetHtmlContent($"""<span class="ant-input-affix-wrapper"><textarea class="{cls}" style="height:{height}px;overflow-y:auto;overflow-x:hidden" rows="{Rows}" @input="n = $el.value.length"{attrs}>{Html.E(value)}</textarea></span><span class="ant-input-textarea-suffix"></span>""");
    }
}

/// <summary>Number input with stepper handles: <c>&lt;app-input-number asp-for="Seats" min="1" max="500" /&gt;</c>.</summary>
[HtmlTargetElement("app-input-number", TagStructure = TagStructure.WithoutEndTag)]
public class InputNumberTagHelper : BoundControlTagHelper
{
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
    public decimal Step { get; set; } = 1;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var attrs = ControlAttributes(output);
        const string bump = "$refs.input.dispatchEvent(new Event('input', { bubbles: true })); $refs.input.dispatchEvent(new Event('change', { bubbles: true }))";
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-input-number", SizeClass("ant-input-number"), StatusClass("ant-input-number"), Disabled ? "ant-input-number-disabled" : null);
        output.Attributes.SetAttribute("x-data", "");
        output.Content.SetHtmlContent(
            """<div class="ant-input-number-handler-wrap" aria-hidden="true">""" +
            $"""<span unselectable="on" role="button" tabindex="-1" aria-label="Increase value" class="ant-input-number-handler ant-input-number-handler-up" @mousedown.prevent @click="$refs.input.stepUp(); {bump}">{AntIcons.Span("up", IconThemeType.Outline, "ant-input-number-handler-up-inner")}</span>""" +
            $"""<span unselectable="on" role="button" tabindex="-1" aria-label="Decrease value" class="ant-input-number-handler ant-input-number-handler-down" @mousedown.prevent @click="$refs.input.stepDown(); {bump}">{AntIcons.Span("down", IconThemeType.Outline, "ant-input-number-handler-down-inner")}</span>""" +
            "</div>" +
            $"""<div class="ant-input-number-input-wrap"><input type="number" role="spinbutton" inputmode="decimal" autocomplete="off" class="ant-input-number-input" x-ref="input"{Html.Attr("min", Min?.ToString(CultureInfo.InvariantCulture))}{Html.Attr("max", Max?.ToString(CultureInfo.InvariantCulture))} step="{Step.ToString(CultureInfo.InvariantCulture)}"{attrs} /></div>""");
    }
}

/// <summary>
/// Toggle switch that posts <c>true</c>/<c>false</c> (binds to bool):
/// <c>&lt;app-switch asp-for="Alerts" checked-text="On" unchecked-text="Off" /&gt;</c>.
/// It raises a bubbling <c>change</c> event, so <c>hx-post="…" hx-trigger="change" hx-include="find input"</c> saves instantly.
/// </summary>
[HtmlTargetElement("app-switch", TagStructure = TagStructure.WithoutEndTag)]
public class SwitchTagHelper : BoundControlTagHelper
{
    public bool Checked { get; set; }
    public string? CheckedText { get; set; }
    public string? UncheckedText { get; set; }

    /// <summary>Accessible name when no visible label points at the switch.</summary>
    public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, _) = Bound();
        var on = For?.Model is true || Checked;
        output.TagName = "button";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("type", "button");
        output.Attributes.SetAttribute("role", "switch");
        if (id.Length > 0 && !output.Attributes.ContainsName("id")) output.Attributes.SetAttribute("id", id);
        if (AriaLabel is not null) output.Attributes.SetAttribute("aria-label", AriaLabel);
        if (Disabled) output.Attributes.SetAttribute("disabled", "disabled");
        output.Attributes.SetAttribute("aria-checked", on ? "true" : "false");
        output.Attributes.SetAttribute("x-data", $"{{ on: {(on ? "true" : "false")} }}");
        output.Attributes.SetAttribute(":aria-checked", "on");
        output.Attributes.SetAttribute(":class", "on && 'ant-switch-checked'");
        output.Attributes.SetAttribute("@click", "on = !on; $nextTick(() => $el.dispatchEvent(new Event('change', { bubbles: true })))");
        output.AddClasses("ant-switch", Size == "small" ? "ant-switch-small" : null, on ? "ant-switch-checked" : null, Disabled ? "ant-switch-disabled" : null);
        var inner = CheckedText is null && UncheckedText is null ? "" :
            $"""<span x-show="on"{(on ? "" : " style=\"display:none\"")}>{Html.E(CheckedText)}</span><span x-show="!on"{(on ? " style=\"display:none\"" : "")}>{Html.E(UncheckedText)}</span>""";
        output.Content.SetHtmlContent(
            $"""<div class="ant-switch-handle"></div><span class="ant-switch-inner">{inner}</span>""" +
            (name.Length == 0 ? "" : $"""<input type="hidden" name="{Html.E(name)}" value="{(on ? "true" : "false")}" :value="on" />"""));
    }
}

/// <summary>
/// Date / time picker with Ant Design's own popup, as AntD Blazor's DatePicker renders it:
/// <c>&lt;app-date-picker asp-for="Start" /&gt;</c>, <c>picker="week|month|quarter|year|time"</c>, <c>show-time</c>.
/// The text input shows the value in AntD's format (<c>2026-09-28</c>, <c>2026-09-28 10:00:00</c>, <c>2026-39Week</c>,
/// <c>2026-09</c>, <c>2026-Q3</c>, <c>2026</c>); a hidden input posts <see cref="PickerText.Canonical"/>. Disabled dates:
/// <c>min</c> / <c>max</c> (yyyy-MM-dd) and <c>disabled-days="sat,sun"</c>. Runs on <c>modulusPicker</c> (modulus-widgets.js).
/// </summary>
[HtmlTargetElement("app-date-picker", TagStructure = TagStructure.WithoutEndTag)]
public class DatePickerTagHelper : BoundControlTagHelper
{
    /// <summary>date | week | month | quarter | year | time</summary>
    public string Picker { get; set; } = "date";

    /// <summary>Date and time: a date panel with hour, minute and second columns, and Now / Ok.</summary>
    public bool ShowTime { get; set; }

    /// <summary>Time columns for <c>picker="time"</c> and <c>show-time</c>: <c>HH:mm:ss</c> (default) or <c>HH:mm</c>.</summary>
    public string Format { get; set; } = "HH:mm:ss";

    /// <summary>Weekdays that can't be picked, as names or numbers (<c>"sat,sun"</c> or <c>"6,0"</c>), as AntD's <c>DisabledDate</c>.</summary>
    public string? DisabledDays { get; set; }

    /// <summary>Shown when a disabled date is typed.</summary>
    public string DisabledDaysMessage { get; set; } = "This date can't be picked.";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, value) = Bound();
        var picker = Picker is "week" or "month" or "quarter" or "year" or "time" ? Picker : "date";
        var date = PickerText.TryParse(value, out var d) ? d : (DateTime?)null;
        var (min, max) = (TakeAttribute(output, "min"), TakeAttribute(output, "max"));
        var placeholder = TakeAttribute(output, "placeholder") ?? PickerText.Placeholder(picker);
        var attrs = output.TakeAttributes();
        var cfg = Widgets.Config(new
        {
            picker, showTime = ShowTime, format = Format,
            values = new[] { date is { } v ? PickerText.Canonical(v, picker, ShowTime, Format) : "" },
            min, max, days = PickerText.Days(DisabledDays), message = DisabledDaysMessage, disabled = Disabled,
        });
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-picker", Size switch { "small" => "ant-picker-small", "large" => "ant-picker-large", _ => null }, StatusClass("ant-picker"), Disabled ? "ant-picker-disabled" : null, "m-picker");
        PickerText.Root(output, cfg);
        var text = date is { } t ? PickerText.Display(t, picker, ShowTime, Format) : "";
        output.Content.SetHtmlContent(
            """<div class="ant-picker-input">""" +
            PickerText.TextInput(0, placeholder, text, id.Length > 0 && !attrs.Contains(" id=\"", StringComparison.Ordinal) ? id : null, Disabled, EffectiveStatus == "error", attrs) +
            $"""<span class="ant-picker-suffix" aria-hidden="true">{AntIcons.Span(picker == "time" ? "clock-circle" : "calendar")}</span>""" +
            PickerText.Clear("values[0]", Disabled) + "</div>" +
            PickerText.Hidden(name, date is { } h ? PickerText.Canonical(h, picker, ShowTime, Format) : "", 0) +
            PickerText.Drop);
    }

    internal static string? TakeAttribute(TagHelperOutput output, string name)
    {
        if (!output.Attributes.TryGetAttribute(name, out var a)) return null;
        output.Attributes.Remove(a);
        return a.Value?.ToString();
    }
}

/// <summary>
/// Date range with Ant Design's two-month popup: <c>&lt;app-range-picker asp-for="Period" /&gt;</c> binds a two-item
/// array/list, or use <c>name-from</c> / <c>name-to</c> with <c>value-from</c> / <c>value-to</c>. <c>presets</c> fills the
/// popup footer with preset tags (AntD's <c>Ranges</c>); <c>min</c>, <c>max</c> and <c>disabled-days</c> disable dates.
/// </summary>
[HtmlTargetElement("app-range-picker", TagStructure = TagStructure.WithoutEndTag)]
public class RangePickerTagHelper : BoundControlTagHelper
{
    public string? NameFrom { get; set; }
    public string? NameTo { get; set; }
    public string? ValueFrom { get; set; }
    public string? ValueTo { get; set; }

    /// <summary>Dates with times: a date panel with time columns for the end being edited, and Now / Ok.</summary>
    public bool ShowTime { get; set; }

    /// <inheritdoc cref="DatePickerTagHelper.Format"/>
    public string Format { get; set; } = "HH:mm:ss";

    public string PlaceholderFrom { get; set; } = "Start date";
    public string PlaceholderTo { get; set; } = "End date";

    /// <summary>Preset ranges, shown as tags in the popup footer.</summary>
    public IEnumerable<Modulus.Ui.Abstractions.Forms.DateRangePreset>? Presets { get; set; }

    /// <inheritdoc cref="DatePickerTagHelper.DisabledDays"/>
    public string? DisabledDays { get; set; }

    /// <inheritdoc cref="DatePickerTagHelper.DisabledDaysMessage"/>
    public string DisabledDaysMessage { get; set; } = "This date can't be picked.";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, id, _) = Bound();
        var values = For?.Model is IEnumerable list and not string ? list.Cast<object?>().Select(Html.Format).ToArray() : [];
        var raw = new[] { ValueFrom ?? values.ElementAtOrDefault(0), ValueTo ?? values.ElementAtOrDefault(1) };
        var dates = raw.Select(r => PickerText.TryParse(r, out var d) ? d : (DateTime?)null).ToArray();
        var (min, max) = (DatePickerTagHelper.TakeAttribute(output, "min"), DatePickerTagHelper.TakeAttribute(output, "max"));
        var attrs = output.TakeAttributes();
        const string Fmt = "yyyy-MM-dd";
        var cfg = Widgets.Config(new
        {
            picker = "date", showTime = ShowTime, format = Format, range = true,
            values = dates.Select(d => d is { } v ? PickerText.Canonical(v, "date", ShowTime, Format) : "").ToArray(),
            min, max, days = PickerText.Days(DisabledDays), message = DisabledDaysMessage, disabled = Disabled,
            presets = (Presets ?? []).Select(p => new
            {
                label = p.Label,
                from = p.From.ToString(Fmt, CultureInfo.InvariantCulture) + (ShowTime ? "T00:00:00" : ""),
                to = p.To.ToString(Fmt, CultureInfo.InvariantCulture) + (ShowTime ? "T23:59:59" : ""),
            }).ToArray(),
        });
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-picker", "ant-picker-range", Size switch { "small" => "ant-picker-small", "large" => "ant-picker-large", _ => null }, StatusClass("ant-picker"), Disabled ? "ant-picker-disabled" : null, "m-picker");
        PickerText.Root(output, cfg);
        string Text(int i) => dates[i] is { } t ? PickerText.Display(t, "date", ShowTime, Format) : "";
        string Value(int i) => dates[i] is { } t ? PickerText.Canonical(t, "date", ShowTime, Format) : "";
        var firstId = id.Length > 0 && !attrs.Contains(" id=\"", StringComparison.Ordinal) ? id : null;
        var error = EffectiveStatus == "error";
        output.Content.SetHtmlContent(
            $"""<div class="ant-picker-input" :class="open && active === 0 && 'ant-picker-input-active'">{PickerText.TextInput(0, PlaceholderFrom, Text(0), firstId, Disabled, error, attrs)}</div>""" +
            $"""<div class="ant-picker-range-separator"><span aria-label="to" class="ant-picker-separator">{AntIcons.Span("swap-right")}</span></div>""" +
            $"""<div class="ant-picker-input" :class="open && active === 1 && 'ant-picker-input-active'">{PickerText.TextInput(1, PlaceholderTo, Text(1), null, Disabled, error, attrs)}</div>""" +
            """<div class="ant-picker-active-bar" :style="bar"></div>""" +
            $"""<span class="ant-picker-suffix" aria-hidden="true">{AntIcons.Span("calendar")}</span>""" +
            PickerText.Clear("values[0] || values[1]", Disabled) +
            PickerText.Hidden(NameFrom ?? name, Value(0), 0) + PickerText.Hidden(NameTo ?? name, Value(1), 1) +
            PickerText.Drop);
    }
}

/// <summary>
/// Values and markup of <c>app-date-picker</c> / <c>app-range-picker</c>: parsing, AntD Blazor's display formats (en-US) and
/// the canonical posted form, which <c>modulusPicker</c> (modulus-widgets.js) mirrors.
/// </summary>
public static class PickerText
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly string[] DayNames = ["sun", "mon", "tue", "wed", "thu", "fri", "sat"];

    /// <summary>AntD's en-US placeholders.</summary>
    public static string Placeholder(string picker) => picker switch
    {
        "week" => "Select week", "month" => "Select month", "quarter" => "Select quarter", "year" => "Select year", "time" => "Select time",
        _ => "Select date",
    };

    /// <summary>Week of the year as AntD Blazor counts it for en-US (CalendarWeekRule.FirstDay, Sunday).</summary>
    public static int Week(DateTime d) => Inv.Calendar.GetWeekOfYear(d, CalendarWeekRule.FirstDay, DayOfWeek.Sunday);

    /// <summary>
    /// Reads yyyy-MM-dd, yyyy-MM-ddTHH:mm[:ss], yyyy-MM-dd HH:mm:ss, yyyy-MM, yyyy-Qn, yyyy-Www, yyyy and HH:mm[:ss]
    /// (the time on today's date).
    /// </summary>
    public static bool TryParse(string? text, out DateTime value)
    {
        value = default;
        var s = text?.Trim() ?? "";
        if (s.Length == 0) return false;
        string[] full = ["yyyy-MM-dd", "yyyy-MM-dd'T'HH:mm", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd HH:mm", "yyyy-MM-dd HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF"];
        if (DateTime.TryParseExact(s, full, Inv, DateTimeStyles.None, out value)) return true;
        if (s.Length is 7 && (s[5] is 'Q' or 'q') && int.TryParse(s[..4], NumberStyles.None, Inv, out var qy) && s[6] is >= '1' and <= '4')
        {
            value = new DateTime(qy, (s[6] - '1') * 3 + 1, 1);
            return true;
        }
        var w = s.IndexOf("-W", StringComparison.OrdinalIgnoreCase);
        if (w == 4 && int.TryParse(s[..4], NumberStyles.None, Inv, out var wy) && int.TryParse(s[6..], NumberStyles.None, Inv, out var wn) && wn is >= 1 and <= 53)
        {
            var jan1 = new DateTime(wy, 1, 1);
            var start = jan1.AddDays(-(int)jan1.DayOfWeek + (wn - 1) * 7);
            value = start.Year < wy ? jan1 : start;
            return true;
        }
        if (DateTime.TryParseExact(s, "yyyy-MM", Inv, DateTimeStyles.None, out value)) return true;
        if (s.Length == 4 && int.TryParse(s, NumberStyles.None, Inv, out var y) && y >= 1) { value = new DateTime(y, 1, 1); return true; }
        if (TimeOnly.TryParseExact(s, ["HH:mm", "HH:mm:ss", "H:mm"], Inv, DateTimeStyles.None, out var time))
        {
            value = DateTime.Today.Add(time.ToTimeSpan());
            return true;
        }
        return false;
    }

    private static string Time(DateTime d, string format) =>
        d.ToString(format.Split(':').Length >= 3 ? "HH:mm:ss" : "HH:mm", Inv);

    /// <summary>The text AntD Blazor shows for the value.</summary>
    public static string Display(DateTime d, string picker, bool showTime, string format = "HH:mm:ss") => picker switch
    {
        "time" => Time(d, format),
        _ when showTime => d.ToString("yyyy-MM-dd", Inv) + " " + Time(d, format),
        "week" => $"{d.Year}-{Week(d)}Week",
        "month" => d.ToString("yyyy-MM", Inv),
        "quarter" => $"{d.Year}-Q{(d.Month - 1) / 3 + 1}",
        "year" => d.Year.ToString(Inv),
        _ => d.ToString("yyyy-MM-dd", Inv),
    };

    /// <summary>The posted value: as displayed, except yyyy-MM-ddTHH:mm:ss for dates with times and yyyy-Www for weeks.</summary>
    public static string Canonical(DateTime d, string picker, bool showTime, string format = "HH:mm:ss") =>
        showTime && picker != "time" ? d.ToString("yyyy-MM-dd'T'HH:mm:ss", Inv)
        : picker == "week" ? $"{d.Year}-W{Week(d):00}"
        : Display(d, picker, showTime, format);

    /// <summary>Disabled weekdays as JavaScript <c>getDay()</c> numbers, from names or numbers ("sat,sun" or "6,0").</summary>
    public static int[] Days(string? days) => string.IsNullOrWhiteSpace(days) ? [] :
        days.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(d => int.TryParse(d, NumberStyles.None, Inv, out var n)
                ? (n is >= 0 and <= 6 ? n : -1)
                : Array.FindIndex(DayNames, x => d.StartsWith(x, StringComparison.OrdinalIgnoreCase)))
            .Where(n => n >= 0).Distinct().Order().ToArray();

    internal static void Root(TagHelperOutput output, string config)
    {
        output.Attributes.SetAttribute("x-data", new Microsoft.AspNetCore.Html.HtmlString($"modulusPicker({config})"));
        output.Attributes.SetAttribute("@click.outside", "close(true)");
        output.Attributes.SetAttribute("@mousedown", "rootDown($event)");
    }

    internal static string TextInput(int i, string placeholder, string text, string? id, bool disabled, bool invalid, string attrs) =>
        $"""<input type="text" data-m-picker-text placeholder="{Html.E(placeholder)}" size="12" autocomplete="off" aria-haspopup="dialog" :aria-expanded="open"{Html.Attr("id", id)}""" +
        $"""{Html.Attr("aria-label", id is null ? placeholder : null)} value="{Html.E(text)}" :value="texts[{i}]"{Html.Attr("disabled", disabled)}{(invalid ? " aria-invalid=\"true\"" : "")}""" +
        $""" @focus="show({i})" @click="show({i})" @input="typed({i}, $event)" @keydown="keydown({i}, $event)"{attrs} />""";

    internal static string Clear(string when, bool disabled) => disabled ? "" :
        $"""<span class="ant-picker-clear" role="button" aria-label="Clear" x-show="{when}" x-cloak @mousedown.prevent @click.stop="clear()">{AntIcons.Span("close-circle", IconThemeType.Fill)}</span>""";

    internal static string Hidden(string name, string value, int i) => name.Length == 0 ? "" :
        $"""<input type="hidden" name="{Html.E(name)}" value="{Html.E(value)}" data-m-picker-value="{i}" />""";

    internal const string Drop =
        """<div x-ref="drop" :class="dropClass()" x-show="open" x-cloak x-html="open ? panel() : ''" @mousedown.prevent @click="pick($event)" @mouseover="hoverCell($event)"></div>""";
}

/// <summary>Slider over a native range input: <c>&lt;app-slider asp-for="Discount" min="0" max="100" show-value /&gt;</c>.</summary>
[HtmlTargetElement("app-slider", TagStructure = TagStructure.WithoutEndTag)]
public class SliderTagHelper : BoundControlTagHelper
{
    public decimal Min { get; set; }
    public decimal Max { get; set; } = 100;
    public decimal Step { get; set; } = 1;

    /// <summary>Shows the current value next to the slider.</summary>
    public bool ShowValue { get; set; }

    /// <summary>Format for the displayed value, e.g. "{0}%".</summary>
    public string ValueFormat { get; set; } = "{0}";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var raw = decimal.TryParse(Bound().Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var v) ? v : Min;
        var pct = Max > Min ? (raw - Min) / (Max - Min) * 100 : 0;
        var attrs = ControlAttributes(output, includeValue: false);
        var inv = CultureInfo.InvariantCulture;
        var format = Html.E(ValueFormat).Replace("{0}", "' + v + '", StringComparison.Ordinal);
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-slider", Disabled ? "ant-slider-disabled" : null);
        output.Attributes.SetAttribute("x-data", $"{{ v: {raw.ToString(inv)} }}");
        output.AddStyle($"--m-slider:{pct.ToString("0.##", inv)}%");
        var (min, max) = (Min.ToString(inv), Max.ToString(inv));
        output.Attributes.SetAttribute(":style", Max > Min ? "`--m-slider:${(v - " + min + ") / (" + max + " - " + min + ") * 100}%`" : "'--m-slider:0%'");
        output.Content.SetHtmlContent(
            $"""<input type="range" class="m-slider-input" min="{Min.ToString(inv)}" max="{Max.ToString(inv)}" step="{Step.ToString(inv)}" value="{raw.ToString(inv)}" x-model.number="v"{attrs} />""" +
            (ShowValue ? $"""<span class="m-slider-value" x-text="'{format}'">{Html.E(string.Format(inv, ValueFormat, raw))}</span>""" : ""));
    }
}

/// <summary>Star rating that posts a number: <c>&lt;app-rate asp-for="Rating" count="5" allow-clear /&gt;</c>.</summary>
[HtmlTargetElement("app-rate", TagStructure = TagStructure.WithoutEndTag)]
public class RateTagHelper : BoundControlTagHelper
{
    public int Count { get; set; } = 5;

    /// <summary>Clicking the current value again clears it.</summary>
    public bool AllowClear { get; set; } = true;

    /// <summary>Accessible names per star, e.g. "Terrible,Bad,Normal,Good,Wonderful".</summary>
    public string? Tooltips { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, _, raw) = Bound();
        var value = (int)Math.Round(decimal.TryParse(raw, NumberStyles.Number, CultureInfo.InvariantCulture, out var d) ? d : 0);
        var tips = Tooltips?.Split(',', StringSplitOptions.TrimEntries) ?? [];
        var star = AntIcons.Span("star", IconThemeType.Fill);
        output.TakeAttributes();
        output.TagName = "span";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-rate");
        output.Attributes.SetAttribute("x-data", $"{{ v: {value}, h: 0 }}");
        var sb = new StringBuilder($"""<ul class="{Html.Cx("ant-rate", Disabled ? "ant-rate-disabled" : null)}" role="radiogroup" @mouseleave="h = 0">""");
        for (var i = 1; i <= Count; i++)
        {
            var label = tips.ElementAtOrDefault(i - 1) ?? $"{i} star{(i == 1 ? "" : "s")}";
            var click = Disabled ? "" : $""" @click="v = {(AllowClear ? $"v === {i} ? 0 : {i}" : i.ToString(CultureInfo.InvariantCulture))}" @mouseenter="h = {i}" @keydown.enter.prevent="v = {i}" @keydown.space.prevent="v = {i}" """;
            sb.Append($"""<li class="ant-rate-star {(value >= i ? "ant-rate-star-full" : "ant-rate-star-zero")}" :class="(h || v) >= {i} ? 'ant-rate-star-full' : 'ant-rate-star-zero'">""")
              .Append($"""<div role="radio" aria-label="{Html.E(label)}" aria-checked="{(value == i ? "true" : "false")}" :aria-checked="v === {i}" aria-posinset="{i}" aria-setsize="{Count}" tabindex="{(Disabled ? -1 : 0)}"{click.TrimEnd()}>""")
              .Append($"""<div class="ant-rate-star-first">{star}</div><div class="ant-rate-star-second">{star}</div></div></li>""");
        }
        sb.Append("</ul>");
        if (name.Length > 0) sb.Append($"""<input type="hidden" name="{Html.E(name)}" value="{value}" :value="v" />""");
        output.Content.SetHtmlContent(sb.ToString());
    }
}

/// <summary>
/// Segmented control over radio inputs: <c>&lt;app-segmented asp-for="View" items="new[] { "List", "Board" }" /&gt;</c>.
/// </summary>
[HtmlTargetElement("app-segmented", TagStructure = TagStructure.WithoutEndTag)]
public class SegmentedTagHelper : BoundControlTagHelper
{
    public IEnumerable<string> Items { get; set; } = [];

    /// <summary>Stretch to the container width.</summary>
    public bool Block { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, _, current) = Bound();
        var extra = output.TakeAttributes();
        var groupName = name.Length > 0 ? name : context.UniqueId;
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-segmented", Block ? "ant-segmented-block" : null, SizeClass("ant-segmented"), Disabled ? "ant-segmented-disabled" : null);
        output.Attributes.SetAttribute("role", "radiogroup");
        var sb = new StringBuilder("""<div class="ant-segmented-group">""");
        foreach (var item in Items)
        {
            var on = item == current;
            sb.Append($"""<label class="ant-segmented-item{(on ? " ant-segmented-item-selected" : "")}"><input class="ant-segmented-item-input" type="radio" name="{Html.E(groupName)}" value="{Html.E(item)}"{(on ? " checked" : "")}{Html.Attr("disabled", Disabled)}{extra} /><div class="ant-segmented-item-label" title="{Html.E(item)}">{Html.E(item)}</div></label>""");
        }
        output.Content.SetHtmlContent(sb.Append("</div>").ToString());
    }
}

/// <summary>
/// Checkbox group bound to a collection: <c>&lt;app-checkbox-group asp-for="Channels" items="…" /&gt;</c>.
/// </summary>
[HtmlTargetElement("app-checkbox-group", TagStructure = TagStructure.WithoutEndTag)]
public class CheckboxGroupTagHelper : BoundControlTagHelper
{
    public IEnumerable<SelectListItem> Items { get; set; } = [];

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, _, _) = Bound();
        var selected = For?.Model is IEnumerable list and not string
            ? list.Cast<object?>().Select(Html.Format).ToHashSet()
            : [];
        var extra = output.TakeAttributes();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-checkbox-group");
        output.Attributes.SetAttribute("role", "group");
        var sb = new StringBuilder();
        foreach (var item in Items)
        {
            var on = item.Selected || selected.Contains(item.Value) || selected.Contains(item.Text);
            var off = item.Disabled || Disabled;
            sb.Append($"""<label class="{Html.Cx("ant-checkbox-wrapper", "ant-checkbox-group-item", on ? "ant-checkbox-wrapper-checked" : null, off ? "ant-checkbox-wrapper-disabled" : null)}"><span class="{Html.Cx("ant-checkbox", on ? "ant-checkbox-checked" : null, off ? "ant-checkbox-disabled" : null)}"><input type="checkbox" class="ant-checkbox-input" name="{Html.E(name)}" value="{Html.E(item.Value)}"{(on ? " checked" : "")}{Html.Attr("disabled", off)}{extra} /><span class="ant-checkbox-inner"></span></span><span>{Html.E(item.Text)}</span></label>""");
        }
        output.Content.SetHtmlContent(sb.ToString());
    }
}

/// <summary>
/// File upload: a button, or a drag-and-drop area with <c>drag</c>. Selected files are listed and can be
/// removed before submit (the enclosing form needs <c>enctype="multipart/form-data"</c>).
/// <code>&lt;app-upload name="files" drag multiple accept=".pdf,image/*" hint="PDF or images up to 10 MB" /&gt;</code>
/// With <c>action</c> each file is posted there as soon as it is picked (field <c>name</c>, antiforgery header
/// included) and shows its progress. <c>max-size</c> (MB), <c>max-count</c> and <c>accept</c> reject files in the
/// browser with a message; <c>list-type="picture"</c> shows image thumbnails.
/// </summary>
[HtmlTargetElement("app-upload")]
public class UploadTagHelper : TagHelper
{
    public string Name { get; set; } = "file";
    public bool Multiple { get; set; }
    public string? Accept { get; set; }
    public bool Drag { get; set; }
    public bool Disabled { get; set; }

    /// <summary>Main text (drag area) or button text.</summary>
    public string? Text { get; set; }

    /// <summary>Secondary text in the drag area.</summary>
    public string? Hint { get; set; }

    /// <summary>text | picture</summary>
    public string ListType { get; set; } = "text";

    /// <summary>Largest file in megabytes.</summary>
    public double? MaxSize { get; set; }

    /// <summary>Most files in the list.</summary>
    public int? MaxCount { get; set; }

    /// <summary>URL each file is posted to as soon as it is added; unset keeps the files in the form.</summary>
    public string? Action { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var custom = (await output.GetChildContentAsync()).GetContent();
        var extra = output.TakeAttributes();
        var picture = ListType == "picture";
        var input = $"""<input type="file" class="m-upload-input" x-ref="input" @change="sync()" name="{Html.E(Name)}"{Html.Attr("multiple", Multiple)}{Html.Attr("accept", Accept)}{Html.Attr("disabled", Disabled)}{extra} />""";
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-upload-wrapper", "m-upload");
        var inv = CultureInfo.InvariantCulture;
        output.Attributes.SetAttribute("x-data", MaxSize is null && MaxCount is null && Action is null && !picture && Accept is null
            ? "modulusUpload"
            : "modulusUpload({ " + string.Join(", ", new[]
            {
                MaxSize is { } mb ? $"maxSize: {(mb * 1024 * 1024).ToString("0", inv)}" : null,
                MaxCount is { } n ? $"maxCount: {n.ToString(inv)}" : null,
                Accept is null ? null : $"accept: '{Js(Accept)}'",
                Action is null ? null : $"action: '{Js(Action)}'",
                picture ? "picture: true" : null,
            }.Where(x => x is not null)) + " })");

        // Values go into a single-quoted JS string inside an attribute that SetAttribute encodes.
        static string Js(string value) => value.Replace("\\", "", StringComparison.Ordinal).Replace("'", "", StringComparison.Ordinal);

        string trigger;
        if (Drag)
        {
            var body = string.IsNullOrWhiteSpace(custom)
                ? $"""<p class="ant-upload-drag-icon">{AntIcons.Span("inbox")}</p><p class="ant-upload-text">{Html.E(Text ?? "Click or drag files to this area to upload")}</p>""" +
                  (Hint is null ? "" : $"""<p class="ant-upload-hint">{Html.E(Hint)}</p>""")
                : custom;
            trigger =
                $"""<div class="{Html.Cx("ant-upload", "ant-upload-drag", Disabled ? "ant-upload-disabled" : null)}" :class="over && 'ant-upload-drag-hover'" @dragover.prevent="over = true" @dragleave="over = false" @drop.prevent="drop($event)">""" +
                $"""<label class="ant-upload ant-upload-btn">{input}<div class="ant-upload-drag-container">{body}</div></label></div>""";
        }
        else
        {
            var body = string.IsNullOrWhiteSpace(custom) ? $"""{AntIcons.Span("upload")}<span>{Html.E(Text ?? "Click to upload")}</span>""" : custom;
            trigger = $"""<div class="ant-upload ant-upload-select"><label class="{Html.Cx("ant-btn", "ant-btn-default", Disabled ? "m-disabled" : null)}">{input}{body}</label></div>""" +
                      (Hint is null ? "" : $"""<div class="m-upload-hint">{Html.E(Hint)}</div>""");
        }

        var type = picture ? "picture" : "text";
        var icon = picture
            ? $"""<span class="ant-upload-list-item-thumbnail"><img x-show="file.url" :src="file.url" :alt="file.name" class="ant-upload-list-item-image" /><span x-show="!file.url">{AntIcons.Span("file")}</span></span>"""
            : $"""<div class="ant-upload-text-icon">{AntIcons.Span("paper-clip")}</div>""";
        output.Content.SetHtmlContent(trigger +
            $"""<div class="ant-upload-list ant-upload-list-{type}" aria-live="polite"><template x-for="(file, i) in files" :key="file.key"><div class="ant-upload-list-{type}-container">""" +
            $"""<div class="ant-upload-list-item ant-upload-list-item-list-type-{type}" :class="'ant-upload-list-item-' + file.status">""" +
            $"""<div class="ant-upload-list-item-info"><span class="ant-upload-span">{icon}<span class="ant-upload-list-item-name" x-text="file.name" :title="file.error || file.name"></span>""" +
            """<span class="m-upload-size" x-text="file.size"></span>""" +
            $"""<span class="ant-upload-list-item-card-actions"><button type="button" class="ant-btn ant-btn-text ant-btn-sm ant-btn-icon-only ant-upload-list-item-card-actions-btn" :aria-label="'Remove ' + file.name" @click="remove(i)">{AntIcons.Span("delete")}</button></span></span></div>""" +
            """<div class="ant-upload-list-item-progress" x-show="file.status === 'uploading'"><div class="ant-progress ant-progress-line ant-progress-status-active ant-progress-small" role="progressbar" :aria-valuenow="file.percent" aria-valuemin="0" aria-valuemax="100" :aria-label="'Uploading ' + file.name">""" +
            """<div class="ant-progress-outer"><div class="ant-progress-inner"><div class="ant-progress-bg" :style="'width:' + file.percent + '%;height:2px'"></div></div></div></div></div>""" +
            """<div class="m-upload-error" x-show="file.error" x-text="file.error"></div>""" +
            """</div></div></template></div>""");
    }
}
