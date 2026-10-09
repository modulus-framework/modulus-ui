using System.Globalization;

namespace Modulus.Ui.Abstractions.Workflow;

/// <summary>Where an approval step stands: finished, the one in progress, still waiting, refused, or skipped.</summary>
public enum ApprovalState { Done, Current, Waiting, Rejected, Skipped }

/// <summary>
/// One step of an approval: its name, the person who acted (or who it is waiting on), when they acted, the state and an
/// optional note (a reason, a comment).
/// </summary>
public sealed record ApprovalStep(string Name, ApprovalState State, string? Who = null, DateTime? At = null, string? Note = null);

/// <summary>A step ready to draw: its tone (success, processing, error, warning or default), its state's text and its time text.</summary>
public sealed record ApprovalStepView(ApprovalStep Step, string Tone, string StateText, string WhenText);

/// <summary>
/// The approval timeline's model, shared by the Blazor <c>ApprovalTimeline</c> and the MVC <c>app-approval-timeline</c>:
/// each step given its tone, its state's text and its time as "MMM d, yyyy · HH:mm" (empty when it has none).
/// </summary>
public static class ApprovalTimelineModel
{
    /// <summary>Builds the steps in their given order.</summary>
    public static IReadOnlyList<ApprovalStepView> Build(IEnumerable<ApprovalStep> steps) =>
        steps.Select(step => new ApprovalStepView(step, ToneOf(step.State), TextOf(step.State),
            step.At is { } at ? at.ToString("MMM d, yyyy · HH:mm", CultureInfo.InvariantCulture) : "")).ToList();

    /// <summary>The tone a step's dot takes: a finished step is success, the current one processing, a refusal error.</summary>
    public static string ToneOf(ApprovalState state) => state switch
    {
        ApprovalState.Done => "success",
        ApprovalState.Current => "processing",
        ApprovalState.Rejected => "error",
        ApprovalState.Waiting => "warning",
        _ => "default",
    };

    /// <summary>The state as the step's label reads it.</summary>
    public static string TextOf(ApprovalState state) => state switch
    {
        ApprovalState.Done => "Approved",
        ApprovalState.Current => "In review",
        ApprovalState.Rejected => "Rejected",
        ApprovalState.Waiting => "Waiting",
        _ => "Skipped",
    };
}
