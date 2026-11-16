using System.Collections;
using System.Globalization;
using System.Text;
using AntDesign;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Time of day with Ant Design's hour, minute and second columns: <c>&lt;app-time-picker asp-for="Opens" format="HH:mm" /&gt;</c>.
/// The same control as <c>&lt;app-date-picker picker="time"&gt;</c>, named like AntD Blazor's <c>TimePicker</c>.
/// </summary>
[HtmlTargetElement("app-time-picker", TagStructure = TagStructure.WithoutEndTag)]
public class TimePickerTagHelper : DatePickerTagHelper
{
    public TimePickerTagHelper() => Picker = "time";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        Picker = "time";
        ShowTime = false;
        base.Process(context, output);
    }
}
