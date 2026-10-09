using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Workflow;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>app-approval-timeline, app-audit-trail and app-status-badge: the same markup as the Blazor components, escaped, and the filter links.</summary>
public class WorkflowTagHelperTests
{
    private static async Task<string> RenderAsync(TagHelper helper, string tag)
    {
        var output = new TagHelperOutput(tag, [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext(tag, [], new Dictionary<object, object>(), "w1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task ApprovalTimeline_DrawsTheStepsWithEscapedText()
    {
        var html = await RenderAsync(new ApprovalTimelineTagHelper
        {
            Steps =
            [
                new ApprovalStep("Finance <review>", ApprovalState.Current, "Margaret", new DateTime(2026, 10, 8, 10, 0, 0), "Freight & tax"),
                new ApprovalStep("Sign-off", ApprovalState.Waiting),
            ],
        }, "app-approval-timeline");

        Assert.Contains("""<ol class="m-approval" aria-label="Approval">""", html);
        Assert.Contains("""<li class="m-approval-step m-approval-processing">""", html);
        Assert.Contains("""<li class="m-approval-step m-approval-warning">""", html);
        Assert.Contains("Finance &lt;review&gt;", html);
        Assert.Contains("<span>Margaret</span><span> ", html);
        Assert.Contains("Oct 8, 2026", html);
        Assert.Contains("Freight &amp; tax", html);
        Assert.Contains("In review", html);
    }

    [Fact]
    public async Task AuditTrail_FiltersByAction_AndLinksEachAction()
    {
        var entries = new[]
        {
            new AuditEntry(1, new DateTime(2026, 10, 7, 9, 12, 0), "Ada", "Created", Note: "Created"),
            new AuditEntry(2, new DateTime(2026, 10, 8, 16, 3, 0), "Grace", "Edited", "Total", "4,200", "4,800"),
        };
        var html = await RenderAsync(new AuditTrailTagHelper { Entries = entries, Action = "Edited", Url = "/apps/workflow" }, "app-audit-trail");

        Assert.Equal(1, CountOf(html, "class=\"m-audit-day\""));   // only the Edited entry's day
        Assert.Contains("""<a class="ant-btn ant-btn-sm ant-btn-default" href="/apps/workflow" aria-pressed="false">All</a>""", html);
        Assert.Contains("""<a class="ant-btn ant-btn-sm ant-btn-primary" href="/apps/workflow?action=Edited" aria-pressed="true">Edited</a>""", html);
        Assert.Contains("""<span class="m-audit-change">Total: 4,200 """, html);   // the arrow is entity-encoded in the output
        Assert.Contains("4,800</span>", html);
        Assert.DoesNotContain("m-audit-change\">Created<", html);
    }

    [Fact]
    public async Task AuditTrail_WithNoEntries_SaysSo()
    {
        var html = await RenderAsync(new AuditTrailTagHelper { Url = "/x" }, "app-audit-trail");
        Assert.Contains("No changes recorded", html);
        Assert.Equal(0, CountOf(html, "class=\"m-audit-day\""));
    }

    [Fact]
    public async Task StatusBadge_TakesItsToneAndTheChildText()
    {
        var helper = new StatusBadgeTagHelper { Tone = "success" };
        var output = new TagHelperOutput("app-status-badge", [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent("Approved")));
        await helper.ProcessAsync(new TagHelperContext("app-status-badge", [], new Dictionary<object, object>(), "s1"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);

        Assert.Equal("""<span class="m-status-badge m-status-success"><span class="m-status-dot" aria-hidden="true"></span>Approved</span>""", writer.ToString());
    }

    private static int CountOf(string html, string text) => (html.Length - html.Replace(text, "").Length) / text.Length;
}
