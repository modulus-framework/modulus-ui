using Modulus.Ui.Abstractions.Flow;

namespace Modulus.Samples.Shared;

/// <summary>Demo data for the workflow designer page, the same in Blazor, MVC and Razor Pages.</summary>
public static class FlowDemo
{
    /// <summary>The workflow the page opens with, until one is saved.</summary>
    private static FlowGraph? _saved;

    /// <summary>An AI support desk: classify a chat message, answer happy customers, escalate unhappy ones for approval, search the help centre for the rest.</summary>
    public static FlowGraph SupportDesk() => new FlowGraph(
    [
        new("chat", "trigger.chat", 0, 0) { Title = "Customer message" },
        new("classify", "ai.classify", 0, 0) { Title = "Classify sentiment", Config = new Dictionary<string, string> { ["categories"] = "positive, negative, other" } },
        new("thanks", "ai.chat", 0, 0) { Title = "Write a thank-you", Config = new Dictionary<string, string> { ["model"] = "claude-haiku-5-5", ["prompt"] = "Thank the customer warmly in two sentences: {{message}}" } },
        new("escalate", "ai.agent", 0, 0) { Title = "Escalation agent", Config = new Dictionary<string, string> { ["goal"] = "Summarise the complaint and propose a fix", ["maxSteps"] = "6" } },
        new("kb", "ai.vector", 0, 0) { Title = "Search help centre", Config = new Dictionary<string, string> { ["topK"] = "5" } },
        new("answer", "ai.chat", 0, 0) { Title = "Draft an answer", Config = new Dictionary<string, string> { ["model"] = "claude-sonnet-5-5", ["prompt"] = "Answer using only these articles: {{articles}}" } },
        new("approve", "ai.human", 0, 0) { Title = "Manager approval" },
        new("mail", "action.email", 0, 0) { Title = "Email the customer", Config = new Dictionary<string, string> { ["to"] = "{{customer.email}}", ["subject"] = "About your request" } },
        new("alert", "action.slack", 0, 0) { Title = "Alert #support", Config = new Dictionary<string, string> { ["channel"] = "#support" } },
        new("reply", "action.respond", 0, 0) { Title = "Reply in chat" },
    ],
    [
        new("e1", "chat", "classify"),
        new("e2", "classify", "thanks") { FromPort = "positive" },
        new("e3", "classify", "escalate") { FromPort = "negative" },
        new("e4", "classify", "kb") { FromPort = "other" },
        new("e5", "kb", "answer"),
        new("e6", "escalate", "approve"),
        new("e7", "approve", "mail") { FromPort = "approved" },
        new("e8", "approve", "alert") { FromPort = "rejected" },
        new("e9", "thanks", "reply"),
        new("e10", "answer", "reply"),
    ]).AutoLayout(rowGap: 110);

    /// <summary>The graph the page shows: the last one saved, or the support desk.</summary>
    public static FlowGraph Current() => _saved ?? SupportDesk();

    /// <summary>Keeps what the editor posted, in memory (a real app stores it in a database).</summary>
    public static void Save(FlowGraph graph) => _saved = graph;

    public static void Reset() => _saved = null;

    public static bool IsSaved => _saved is not null;

    /// <summary>The nodes in the order a run visits them, for the simulated run.</summary>
    public static IReadOnlyList<string> RunOrder(FlowGraph graph) => graph.ExecutionOrder(FlowCatalog.Default).Where(n => !n.Disabled).Select(n => n.Id).ToList();

    /// <summary>Why this graph can or cannot run, as plain lines for the page.</summary>
    public static IReadOnlyList<FlowIssue> Check(FlowGraph graph) => graph.Validate(FlowCatalog.Default);
}

/// <summary>What the MVC and Razor Pages workflow page shows.</summary>
public sealed record FlowView(FlowGraph Graph, IReadOnlyList<FlowIssue> Issues, bool Saved);

public static class FlowPage
{
    public static FlowView Show()
    {
        var graph = FlowDemo.Current();
        return new FlowView(graph, FlowDemo.Check(graph), FlowDemo.IsSaved);
    }

    /// <summary>Stores what the form posted. False (and nothing stored) when it is not a workflow.</summary>
    public static bool TrySave(string? json, out string message)
    {
        if (!FlowGraph.TryParse(json, out var graph))
        {
            message = "That is not a workflow, so nothing was saved.";
            return false;
        }
        FlowDemo.Save(graph);
        var errors = FlowDemo.Check(graph).Count(i => i.Level == FlowIssueLevel.Error);
        message = errors == 0 ? $"Workflow saved: {graph.Nodes.Count} nodes, {graph.Edges.Count} connections." : $"Workflow saved with {errors} error{(errors == 1 ? "" : "s")} to fix before it can run.";
        return true;
    }
}
