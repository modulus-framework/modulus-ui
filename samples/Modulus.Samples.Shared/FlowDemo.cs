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

    private static readonly string[] Negative = ["refund", "late", "broken", "angry", "terrible", "cancel", "damaged", "worst", "never arrived"];
    private static readonly string[] Positive = ["thank", "great", "love", "awesome", "perfect", "happy"];

    /// <summary>
    /// The engine with stand-ins for the nodes that would call outside systems: a keyword sentiment classifier for the "AI" node, prompts
    /// that fill in the item, a tiny help-centre search, an approval that rejects threats of legal action, and email and chat that only
    /// say what they would send. An app registers real ones (a model client, an SMTP sender) the same way.
    /// </summary>
    public static FlowRunner Runner() => new FlowRunner()
        .On("ai.classify", (ctx, _) =>
        {
            var positive = new List<FlowItem>();
            var negative = new List<FlowItem>();
            var other = new List<FlowItem>();
            foreach (var item in ctx.Items)
            {
                var text = item.Text("message").ToLowerInvariant();
                (Negative.Any(text.Contains) ? negative : Positive.Any(text.Contains) ? positive : other).Add(item);
            }
            return Task.FromResult(new FlowResult { Note = $"{positive.Count} positive, {negative.Count} negative, {other.Count} other" }
                .To("positive", positive).To("negative", negative).To("other", other));
        })
        .OnEach("ai.chat", (ctx, item, _) => Task.FromResult(item.With("reply", $"[{ctx.Setting("model")}] {ctx.Setting("prompt", item)}")))
        .OnEach("ai.agent", (ctx, item, _) => Task.FromResult(item.With("summary", $"{ctx.Setting("goal")}: \"{item.Text("message")}\"; proposed fix: review the order and offer a refund")))
        .OnEach("ai.vector", (ctx, item, _) =>
        {
            var text = item.Text("message").ToLowerInvariant();
            var articles = new List<object?> { text.Contains("order") || text.Contains("late") ? "Where is my order?" : "Getting started", "Returns and refunds" };
            return Task.FromResult(item.With("articles", articles.Take(int.TryParse(ctx.Setting("topK"), out var k) ? Math.Clamp(k, 1, 2) : 2).ToList()));
        })
        .On("ai.human", (ctx, _) =>
        {
            var approved = ctx.Items.Where(i => !i.Text("message").Contains("lawyer", StringComparison.OrdinalIgnoreCase)).ToList();
            var rejected = ctx.Items.Except(approved).ToList();
            return Task.FromResult(new FlowResult { Note = approved.Count > 0 ? "Approved" : "Rejected" }.To("approved", approved).To("rejected", rejected));
        })
        .OnEach("action.email", (ctx, item, _) => Task.FromResult(item.With("emailed", $"To {ctx.Setting("to", item)}: {ctx.Setting("subject", item)}")))
        .OnEach("action.slack", (ctx, item, _) => Task.FromResult(item.With("posted", $"{ctx.Setting("channel")}: {item.Text("message")}")))
        .OnEach("action.respond", (_, item, _) => Task.FromResult(item.With("responded", true)));

    /// <summary>What a customer's chat message looks like when it reaches the trigger.</summary>
    public static FlowItem Message(string text) => FlowItem.Of(("message", text), ("customer", new Dictionary<string, object?> { ["name"] = "Ada Park", ["email"] = "ada@example.com" }));

    public const string SampleMessage = "My order arrived late and I want a refund";

    /// <summary>Runs the workflow for one chat message, reporting each node as it starts and ends.</summary>
    public static async Task<FlowRunResult> Run(FlowGraph graph, string? message, Func<FlowNodeRun, Task>? onStep = null)
    {
        var text = string.IsNullOrWhiteSpace(message) ? SampleMessage : message.Trim();
        var result = await Runner().RunAsync(graph, [Message(text)],
            new FlowRunOptions { OnStep = onStep, NodeTimeout = TimeSpan.FromSeconds(5) });
        Record(result, text);
        return result;
    }

    private static readonly List<FlowRunRecord> _history = [];
    private static readonly object _historyLock = new();

    /// <summary>The last runs, newest first. The sample keeps them in memory; an app keeps them in its own store.</summary>
    public static IReadOnlyList<FlowRunRecord> History()
    {
        lock (_historyLock) return _history.ToList();
    }

    private const int HistoryLength = 10;

    private static void Record(FlowRunResult result, string message)
    {
        lock (_historyLock)
        {
            _history.Insert(0, new FlowRunRecord(DateTimeOffset.UtcNow, message, result.Status, result.Steps.Count, result.Duration));
            if (_history.Count > HistoryLength) _history.RemoveRange(HistoryLength, _history.Count - HistoryLength);
        }
    }

    /// <summary>The words of each finished node for a result list: what it said, or why it failed.</summary>
    public static string Describe(FlowNodeRun step)
    {
        if (step.Error is not null) return step.Error;
        var last = step.Outputs.Values.SelectMany(v => v).FirstOrDefault();
        var shown = last is null ? "" : new[] { "responded", "emailed", "posted", "reply", "summary" }.Select(k => last.Text(k)).FirstOrDefault(t => t.Length > 0) ?? "";
        return shown.Length > 0 ? shown : step.Note ?? "";
    }

    /// <summary>Why this graph can or cannot run, as plain lines for the page.</summary>
    public static IReadOnlyList<FlowIssue> Check(FlowGraph graph) => graph.Validate(FlowCatalog.Default);
}

/// <summary>What the MVC and Razor Pages workflow page shows; <paramref name="Run"/> is set after a run (the canvas then shows each node's status).</summary>
public sealed record FlowView(FlowGraph Graph, IReadOnlyList<FlowIssue> Issues, bool Saved, FlowRunView? Run = null, IReadOnlyList<FlowRunRecord>? History = null);

/// <summary>One run in the history: when, for which message, how it ended, how many nodes ran, and how long it took.</summary>
public sealed record FlowRunRecord(DateTimeOffset At, string Message, FlowStatus Status, int Nodes, TimeSpan Duration);

/// <summary>The result of a run for the MVC and Razor Pages result list: the message that was run and what each node did.</summary>
public sealed record FlowRunView(FlowRunResult Result, string Message);

public static class FlowPage
{
    public static FlowView Show()
    {
        var graph = FlowDemo.Current();
        return new FlowView(graph, FlowDemo.Check(graph), FlowDemo.IsSaved, null, FlowDemo.History());
    }

    /// <summary>Stores what the form posted. False (and nothing stored) when it is not a workflow.</summary>
    public static bool TrySave(string? json, out string message)
    {
        if (!FlowGraph.TryParse(json, out var graph))
        {
            message = "That is not a workflow, so nothing was saved.";
            return false;
        }
        FlowDemo.Save(Clean(graph));
        var errors = FlowDemo.Check(graph).Count(i => i.Level == FlowIssueLevel.Error);
        message = errors == 0 ? $"Workflow saved: {graph.Nodes.Count} nodes, {graph.Edges.Count} connections." : $"Workflow saved with {errors} error{(errors == 1 ? "" : "s")} to fix before it can run.";
        return true;
    }

    /// <summary>Runs the workflow as posted (saved or not) for one message. Null when the post is not a workflow.</summary>
    public static async Task<FlowView?> RunAsync(string? json, string? message)
    {
        if (!FlowGraph.TryParse(json, out var graph)) return null;
        var text = string.IsNullOrWhiteSpace(message) ? FlowDemo.SampleMessage : message.Trim();
        var result = await FlowDemo.Run(graph, text);
        return new FlowView(result.ApplyTo(graph), FlowDemo.Check(graph), FlowDemo.IsSaved, new FlowRunView(result, text), FlowDemo.History());
    }

    /// <summary>The graph without the statuses and remarks of a run, so a saved workflow starts clean.</summary>
    private static FlowGraph Clean(FlowGraph graph) => graph with
    {
        Nodes = graph.Nodes.Select(n => n with { Status = FlowStatus.None, Note = null }).ToList(),
    };
}
