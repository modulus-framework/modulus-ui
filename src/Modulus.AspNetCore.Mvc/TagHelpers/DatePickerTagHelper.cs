using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

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
            picker,
            showTime = ShowTime,
            format = Format,
            values = new[] { date is { } v ? PickerText.Canonical(v, picker, ShowTime, Format) : "" },
            min,
            max,
            days = PickerText.Days(DisabledDays),
            message = DisabledDaysMessage,
            disabled = Disabled,
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
