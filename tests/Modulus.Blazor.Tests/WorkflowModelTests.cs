using Modulus.Ui.Abstractions.Workflow;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The workflow kit's models: approval steps given tones and state text, and the audit trail grouped by day and filtered.</summary>
public class WorkflowModelTests
{
    [Fact]
    public void ApprovalSteps_TakeTheirToneAndStateText_AndTheirTimeWhenTheyHaveOne()
    {
        var views = ApprovalTimelineModel.Build(
        [
            new ApprovalStep("Requested", ApprovalState.Done, "Ada", new DateTime(2026, 10, 6, 9, 5, 0)),
            new ApprovalStep("Review", ApprovalState.Current, "Grace"),
            new ApprovalStep("Sign-off", ApprovalState.Waiting),
            new ApprovalStep("Refused", ApprovalState.Rejected),
            new ApprovalStep("Supplier", ApprovalState.Skipped),
        ]);

        Assert.Equal(["success", "processing", "warning", "error", "default"], views.Select(v => v.Tone));
        Assert.Equal(["Approved", "In review", "Waiting", "Rejected", "Skipped"], views.Select(v => v.StateText));
        Assert.Equal("Oct 6, 2026 · 09:05", views[0].WhenText);
        Assert.Equal("", views[1].WhenText);
    }

    [Theory]
    [InlineData(ApprovalState.Done, "success")]
    [InlineData(ApprovalState.Current, "processing")]
    [InlineData(ApprovalState.Rejected, "error")]
    [InlineData(ApprovalState.Waiting, "warning")]
    [InlineData(ApprovalState.Skipped, "default")]
    public void EveryState_HasOneTone(ApprovalState state, string tone) =>
        Assert.Equal(tone, ApprovalTimelineModel.ToneOf(state));

    [Fact]
    public void ApprovalSteps_KeepTheirOrder_AndAnEmptyListIsEmpty()
    {
        var views = ApprovalTimelineModel.Build(
        [
            new ApprovalStep("Second", ApprovalState.Waiting),
            new ApprovalStep("First", ApprovalState.Done),
        ]);
        Assert.Equal(["Second", "First"], views.Select(v => v.Step.Name));
        Assert.Empty(ApprovalTimelineModel.Build([]));
    }

    private static readonly AuditEntry[] Trail =
    [
        new(1, new DateTime(2026, 10, 7, 9, 12, 0), "Ada", "Created", Note: "Purchase order created"),
        new(2, new DateTime(2026, 10, 7, 9, 14, 0), "Ada", "Edited", "Total", "4,200", "4,800"),
        new(3, new DateTime(2026, 10, 8, 16, 3, 0), "Grace", "Approved", Note: "Within the budget"),
        new(4, new DateTime(2026, 10, 8, 16, 3, 0), "Grace", "Edited", "Status", null, "In review"),
        new(5, new DateTime(2026, 10, 9, 8, 40, 0), "Margaret", "Commented", Note: "Checking the freight"),
    ];

    [Fact]
    public void Trail_GroupsByDay_NewestDayAndNewestEntryFirst()
    {
        var view = AuditTrailModel.Build(Trail);

        Assert.Equal([new DateTime(2026, 10, 9), new DateTime(2026, 10, 8), new DateTime(2026, 10, 7)], view.Days.Select(d => d.Day));
        Assert.Equal([5], view.Days[0].Rows.Select(r => r.Entry.Id));
        // Same minute on the 8th: the higher id (the later entry) comes first.
        Assert.Equal([4, 3], view.Days[1].Rows.Select(r => r.Entry.Id));
        Assert.Equal([2, 1], view.Days[2].Rows.Select(r => r.Entry.Id));
        Assert.Equal("09:14", view.Days[2].Rows[0].TimeText);
    }

    [Fact]
    public void Trail_ChangesReadAsFieldBeforeAndAfter_WithNoneForAnEmptySide()
    {
        var rows = AuditTrailModel.Build(Trail).Days.SelectMany(d => d.Rows).ToDictionary(r => r.Entry.Id);

        Assert.Equal("Total: 4,200 → 4,800", rows[2].ChangeText);
        Assert.Equal("Status: none → In review", rows[4].ChangeText);
        Assert.Equal("Within the budget", rows[3].ChangeText);
        Assert.Equal("Purchase order created", rows[1].ChangeText);
    }

    [Fact]
    public void Trail_FilterByAction_IgnoresCase_AndListsTheActions()
    {
        var view = AuditTrailModel.Build(Trail, " edited ");

        Assert.Equal("edited", view.Action);
        Assert.Equal(["Approved", "Commented", "Created", "Edited"], view.Actions);
        Assert.Equal([2, 4], view.Days.SelectMany(d => d.Rows).Select(r => r.Entry.Id).OrderBy(id => id));
        Assert.Null(AuditTrailModel.Build(Trail, "  ").Action);
        Assert.Empty(AuditTrailModel.Build(Trail, "Deleted").Days);
        Assert.Empty(AuditTrailModel.Build([]).Days);
    }
}
