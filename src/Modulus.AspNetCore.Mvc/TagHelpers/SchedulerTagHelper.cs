using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Scheduler;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// An event calendar (<see cref="SchedulerView"/>), the MVC front end of the Blazor <c>Scheduler</c>:
/// <code>&lt;app-scheduler view="@Model" url="/apps/calendar" /&gt;</code>
/// Months, the view and the filter are boosted GETs to <c>{url}</c> (the page reloads with the new query); the event form is
/// the drawer's, loaded from <c>{url}/form</c>. A save or delete fires <see cref="RefreshEvent"/>, which reloads the calendar.
/// </summary>
[HtmlTargetElement("app-scheduler", TagStructure = TagStructure.WithoutEndTag)]
public class SchedulerTagHelper : TagHelper
{
    /// <summary>The event a save or delete fires (htmx) so the calendar reloads.</summary>
    public const string RefreshEvent = "scheduler:refresh";

    /// <summary>The calendar to draw.</summary>
    public SchedulerView? View { get; set; }

    /// <summary>The page the calendar links to (its month, view and filter links are under this path).</summary>
    public string Url { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var view = View ?? throw new InvalidOperationException("app-scheduler needs a view.");
        var url = Html.E(Url);
        var names = view.Categories.Select(c => c.Name).ToList();
        var query = Html.E(SchedulerModel.QueryString(view.Query, names));
        var sb = new StringBuilder();
        sb.Append($"""<div id="scheduler" class="m-card m-app m-app-calendar" data-m-scheduler hx-boost="true" hx-target="#scheduler" hx-select="#scheduler" hx-swap="outerHTML" hx-push-url="true">""");
        sb.Append($"""<div hidden hx-get="{url}{query}" hx-trigger="{RefreshEvent} from:body" hx-push-url="false"></div>""");
        Aside(sb, view, url);
        Main(sb, view, url);
        sb.Append("</div>");
        output.TagName = null;
        output.Content.SetHtmlContent(sb.ToString());
    }

    private static void Aside(StringBuilder sb, SchedulerView view, string url)
    {
        sb.Append("<aside class=\"m-app-side\">");
        sb.Append($"""<div class="m-app-side-head"><button type="button" class="ant-btn ant-btn-primary ant-btn-block" @click="$store.drawer.load('{url}/form', 'Add event')">{ShellRenderer.Icon("plus")}<span>Add event</span></button></div>""");
        sb.Append("""<div class="m-app-side-body">""");
        sb.Append($"""<form method="get" action="{url}" @change="$el.requestSubmit()">""");
        sb.Append($"""<input type="hidden" name="month" value="{view.Month.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture)}" />""");
        if (view.List) sb.Append("""<input type="hidden" name="view" value="list" />""");
        sb.Append("""<input type="hidden" name="filtered" value="true" />""");
        sb.Append("""<div class="m-subheader m-app-nav-title" style="margin-top:0">Event filters</div><div class="m-cal-filters">""");
        foreach (var category in view.Categories)
        {
            var on = view.IsOn(category.Name);
            var name = Html.E(category.Name);
            sb.Append($"""<label class="ant-checkbox-wrapper{(on ? " ant-checkbox-wrapper-checked" : "")}" style="--c:{Html.E(category.Color)}"><span class="ant-checkbox{(on ? " ant-checkbox-checked" : "")}"><input type="checkbox" class="ant-checkbox-input" name="cat" value="{name}"{(on ? " checked" : "")} /><span class="ant-checkbox-inner"></span></span><span>{name}</span></label>""");
        }
        sb.Append("</div></form>");
        sb.Append("""<div class="m-subheader m-app-nav-title">Upcoming</div>""");
        var upcoming = view.Upcoming();
        if (upcoming.Count == 0) sb.Append("""<div class="m-small m-muted" style="padding:0 8px">Nothing coming up this month</div>""");
        foreach (var e in upcoming)
            sb.Append($"""<button type="button" class="m-cal-agenda-row" @click="$store.drawer.load('{url}/form/{e.Id}', 'Edit event')"><span class="m-app-dot" style="--c:{Html.E(view.ColorOf(e.Category))}"></span><span class="m-truncate" style="flex:1"><span class="m-heading m-medium">{Html.E(e.Title)}</span><br /><span class="m-xsmall m-muted">{e.Start.ToString("ddd, MMM d", System.Globalization.CultureInfo.InvariantCulture)} · {Html.E(SchedulerModel.TimeLabel(e))}</span></span></button>""");
        sb.Append("</div></aside>");
    }

    private static void Main(StringBuilder sb, SchedulerView view, string url)
    {
        var today = view.Today;
        sb.Append($"""<section class="m-app-main" aria-label="{Html.E(view.Title)}">""");
        sb.Append("""<div class="m-cal-toolbar"><div class="ant-btn-group" style="display:inline-flex">""");
        sb.Append($"""<a class="ant-btn ant-btn-default ant-btn-icon-only" href="{url}{Html.E(Query(view, view.Month.AddMonths(-1)))}" aria-label="Previous month" title="Previous month">{ShellRenderer.Icon("left")}</a>""");
        sb.Append($"""<a class="ant-btn ant-btn-default ant-btn-icon-only" href="{url}{Html.E(Query(view, view.Month.AddMonths(1)))}" aria-label="Next month" title="Next month">{ShellRenderer.Icon("right")}</a>""");
        sb.Append("</div>");
        sb.Append($"""<a class="ant-btn ant-btn-default" href="{url}{Html.E(Query(view, new DateTime(today.Year, today.Month, 1)))}"><span>Today</span></a>""");
        sb.Append($"""<h2 class="m-cal-title">{Html.E(view.Title)}</h2><span class="m-spacer"></span>""");
        sb.Append($"""<div class="ant-segmented" role="group" aria-label="View"><div class="ant-segmented-group">""");
        sb.Append($"""<a class="ant-segmented-item{(view.List ? "" : " ant-segmented-item-selected")}" href="{url}{Html.E(Query(view, list: false))}"{(view.List ? "" : " aria-current=\"page\"")}><div class="ant-segmented-item-label">Month</div></a>""");
        sb.Append($"""<a class="ant-segmented-item{(view.List ? " ant-segmented-item-selected" : "")}" href="{url}{Html.E(Query(view, list: true))}"{(view.List ? " aria-current=\"page\"" : "")}><div class="ant-segmented-item-label">List</div></a>""");
        sb.Append("</div></div></div>");

        if (view.List) Agenda(sb, view, url, today);
        else Grid(sb, view, url, today);
        sb.Append("</section>");
    }

    private static void Agenda(StringBuilder sb, SchedulerView view, string url, DateTime today)
    {
        var agenda = view.Agenda.ToList();
        if (agenda.Count == 0) sb.Append("""<div class="m-table-empty" style="padding:64px 0">No events this month</div>""");
        foreach (var (day, events) in agenda)
        {
            sb.Append($"""<div class="m-cal-agenda-day{(day == today ? " today" : "")}"><div class="m-cal-agenda-date">{day.ToString("ddd, MMM d", System.Globalization.CultureInfo.InvariantCulture)}{(day == today ? " · Today" : "")}</div><div>""");
            foreach (var e in events)
            {
                sb.Append($"""<button type="button" class="m-cal-agenda-row" @click="$store.drawer.load('{url}/form/{e.Id}', 'Edit event')"><time>{Html.E(SchedulerModel.TimeLabel(e))}</time><span class="m-app-dot" style="--c:{Html.E(view.ColorOf(e.Category))}"></span><span class="m-heading m-medium">{Html.E(e.Title)}</span>""");
                if (e.Location.Length > 0) sb.Append($"""<span class="m-small m-muted m-truncate">{Html.E(e.Location)}</span>""");
                sb.Append("</button>");
            }
            sb.Append("</div></div>");
        }
    }

    private static void Grid(StringBuilder sb, SchedulerView view, string url, DateTime today)
    {
        sb.Append("""<div class="m-cal-head" aria-hidden="true">""");
        foreach (var d in new[] { "Sun", "Mon", "Tue", "Wed", "Thu", "Fri", "Sat" }) sb.Append($"<div>{d}</div>");
        sb.Append("</div><div class=\"m-cal-grid\">");
        foreach (var day in view.Days)
        {
            var events = view.On(day).ToList();
            var label = Html.E(day.ToString("MMMM d", System.Globalization.CultureInfo.InvariantCulture));
            var cell = "m-cal-cell" + (day.Month != view.Month.Month ? " other" : "") + (day == today ? " today" : "");
            sb.Append($"""<div class="{cell}"><button type="button" class="m-cal-add" aria-label="Add event on {label}" @click="$store.drawer.load('{url}/form?date={day:yyyy-MM-dd}', 'Add event')"></button><span class="m-cal-num">{day.Day}</span>""");
            foreach (var e in events.Take(3))
            {
                sb.Append($"""<button type="button" class="m-cal-event{(e.AllDay ? " allday" : "")}" style="--c:{Html.E(view.ColorOf(e.Category))}" title="{Html.E(e.Title)}" @click="$store.drawer.load('{url}/form/{e.Id}', 'Edit event')">""");
                if (!e.AllDay) sb.Append($"""<span class="m-cal-dot"></span><time>{Html.E(SchedulerModel.TimeLabel(e))}</time>""");
                sb.Append($"<span>{Html.E(e.Title)}</span></button>");
            }
            if (events.Count > 3) sb.Append($"""<span class="m-cal-more">+{events.Count - 3} more</span>""");
            sb.Append("</div>");
        }
        sb.Append("</div>");
    }

    private static string Query(SchedulerView view, DateTime? month = null, bool? list = null) =>
        SchedulerModel.QueryString(view.Query with { Month = month ?? view.Month, List = list ?? view.List }, view.Categories.Select(c => c.Name).ToList());
}
