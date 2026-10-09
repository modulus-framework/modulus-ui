using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Gantt;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>app-gantt: the rows, the bars' placement and fill, the late mark, the today line and the escaped task names.</summary>
public class GanttTagHelperTests
{
    private static async Task<string> RenderAsync(GanttView view)
    {
        var helper = new GanttTagHelper { View = view };
        var output = new TagHelperOutput("app-gantt", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext("app-gantt", [], new Dictionary<object, object>(), "g1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task DrawsTheRowsGroupsBarsAndTodayLine()
    {
        var view = GanttModel.Build(
        [
            new GanttTask(1, "Design <review>", new DateTime(2026, 10, 5), new DateTime(2026, 10, 7), 100, "Discovery", "success"),
            new GanttTask(2, "Build", new DateTime(2026, 10, 8), new DateTime(2026, 10, 14), 60, "Build"),
        ], new DateTime(2026, 10, 5), new DateTime(2026, 10, 18), new DateTime(2026, 10, 9));

        var html = await RenderAsync(view);

        Assert.Equal(2, CountOf(html, "class=\"m-gantt-row\""));
        Assert.Equal(2, CountOf(html, "class=\"m-gantt-group\""));
        Assert.Equal(2, CountOf(html, "class=\"m-gantt-today\""));
        Assert.Contains("class=\"m-gantt-bar m-gantt-success\" style=\"left:0%;width:21.429%\"", html);
        Assert.Contains("<span class=\"m-gantt-fill\" style=\"width:60%\"></span>", html);
        Assert.Contains("Design &lt;review&gt;", html);
        Assert.Contains("<span class=\"m-gantt-tick\" style=\"left:50%\">Oct 12</span>", html);
    }

    [Fact]
    public async Task ALateTask_IsMarkedLate()
    {
        var view = GanttModel.Build([new GanttTask(1, "Old", new DateTime(2026, 10, 5), new DateTime(2026, 10, 6), 10)],
            new DateTime(2026, 10, 5), new DateTime(2026, 10, 18), new DateTime(2026, 10, 9));

        var html = await RenderAsync(view);

        Assert.Contains("m-gantt-primary late", html);
    }

    private static int CountOf(string text, string needle) => (text.Length - text.Replace(needle, "", StringComparison.Ordinal).Length) / needle.Length;
}
