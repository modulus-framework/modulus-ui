using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Charts;
using Modulus.Ui.Abstractions.Forms;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>What the Plugins pages added to the MVC library: quarter and year pickers, range presets, upload limits
/// and progress, and the chart legend toggle / drill-down attributes.</summary>
public class PluginTagHelperTests
{
    private static async Task<string> RenderAsync(TagHelper helper, string tag, TagHelperAttributeList? attributes = null)
    {
        var output = new TagHelperOutput(tag, attributes ?? [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext(tag, [], new Dictionary<object, object>(), "u1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task DatePicker_RendersAntDesignsInputAndPopupHost()
    {
        var html = await RenderAsync(new DatePickerTagHelper { Name = "d", Value = "2026-09-28" }, "app-date-picker");

        Assert.StartsWith("<div class=\"ant-picker m-picker\" x-data=\"modulusPicker(", html);
        Assert.Contains("<input type=\"text\" data-m-picker-text placeholder=\"Select date\" size=\"12\" autocomplete=\"off\"", html);
        Assert.Contains("value=\"2026-09-28\" :value=\"texts[0]\"", html);
        Assert.Contains("<input type=\"hidden\" name=\"d\" value=\"2026-09-28\" data-m-picker-value=\"0\" />", html);
        Assert.Contains("<span class=\"ant-picker-clear\"", html);
        Assert.Contains("x-ref=\"drop\"", html);
        Assert.DoesNotContain("type=\"date\"", html);
    }

    [Theory]
    [InlineData("week", "2026-09-28", "2026-40Week", "2026-W40", "Select week")]
    [InlineData("month", "2026-09-28", "2026-09", "2026-09", "Select month")]
    [InlineData("quarter", "2026-Q3", "2026-Q3", "2026-Q3", "Select quarter")]
    [InlineData("year", "2026", "2026", "2026", "Select year")]
    [InlineData("time", "08:30", "08:30:00", "08:30:00", "Select time")]
    public async Task DatePicker_ShowsAntDesignsFormat_AndPostsTheCanonicalValue(string picker, string value, string text, string posted, string placeholder)
    {
        var html = await RenderAsync(new DatePickerTagHelper { Name = "p", Picker = picker, Value = value }, "app-date-picker");

        Assert.Contains($"placeholder=\"{placeholder}\"", html);
        Assert.Contains($"value=\"{text}\" :value=\"texts[0]\"", html);
        Assert.Contains($"name=\"p\" value=\"{posted}\" data-m-picker-value", html);
    }

    [Fact]
    public async Task DatePicker_ShowTime_ShowsSecondsAndPostsIso()
    {
        var html = await RenderAsync(new DatePickerTagHelper { Name = "m", ShowTime = true, Value = "2026-09-30T10:00" }, "app-date-picker");

        Assert.Contains("value=\"2026-09-30 10:00:00\" :value=\"texts[0]\"", html);
        Assert.Contains("value=\"2026-09-30T10:00:00\" data-m-picker-value", html);
    }

    [Fact]
    public async Task DatePicker_MinMaxAndDisabledDays_GoToTheComponent()
    {
        var html = await RenderAsync(new DatePickerTagHelper { Name = "d", DisabledDays = "sat, Sun" }, "app-date-picker",
            [new TagHelperAttribute("min", "2026-10-01"), new TagHelperAttribute("max", "2026-11-30")]);

        Assert.Contains("&quot;min&quot;:&quot;2026-10-01&quot;", html);
        Assert.Contains("&quot;max&quot;:&quot;2026-11-30&quot;", html);
        Assert.Contains("&quot;days&quot;:[0,6]", html);
        Assert.DoesNotContain(" min=\"", html);
    }

    [Fact]
    public async Task RangePicker_Presets_GoToThePopupFooter()
    {
        var presets = DatePeriods.Presets(new DateOnly(2026, 9, 28));
        var html = await RenderAsync(new RangePickerTagHelper { NameFrom = "from", NameTo = "to", ValueFrom = "2026-09-01", Presets = presets }, "app-range-picker");

        Assert.StartsWith("<div class=\"ant-picker ant-picker-range m-picker\" x-data=\"modulusPicker(", html);
        Assert.Contains("&quot;label&quot;:&quot;Last 7 days&quot;,&quot;from&quot;:&quot;2026-09-22&quot;,&quot;to&quot;:&quot;2026-09-28&quot;", html);
        Assert.Contains("placeholder=\"Start date\"", html);
        Assert.Contains("placeholder=\"End date\"", html);
        Assert.Contains("<div class=\"ant-picker-active-bar\" :style=\"bar\"></div>", html);
        Assert.Contains("<input type=\"hidden\" name=\"from\" value=\"2026-09-01\" data-m-picker-value=\"0\" />", html);
        Assert.Contains("<input type=\"hidden\" name=\"to\" value=\"\" data-m-picker-value=\"1\" />", html);
        Assert.DoesNotContain("m-picker-presets", html);
    }

    [Theory]
    [InlineData("2026-09-28", 2026, 9, 28)]
    [InlineData("2026-09-28T10:15", 2026, 9, 28)]
    [InlineData("2026-09-28 10:15:30", 2026, 9, 28)]
    [InlineData("2026-Q3", 2026, 7, 1)]
    [InlineData("2026-09", 2026, 9, 1)]
    [InlineData("2026", 2026, 1, 1)]
    [InlineData("2026-W01", 2026, 1, 1)]
    [InlineData("2026-W40", 2026, 9, 27)]
    public void PickerText_ReadsEveryPostedForm(string text, int y, int m, int d)
    {
        Assert.True(PickerText.TryParse(text, out var value));
        Assert.Equal(new DateOnly(y, m, d), DateOnly.FromDateTime(value));
    }

    [Fact]
    public void PickerText_RejectsNonsense()
    {
        Assert.False(PickerText.TryParse("2026-13", out _));
        Assert.False(PickerText.TryParse("soon", out _));
        Assert.False(PickerText.TryParse("", out _));
    }

    [Fact]
    public async Task Upload_PassesLimitsAndActionToTheScript_AndDrawsPictureItems()
    {
        var html = await RenderAsync(new UploadTagHelper { Name = "file", Drag = true, Multiple = true, ListType = "picture", Accept = ".pdf,image/*", MaxSize = 5, MaxCount = 6, Action = "/plugins/dropzone/upload" }, "app-upload");

        Assert.Contains("x-data=\"modulusUpload({ maxSize: 5242880, maxCount: 6, accept: &#x27;.pdf,image/*&#x27;, action: &#x27;/plugins/dropzone/upload&#x27;, picture: true })\"", html);
        Assert.Contains("<div class=\"ant-upload-list ant-upload-list-picture\" aria-live=\"polite\">", html);
        Assert.Contains("ant-upload-list-item-thumbnail", html);
        Assert.Contains("role=\"progressbar\"", html);
        Assert.Contains("x-text=\"file.error\"", html);
    }

    [Fact]
    public async Task Upload_WithoutOptions_KeepsThePlainComponent()
    {
        var html = await RenderAsync(new UploadTagHelper { Name = "files" }, "app-upload");

        Assert.Contains("x-data=\"modulusUpload\"", html);
        Assert.Contains("ant-upload-list-text", html);
        Assert.DoesNotContain("ant-upload-list-item-thumbnail", html);
    }

    [Fact]
    public async Task ColumnChart_LegendToggleAndLinks_ReachTheRenderer()
    {
        var html = await RenderAsync(new ColumnChartTagHelper
        {
            Labels = ["Q1", "Q2"],
            Series = [new ChartSeries("Online", [1, 2]), new ChartSeries("Retail", [3, 4])],
            LegendToggle = true,
            Links = ["/q/1", null],
        }, "app-column-chart");

        Assert.Contains("data-m-legend", html);
        Assert.Contains("<a href=\"/q/1\">Q1</a>", html);
        Assert.Contains("<span>Q2</span>", html);
    }
}
