using Bunit;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions.Workflow;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The Blazor workflow kit: the approval timeline's dots and notes, the audit trail's days, rows and filter, and the status badge's tone.</summary>
public class WorkflowComponentTests : BunitContext
{
    private static readonly ApprovalStep[] Steps =
    [
        new("Requested", ApprovalState.Done, "Ada", new DateTime(2026, 10, 6, 9, 5, 0), "Laptops"),
        new("Finance review", ApprovalState.Current, "Margaret"),
        new("Sign-off", ApprovalState.Waiting, "Linus"),
    ];

    [Fact]
    public void ApprovalTimeline_DrawsEachStepInItsToneWithItsNoteAndWho()
    {
        var cut = Render<ApprovalTimeline>(p => p.Add(x => x.Steps, Steps));

        var items = cut.FindAll("li.m-approval-step");
        Assert.Equal(["m-approval-step m-approval-success", "m-approval-step m-approval-processing", "m-approval-step m-approval-warning"], items.Select(i => i.ClassName));
        Assert.Equal(["Approved", "In review", "Waiting"], cut.FindAll(".m-approval-state").Select(s => s.TextContent));
        Assert.Contains("Laptops", cut.Find(".m-approval-note").TextContent);
        Assert.Contains("Oct 6, 2026 · 09:05", items[0].TextContent);
        Assert.Equal("Approval", cut.Find("ol.m-approval").GetAttribute("aria-label"));
    }

    [Fact]
    public void AuditTrail_GroupsByDay_AndAFilterButtonRaisesTheAction()
    {
        string? chosen = "unset";
        var entries = new[]
        {
            new AuditEntry(1, new DateTime(2026, 10, 7, 9, 12, 0), "Ada", "Created", Note: "Created"),
            new AuditEntry(2, new DateTime(2026, 10, 8, 16, 3, 0), "Grace", "Edited", "Total", "4,200", "4,800"),
        };
        var cut = Render<AuditTrail>(p => p
            .Add(x => x.Entries, entries)
            .Add(x => x.ActionChanged, (string? action) => { chosen = action; return Task.CompletedTask; }));

        Assert.Equal(2, cut.FindAll("section.m-audit-day").Count);
        Assert.Equal(["All", "Created", "Edited"], cut.FindAll(".m-audit-filters button").Select(b => b.TextContent));
        Assert.Equal("Total: 4,200 → 4,800", cut.Find(".m-audit-change").TextContent);

        Assert.Equal("true", cut.FindAll(".m-audit-filters button")[0].GetAttribute("aria-pressed"));
        Assert.Equal("false", cut.FindAll(".m-audit-filters button")[2].GetAttribute("aria-pressed"));

        cut.FindAll(".m-audit-filters button")[2].Click();
        Assert.Equal("Edited", chosen);
    }

    [Fact]
    public void AuditTrail_WithNoEntries_SaysSo()
    {
        var cut = Render<AuditTrail>(p => p.Add(x => x.Entries, []));
        Assert.Empty(cut.FindAll("section.m-audit-day"));
        Assert.Contains("No changes recorded", cut.Markup);
    }

    [Fact]
    public void StatusBadge_TakesItsToneAndText()
    {
        var cut = Render<StatusBadge>(p => p.Add(x => x.Tone, "success").AddChildContent("Approved"));

        var badge = cut.Find("span.m-status-badge");
        Assert.Equal("m-status-badge m-status-success", badge.ClassName);
        Assert.Equal("Approved", badge.TextContent.Trim());
        Assert.NotNull(cut.Find("span.m-status-badge > span.m-status-dot"));
    }
}
