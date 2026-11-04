using System.Globalization;
using System.Text.RegularExpressions;
using Modulus.Ui.Abstractions.Flow;

namespace Modulus.Samples.Shared;

public sealed record PurchaseOrderLine(string Sku, int Quantity, decimal UnitPrice);

/// <summary>A buyer's purchase order read from an uploaded document.</summary>
public sealed record BuyerOrder(string Number, string Buyer, IReadOnlyList<PurchaseOrderLine> Lines)
{
    public decimal Total => Lines.Sum(l => l.Quantity * l.UnitPrice);
}

/// <summary>Where a purchase order's approval stands: waiting for a person, approved (the order is created), or rejected.</summary>
public enum PurchaseOrderState
{
    Waiting,
    Approved,
    Rejected,
}

/// <summary>
/// Reads a purchase order from text: a number (PO-1001), a "Buyer:" line, and lines of the form "SKU-100 2 x 49.90". Null when a
/// number or a line is missing, so an ordinary document is not taken for an order.
/// </summary>
public static class PurchaseOrderParser
{
    public static BuyerOrder? Parse(string text)
    {
        var number = Regex.Match(text, @"\bPO[- ]?\d{3,}\b", RegexOptions.IgnoreCase).Value.ToUpperInvariant().Replace(' ', '-');
        if (number.Length == 0) return null;
        var buyer = Regex.Match(text, @"^\s*Buyer:\s*(.+?)\s*$", RegexOptions.Multiline | RegexOptions.IgnoreCase).Groups[1].Value;
        var lines = Regex.Matches(text, @"^\s*([A-Z0-9][A-Z0-9-]{2,})\s+(\d+)\s*(?:x|×)\s*([\d.,]+)\s*$", RegexOptions.Multiline)
            .Select(m => new PurchaseOrderLine(m.Groups[1].Value, int.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture), decimal.Parse(m.Groups[3].Value.Replace(",", ""), CultureInfo.InvariantCulture)))
            .ToList();
        return lines.Count == 0 ? null : new BuyerOrder(number, buyer.Length == 0 ? "Unknown buyer" : buyer, lines);
    }
}

/// <summary>
/// The purchase-order approval: the process as a workflow: record the order, then wait for the buyer's decision at an approval node, then
/// finish on the approved or the rejected branch. The run pauses (Waiting) until a decision is given; giving the decision and
/// running again resumes it from the start with the decision in the item.
/// </summary>
public static class PurchaseOrderFlow
{
    public static readonly FlowGraph Graph = new(
        [
            new FlowNode("start", "trigger.manual", 0, 0),
            new FlowNode("record", "data.set", 0, 0) { Title = "Record the order", Config = new Dictionary<string, string> { ["fields"] = "status = recorded" } },
            new FlowNode("approval", "ai.human", 0, 0) { Title = "Buyer approval" },
            new FlowNode("ordered", "action.respond", 0, 0) { Title = "Create the order" },
            new FlowNode("declined", "action.respond", 0, 0) { Title = "Close as rejected" },
        ],
        [
            new FlowEdge("e1", "start", "record"),
            new FlowEdge("e2", "record", "approval"),
            new FlowEdge("e3", "approval", "ordered") { FromPort = "approved" },
            new FlowEdge("e4", "approval", "declined") { FromPort = "rejected" },
        ]);

    /// <summary>The sample's runner, with the approval node that waits for a decision (the stand-in approval auto-approves).</summary>
    private static FlowRunner Runner() => FlowDemo.Runner().On("ai.human", (ctx, _) =>
    {
        var decision = ctx.Items.Select(i => i.Text("decision")).FirstOrDefault(d => d.Length > 0);
        if (decision is null) return Task.FromResult(new FlowResult { Waiting = true, Note = "Waiting for the buyer's approval" });
        var approved = decision == "approve";
        return Task.FromResult(new FlowResult { Note = approved ? "Approved" : "Rejected" }.To(approved ? "approved" : "rejected", ctx.Items));
    });

    /// <summary>Runs the process for an order with the decision given (null while it waits). Returns where it stands.</summary>
    public static async Task<PurchaseOrderState> RunAsync(BuyerOrder order, string? decision)
    {
        var run = await Runner().RunAsync(Graph, [FlowItem.Of(("po", order.Number), ("buyer", order.Buyer), ("decision", decision ?? ""))]);
        if (run.Step("approval") is { Status: FlowStatus.Waiting }) return PurchaseOrderState.Waiting;
        return run.Step("ordered") is { Status: FlowStatus.Success } ? PurchaseOrderState.Approved : PurchaseOrderState.Rejected;
    }
}

/// <summary>The purchase orders of the sample, and where each one's approval stands (kept in memory; an app keeps them in its database).</summary>
public static class PurchaseOrderBook
{
    private static readonly object Lock = new();
    private static readonly Dictionary<string, (BuyerOrder Order, PurchaseOrderState State)> Orders = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<(BuyerOrder Order, PurchaseOrderState State)> All()
    {
        lock (Lock) return Orders.Values.ToList();
    }

    public static bool TryGet(string number, out BuyerOrder order, out PurchaseOrderState state)
    {
        lock (Lock)
        {
            var found = Orders.TryGetValue(number, out var entry);
            order = entry.Order;
            state = entry.State;
            return found;
        }
    }

    /// <summary>Records a new order as waiting for approval: the process runs up to the approval and pauses.</summary>
    public static async Task<PurchaseOrderState> SubmitAsync(BuyerOrder order)
    {
        var state = await PurchaseOrderFlow.RunAsync(order, null);
        lock (Lock) Orders[order.Number] = (order, state);
        return state;
    }

    /// <summary>Gives the buyer's decision for an order that is waiting, and runs the process on from the approval.</summary>
    public static async Task<PurchaseOrderState?> DecideAsync(string number, bool approve)
    {
        BuyerOrder order;
        lock (Lock)
        {
            if (!Orders.TryGetValue(number, out var entry) || entry.State != PurchaseOrderState.Waiting) return null;
            order = entry.Order;
        }
        var state = await PurchaseOrderFlow.RunAsync(order, approve ? "approve" : "reject");
        lock (Lock) Orders[number] = (order, state);
        return state;
    }
}
