using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

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
        "week" => "Select week",
        "month" => "Select month",
        "quarter" => "Select quarter",
        "year" => "Select year",
        "time" => "Select time",
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
