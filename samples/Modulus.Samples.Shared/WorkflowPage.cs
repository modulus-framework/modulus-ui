using Modulus.Ui.Abstractions.Workflow;

namespace Modulus.Samples.Shared;

/// <summary>
/// The workflow sample: a purchase order going through approval, with its steps and its audit trail. The dates hang off
/// <c>now</c>, so the sample always shows the same shape: two steps done, one in review, one waiting, one skipped.
/// </summary>
public static class WorkflowPage
{
    /// <summary>The order as the page draws it: its number, status (and tone), approval steps, audit trail and the filter in force.</summary>
    public sealed record Order(string Number, string Status, string StatusTone, IReadOnlyList<ApprovalStep> Steps, IReadOnlyList<AuditEntry> Trail, string? Action);

    /// <summary>The order for <paramref name="now"/>, with the audit trail filtered to <paramref name="action"/> (null for all).</summary>
    public static Order Board(DateTime now, string? action = null)
    {
        var day = now.Date;
        return new Order(
            "PO-2041",
            "In review", "processing",
            [
                new ApprovalStep("Requested", ApprovalState.Done, "Ada Lovelace", day.AddDays(-3).AddHours(9).AddMinutes(12), "Laptops for the new starters"),
                new ApprovalStep("Manager approval", ApprovalState.Done, "Grace Hopper", day.AddDays(-2).AddHours(16).AddMinutes(3), "Within the team budget"),
                new ApprovalStep("Finance review", ApprovalState.Current, "Margaret Hamilton", day.AddDays(-1).AddHours(10), "Checking the freight line"),
                new ApprovalStep("Director sign-off", ApprovalState.Waiting, "Linus Torvalds", Note: "Needed above 5,000"),
                new ApprovalStep("Send to supplier", ApprovalState.Skipped, Note: "Sent by Finance once signed"),
            ],
            [
                new AuditEntry(1, day.AddDays(-3).AddHours(9).AddMinutes(12), "Ada Lovelace", "Created", Note: "Purchase order created"),
                new AuditEntry(2, day.AddDays(-3).AddHours(9).AddMinutes(14), "Ada Lovelace", "Edited", "Total", "4,200", "4,800"),
                new AuditEntry(3, day.AddDays(-2).AddHours(16).AddMinutes(3), "Grace Hopper", "Approved", Note: "Within the team budget"),
                new AuditEntry(4, day.AddDays(-1).AddHours(10), "System", "Routed", Note: "Sent to Finance"),
                new AuditEntry(5, day.AddDays(-1).AddHours(10).AddMinutes(5), "Margaret Hamilton", "Commented", Note: "Checking the freight line"),
                new AuditEntry(6, day.AddHours(8).AddMinutes(40), "Margaret Hamilton", "Edited", "Status", "Requested", "In review"),
            ],
            action);
    }
}
