namespace Modulus.Ui.Abstractions.Flow;

/// <summary>One output of a node: a name for the connection and, when a node has several, the label beside the dot ("true", "false").</summary>
public sealed record FlowPort(string Id, string Label = "");

/// <summary>
/// A kind of node the palette offers. A node without an input (<see cref="HasInput"/> false) is a trigger: it starts a run.
/// <see cref="Slot"/> picks the categorical colour (1 to 8) of the nodes of this kind.
/// </summary>
public sealed record FlowNodeType(string Key, string Title, string Category, string Icon)
{
    public string? Description { get; init; }

    public bool HasInput { get; init; } = true;

    public IReadOnlyList<FlowPort> Outputs { get; init; } = [new FlowPort("out")];

    /// <summary>Settings a new node of this kind starts with.</summary>
    public IReadOnlyDictionary<string, string> Defaults { get; init; } = new Dictionary<string, string>();

    public int Slot { get; init; } = 8;

    public bool IsTrigger => !HasInput;
}

/// <summary>The node kinds an editor offers, grouped by category. <see cref="Default"/> is a set for AI and automation workflows.</summary>
public sealed class FlowCatalog(IReadOnlyList<FlowNodeType> types)
{
    public IReadOnlyList<FlowNodeType> Types { get; } = types;

    public FlowNodeType? Find(string? key) => Types.FirstOrDefault(t => t.Key == key);

    /// <summary>The categories in the order they first appear.</summary>
    public IEnumerable<string> Categories => Types.Select(t => t.Category).Distinct();

    private static FlowPort[] Ports(params string[] labels) => labels.Select(l => new FlowPort(l.ToLowerInvariant().Replace(' ', '-'), l)).ToArray();

    private static Dictionary<string, string> Cfg(params (string Key, string Value)[] pairs) => pairs.ToDictionary(p => p.Key, p => p.Value);

    /// <summary>Triggers, AI, logic, data and app actions: enough to draw an agent, a RAG pipeline, an approval flow or an ETL job.</summary>
    public static FlowCatalog Default { get; } = new(
    [
        // Triggers: no input.
        new("trigger.manual", "Manual trigger", "Triggers", "play-circle") { HasInput = false, Slot = 5, Description = "Start the workflow by hand, for testing." },
        new("trigger.webhook", "Webhook", "Triggers", "api") { HasInput = false, Slot = 5, Description = "Start when an HTTP request arrives.", Defaults = Cfg(("method", "POST"), ("path", "/hooks/new")) },
        new("trigger.schedule", "Schedule", "Triggers", "clock-circle") { HasInput = false, Slot = 5, Description = "Start on a timetable.", Defaults = Cfg(("cron", "0 9 * * 1-5")) },
        new("trigger.chat", "Chat message", "Triggers", "message") { HasInput = false, Slot = 5, Description = "Start when a user writes to the assistant." },

        // AI
        new("ai.chat", "AI chat model", "AI", "robot") { Slot = 1, Description = "Send a prompt to a language model.", Defaults = Cfg(("model", "claude-sonnet-5-5"), ("prompt", "")) },
        new("ai.agent", "AI agent", "AI", "experiment") { Slot = 1, Description = "A model that picks tools and loops until the task is done.", Defaults = Cfg(("goal", ""), ("maxSteps", "8")) },
        new("ai.classify", "Text classifier", "AI", "tags") { Slot = 1, Description = "Route text into one of a few categories.", Outputs = Ports("Positive", "Negative", "Other") },
        new("ai.extract", "Extract data", "AI", "file-search") { Slot = 1, Description = "Pull structured fields out of text or a document.", Defaults = Cfg(("schema", "{}")) },
        new("ai.embed", "Embeddings", "AI", "deployment-unit") { Slot = 1, Description = "Turn text into vectors." },
        new("ai.vector", "Vector search", "AI", "search") { Slot = 1, Description = "Find the nearest chunks in a vector store.", Defaults = Cfg(("topK", "5")) },
        new("ai.human", "Human approval", "AI", "user") { Slot = 1, Description = "Pause until a person approves or rejects.", Outputs = Ports("Approved", "Rejected") },

        // Logic
        new("logic.if", "If", "Logic", "branches") { Slot = 3, Description = "Split on a condition.", Outputs = Ports("True", "False"), Defaults = Cfg(("condition", "")) },
        new("logic.switch", "Switch", "Logic", "apartment") { Slot = 3, Description = "Route by value to one of several branches.", Outputs = Ports("Case 1", "Case 2", "Fallback") },
        new("logic.merge", "Merge", "Logic", "merge-cells") { Slot = 3, Description = "Join branches back into one." },
        new("logic.loop", "Loop over items", "Logic", "retweet") { Slot = 3, Description = "Run the next nodes once per item.", Outputs = Ports("Each item", "Done") },
        new("logic.wait", "Wait", "Logic", "hourglass") { Slot = 3, Description = "Pause for a time or until a date.", Defaults = Cfg(("seconds", "60")) },

        // Data
        new("data.http", "HTTP request", "Data", "global") { Slot = 2, Description = "Call any API.", Defaults = Cfg(("method", "GET"), ("url", "")) },
        new("data.set", "Set fields", "Data", "edit") { Slot = 2, Description = "Add or change values on the items." },
        new("data.code", "Code", "Data", "code") { Slot = 2, Description = "Run a script on the items.", Defaults = Cfg(("language", "javascript"), ("source", "")) },
        new("data.database", "Database query", "Data", "database") { Slot = 2, Description = "Read or write rows.", Defaults = Cfg(("query", "")) },
        new("data.filter", "Filter", "Data", "filter") { Slot = 2, Description = "Keep only the items that match." },

        // Actions
        new("action.email", "Send email", "Actions", "mail") { Slot = 4, Description = "Send a message by email.", Defaults = Cfg(("to", ""), ("subject", "")) },
        new("action.slack", "Post to chat", "Actions", "bell") { Slot = 4, Description = "Post to a Slack or Teams channel.", Defaults = Cfg(("channel", "#general")) },
        new("action.respond", "Respond to webhook", "Actions", "send") { Slot = 4, Description = "Answer the request that started the run." },
    ]);
}
