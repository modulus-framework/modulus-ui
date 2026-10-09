using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Workflow;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// An approval's steps as a timeline (<see cref="ApprovalTimelineModel"/>), the MVC front end of the Blazor <c>ApprovalTimeline</c>:
/// <code>&lt;app-approval-timeline steps="@Model.Steps" /&gt;</code>
/// </summary>
[HtmlTargetElement("app-approval-timeline", TagStructure = TagStructure.WithoutEndTag)]
public class ApprovalTimelineTagHelper : TagHelper
{
    /// <summary>The steps, in order.</summary>
    public IEnumerable<ApprovalStep>? Steps { get; set; }

    /// <summary>The label for screen readers. Defaults to "Approval".</summary>
    public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var sb = new StringBuilder();
        sb.Append($"""<ol class="m-approval" aria-label="{Html.E(AriaLabel ?? "Approval")}">""");
        foreach (var view in ApprovalTimelineModel.Build(Steps ?? []))
        {
            var step = view.Step;
            var who = step.Who is { Length: > 0 } w ? Html.E(w) : null;
            sb.Append($"""<li class="m-approval-step m-approval-{view.Tone}"><span class="m-approval-dot" aria-hidden="true"></span><div class="m-approval-body">""");
            sb.Append($"""<div class="m-approval-head"><span class="m-heading m-medium">{Html.E(step.Name)}</span><span class="m-approval-state">{Html.E(view.StateText)}</span></div>""");
            sb.Append("""<div class="m-small m-muted">""");
            if (who is not null) sb.Append($"<span>{who}</span>");
            if (view.WhenText.Length > 0) sb.Append($"<span>{(who is not null ? " · " : "")}{Html.E(view.WhenText)}</span>");
            sb.Append("</div>");
            if (step.Note is { Length: > 0 }) sb.Append($"""<div class="m-small m-approval-note">{Html.E(step.Note)}</div>""");
            sb.Append("</div></li>");
        }
        sb.Append("</ol>");
        output.TagName = null;
        output.Content.SetHtmlContent(sb.ToString());
    }
}

/// <summary>
/// A record's audit trail (<see cref="AuditTrailModel"/>), the MVC front end of the Blazor <c>AuditTrail</c>:
/// <code>&lt;app-audit-trail entries="@Model.Entries" action="@Model.Action" url="/apps/workflow" /&gt;</code>
/// The action filters are links to <c>{url}?action=…</c> (or <c>{url}</c> for All), so the page reloads with the filter.
/// </summary>
[HtmlTargetElement("app-audit-trail", TagStructure = TagStructure.WithoutEndTag)]
public class AuditTrailTagHelper : TagHelper
{
    /// <summary>The entries to show, in any order.</summary>
    public IEnumerable<AuditEntry>? Entries { get; set; }

    /// <summary>The action the list is filtered to (null for all).</summary>
    public string? Action { get; set; }

    /// <summary>The page the filter links go to (its path, with the query added).</summary>
    public string Url { get; set; } = "";

    /// <summary>The label for screen readers. Defaults to "Audit trail".</summary>
    public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var view = AuditTrailModel.Build(Entries ?? [], Action);
        var url = Html.E(Url);
        var sb = new StringBuilder();
        sb.Append($"""<div class="m-audit" aria-label="{Html.E(AriaLabel ?? "Audit trail")}"><div class="m-audit-filters" role="group" aria-label="Filter by action">""");
        sb.Append($"""<a class="ant-btn ant-btn-sm {(view.Action is null ? "ant-btn-primary" : "ant-btn-default")}" href="{url}" aria-pressed="{(view.Action is null ? "true" : "false")}">All</a>""");
        foreach (var action in view.Actions)
        {
            var selected = string.Equals(view.Action, action, StringComparison.OrdinalIgnoreCase);
            sb.Append($"""<a class="ant-btn ant-btn-sm {(selected ? "ant-btn-primary" : "ant-btn-default")}" href="{url}?action={Uri.EscapeDataString(action)}" aria-pressed="{(selected ? "true" : "false")}">{Html.E(action)}</a>""");
        }
        sb.Append("</div>");
        if (view.Days.Count == 0)
            sb.Append("""<div class="m-table-empty" style="padding:32px 0">No changes recorded</div>""");
        foreach (var day in view.Days)
        {
            sb.Append($"""<section class="m-audit-day"><h3 class="m-audit-date">{Html.E(day.Day.ToString("ddd, MMM d, yyyy", System.Globalization.CultureInfo.InvariantCulture))}</h3><ul class="m-audit-list">""");
            foreach (var row in day.Rows)
            {
                sb.Append($"""<li class="m-audit-row"><time>{Html.E(row.TimeText)}</time><span class="m-audit-action">{Html.E(row.Entry.Action)}</span><span class="m-audit-actor">{Html.E(row.Entry.Actor)}</span>""");
                if (row.ChangeText.Length > 0) sb.Append($"""<span class="m-audit-change">{Html.E(row.ChangeText)}</span>""");
                sb.Append("</li>");
            }
            sb.Append("</ul></section>");
        }
        sb.Append("</div>");
        output.TagName = null;
        output.Content.SetHtmlContent(sb.ToString());
    }
}

/// <summary>
/// A status as a tinted pill (the Blazor <c>StatusBadge</c>): <code>&lt;app-status-badge tone="success"&gt;Approved&lt;/app-status-badge&gt;</code>
/// </summary>
[HtmlTargetElement("app-status-badge")]
public class StatusBadgeTagHelper : TagHelper
{
    /// <summary>The tone: success, processing, error, warning or default.</summary>
    public string Tone { get; set; } = "default";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var body = await output.GetChildContentAsync();
        output.TagName = "span";
        output.Attributes.SetAttribute("class", $"m-status-badge m-status-{Tone}");
        output.Content.SetHtmlContent($"""<span class="m-status-dot" aria-hidden="true"></span>{body.GetContent()}""");
    }
}
