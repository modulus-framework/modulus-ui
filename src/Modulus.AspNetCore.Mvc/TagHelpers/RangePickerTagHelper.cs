using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

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
            picker = "date",
            showTime = ShowTime,
            format = Format,
            range = true,
            values = dates.Select(d => d is { } v ? PickerText.Canonical(v, "date", ShowTime, Format) : "").ToArray(),
            min,
            max,
            days = PickerText.Days(DisabledDays),
            message = DisabledDaysMessage,
            disabled = Disabled,
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
