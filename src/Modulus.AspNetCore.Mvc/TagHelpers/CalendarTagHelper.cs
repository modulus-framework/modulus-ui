using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Month calendar (Ant Design's Calendar): <c>&lt;app-calendar value="Model.Day" /&gt;</c>, or
/// <c>fullscreen</c> for a page-sized one. Year and month selects and the Month / Year switch work in the
/// browser; with <c>name</c> (or <c>asp-for</c>) the selected day posts as yyyy-MM-dd.
/// Day and month names follow the request culture.
/// </summary>
[HtmlTargetElement("app-calendar", TagStructure = TagStructure.WithoutEndTag)]
public class CalendarTagHelper : BoundControlTagHelper
{
    /// <summary>The selected day (defaults to the bound value, then today).</summary>
    public DateTime? Date { get; set; }

    public bool Fullscreen { get; set; }

    /// <summary>month | year</summary>
    public string Mode { get; set; } = "month";

    public string MonthLabel { get; set; } = "Month";
    public string YearLabel { get; set; } = "Year";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (name, _, _) = Bound();
        var culture = CultureInfo.CurrentCulture;
        var selected = (Date ?? For?.Model switch { DateTime d => (DateTime?)d, DateOnly d => d.ToDateTime(TimeOnly.MinValue), _ => null }
            ?? (DateTime.TryParse(Value, CultureInfo.InvariantCulture, out var parsed) ? parsed : DateTime.Today)).Date;
        var firstDay = (int)culture.DateTimeFormat.FirstDayOfWeek;
        var days = Enumerable.Range(0, 7).Select(i => ShortDay(culture, (i + firstDay) % 7)).ToArray();
        var months = culture.DateTimeFormat.AbbreviatedMonthNames.Take(12).ToArray();
        var year = Mode == "year";
        var group = $"m-cal-{context.UniqueId}";

        var years = string.Concat(Enumerable.Range(selected.Year - 10, 21).Select(y => $"""<option value="{y}"{(y == selected.Year ? " selected" : "")}>{y}</option>"""));
        var monthOptions = string.Concat(months.Select((m, i) => $"""<option value="{i}"{(i == selected.Month - 1 ? " selected" : "")}>{Html.E(m)}</option>"""));
        var header =
            """<div class="ant-picker-calendar-header">""" +
            $"""<select class="m-select m-select-sm ant-picker-calendar-year-select" aria-label="Year" x-model.number="year">{years}</select>""" +
            $"""<select class="m-select m-select-sm ant-picker-calendar-month-select" aria-label="Month" x-model.number="month" x-show="mode === 'month'"{(year ? " style=\"display: none;\"" : "")}>{monthOptions}</select>""" +
            """<div class="ant-picker-calendar-mode-switch ant-radio-group ant-radio-group-small">""" +
            ModeButton(group, "month", MonthLabel, !year) + ModeButton(group, "year", YearLabel, year) +
            "</div></div>";

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-picker-calendar", Fullscreen ? "ant-picker-calendar-full" : null);
        output.Attributes.SetAttribute("x-data", new HtmlString($"modulusCalendar({Widgets.Config(new { selected = selected.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), mode = year ? "year" : "month", firstDay, days, months, today = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) })})"));
        output.Content.SetHtmlContent(
            header +
            $"""<div class="ant-picker-panel" x-html="panel()" @click="pick($event)">{(year ? YearPanel(selected, months) : MonthPanel(selected, firstDay, days))}</div>""" +
            (name.Length > 0 ? $"""<input type="hidden" name="{Html.E(name)}" value="{selected:yyyy-MM-dd}" :value="selected" />""" : ""));
    }

    private static string ModeButton(string group, string value, string label, bool on) =>
        $$"""<label class="{{Html.Cx("ant-radio-button-wrapper", on ? "ant-radio-button-wrapper-checked" : null)}}" :class="{ 'ant-radio-button-wrapper-checked': mode === '{{value}}' }"><span class="{{Html.Cx("ant-radio-button", on ? "ant-radio-button-checked" : null)}}" :class="{ 'ant-radio-button-checked': mode === '{{value}}' }"><input type="radio" class="ant-radio-button-input" name="{{group}}" value="{{value}}"{{(on ? " checked" : "")}} x-model="mode" /><span class="ant-radio-button-inner"></span></span><span>{{Html.E(label)}}</span></label>""";

    /// <summary>Two-letter weekday names, as AntD shows them ("Su", "Mo"; "So", "Mo"; "di", "lu").</summary>
    internal static string ShortDay(CultureInfo culture, int day)
    {
        var name = culture.DateTimeFormat.AbbreviatedDayNames[day].TrimEnd('.');
        return name.Length > 2 ? name[..2] : name;
    }

    // The same markup as modulusCalendar.panel() in modulus-widgets.js.
    internal static string MonthPanel(DateTime selected, int firstDay, string[] days)
    {
        var first = new DateTime(selected.Year, selected.Month, 1);
        var start = first.AddDays(-(((int)first.DayOfWeek - firstDay + 7) % 7));
        var sb = new StringBuilder("""<div class="ant-picker-date-panel"><div class="ant-picker-body"><table class="ant-picker-content"><thead><tr>""");
        foreach (var d in days) sb.Append($"<th>{Html.E(d)}</th>");
        sb.Append("</tr></thead><tbody>");
        for (var w = 0; w < 6; w++)
        {
            sb.Append("<tr>");
            for (var i = 0; i < 7; i++)
            {
                var day = start.AddDays(w * 7 + i);
                var cls = Html.Cx("ant-picker-cell", day.Month == selected.Month ? "ant-picker-cell-in-view" : null,
                    day == DateTime.Today ? "ant-picker-cell-today" : null, day == selected ? "ant-picker-cell-selected" : null);
                sb.Append($"""<td title="{day:yyyy-MM-dd}" class="{cls}"><div class="ant-picker-cell-inner ant-picker-calendar-date"><div class="ant-picker-calendar-date-value">{day.Day}</div><div class="ant-picker-calendar-date-content"></div></div></td>""");
            }
            sb.Append("</tr>");
        }
        return sb.Append("</tbody></table></div></div>").ToString();
    }

    internal static string YearPanel(DateTime selected, string[] months)
    {
        var sb = new StringBuilder("""<div class="ant-picker-month-panel"><div class="ant-picker-body"><table class="ant-picker-content"><tbody>""");
        for (var r = 0; r < 4; r++)
        {
            sb.Append("<tr>");
            for (var c = 0; c < 3; c++)
            {
                var m = r * 3 + c;
                var today = DateTime.Today.Year == selected.Year && DateTime.Today.Month == m + 1;
                var cls = Html.Cx("ant-picker-cell", "ant-picker-cell-in-view", today ? "ant-picker-cell-today" : null, m == selected.Month - 1 ? "ant-picker-cell-selected" : null);
                sb.Append($"""<td title="{selected.Year:D4}-{m + 1:D2}" class="{cls}"><div class="ant-picker-cell-inner ant-picker-calendar-date"><div class="ant-picker-calendar-date-value">{Html.E(months[m])}</div><div class="ant-picker-calendar-date-content"></div></div></td>""");
            }
            sb.Append("</tr>");
        }
        return sb.Append("</tbody></table></div></div>").ToString();
    }
}
