using System.Globalization;
using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Gantt;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// A read-only timeline (<see cref="GanttView"/>), the MVC front end of the Blazor <c>Gantt</c>:
/// <code>&lt;app-gantt view="@Model" /&gt;</code>
/// The markup matches the Blazor component: group headings, a row per task with its bar filled to its progress, weekly ticks
/// and a today line.
/// </summary>
[HtmlTargetElement("app-gantt", TagStructure = TagStructure.WithoutEndTag)]
public class GanttTagHelper : TagHelper
{
    /// <summary>The timeline to draw.</summary>
    public GanttView? View { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var view = View ?? throw new InvalidOperationException("app-gantt needs a view.");
        var sb = new StringBuilder();
        sb.Append("""<div class="m-gantt" data-m-gantt><div class="m-gantt-head" aria-hidden="true"><div class="m-gantt-name">Task</div><div class="m-gantt-track">""");
        foreach (var tick in view.Ticks)
            sb.Append($"""<span class="m-gantt-tick" style="left:{Pct(tick.LeftPct)}">{Html.E(tick.Label)}</span>""");
        sb.Append("""</div></div><div class="m-gantt-body" role="table" aria-label="Timeline">""");
        foreach (var row in view.Rows)
        {
            var task = row.Task;
            if (row.NewGroup)
                sb.Append($"""<div class="m-gantt-group" role="rowheader">{Html.E(task.Group is { Length: > 0 } g ? g : "Ungrouped")}</div>""");
            sb.Append("""<div class="m-gantt-row" role="row"><div class="m-gantt-name" role="cell">""");
            sb.Append($"""<span class="m-heading m-medium m-truncate">{Html.E(task.Name)}</span>""");
            if (task.Owner is { Length: > 0 }) sb.Append($"""<span class="m-xsmall m-muted m-truncate">{Html.E(task.Owner)}</span>""");
            sb.Append("""</div><div class="m-gantt-track" role="cell">""");
            var classes = $"m-gantt-bar m-gantt-{Html.E(task.Tone)}{(row.Late ? " late" : "")}{(row.ClippedStart ? " clip-start" : "")}{(row.ClippedEnd ? " clip-end" : "")}";
            var title = Html.E(Title(row));
            sb.Append($"""<div class="{classes}" style="left:{Pct(row.LeftPct)};width:{Pct(row.WidthPct)}" title="{title}" aria-label="{title}"><span class="m-gantt-fill" style="width:{row.Progress}%"></span><span class="m-gantt-bar-label">{row.Progress}%</span></div>""");
            if (view.TodayPct is { } today)
                sb.Append($"""<div class="m-gantt-today" style="left:{Pct(today)}" aria-hidden="true"></div>""");
            sb.Append("</div></div>");
        }
        sb.Append("</div></div>");
        output.TagName = null;
        output.Content.SetHtmlContent(sb.ToString());
    }

    private static string Pct(double value) => value.ToString("0.###", CultureInfo.InvariantCulture) + "%";

    private static string Title(GanttRowView row)
    {
        var t = row.Task;
        return $"{t.Name}: {t.Start:MMM d} to {t.End:MMM d}, {row.Progress}%" + (row.Late ? ", late" : "");
    }
}
