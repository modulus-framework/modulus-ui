using System.Text.Json;
using System.Text.Json.Serialization;

namespace Modulus.Ui.Abstractions.Flow;

/// <summary>How a node did in the last run; the editor draws a ring and an icon for each (n8n and Zapier style).</summary>
public enum FlowStatus
{
    None,
    Waiting,
    Running,
    Success,
    Error,
}

/// <summary>A box on the canvas. <see cref="Type"/> is the key of a <see cref="FlowNodeType"/>; <see cref="Config"/> holds the node's own settings.</summary>
public sealed record FlowNode(string Id, string Type, double X, double Y)
{
    /// <summary>Shown instead of the type's title ("Summarise ticket" rather than "AI chat model").</summary>
    public string? Title { get; init; }

    /// <summary>The node's settings (a prompt, a URL, a cron expression): the app reads and edits them, the editor only carries them.</summary>
    public IReadOnlyDictionary<string, string> Config { get; init; } = new Dictionary<string, string>();

    /// <summary>A disabled node stays on the canvas but is skipped when the workflow runs.</summary>
    public bool Disabled { get; init; }

    public FlowStatus Status { get; init; }

    /// <summary>A short remark under the title, such as the number of items of the last run.</summary>
    public string? Note { get; init; }
}

/// <summary>A connection from output port <see cref="FromPort"/> of one node to the input of another.</summary>
/// <summary>The loop node (<c>logic.loop</c>): its body is the chain from its "each item" output back into it, and its items leave on "done".</summary>
public static class FlowLoop
{
    public const string NodeType = "logic.loop";
    public const string BodyPort = "each-item";
    public const string DonePort = "done";

    /// <summary>The most iterations a loop may be set to run.</summary>
    public const int MaxIterations = 50;
}

/// <summary>One node of a loop body and the output port it hands items on to the next node (the last one hands them back to the loop).</summary>
public sealed record FlowLoopStep(FlowNode Node, string Port);

public sealed record FlowEdge(string Id, string From, string To)
{
    /// <summary>The output port of <see cref="From"/>; "out" for a node with one output.</summary>
    public string FromPort { get; init; } = "out";
}

/// <summary>One finding of <see cref="FlowGraph.Validate"/>.</summary>
public sealed record FlowIssue(FlowIssueLevel Level, string? NodeId, string Message);

public enum FlowIssueLevel
{
    Warning,
    Error,
}

/// <summary>
/// A workflow: nodes and the connections between them. It is what the editor edits, what a form posts and what an engine runs.
/// The JSON form (<see cref="ToJson"/>, <see cref="TryParse"/>) is the same in Blazor, MVC and Razor Pages.
/// </summary>
public sealed record FlowGraph(IReadOnlyList<FlowNode> Nodes, IReadOnlyList<FlowEdge> Edges)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public static FlowGraph Empty { get; } = new([], []);

    public FlowNode? Node(string id) => Nodes.FirstOrDefault(n => n.Id == id);

    public IEnumerable<FlowEdge> Outgoing(string id) => Edges.Where(e => e.From == id);

    public IEnumerable<FlowEdge> Incoming(string id) => Edges.Where(e => e.To == id);

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>Reads what <see cref="ToJson"/> or the editor wrote. False for empty text, bad JSON or a document without nodes and edges.</summary>
    public static bool TryParse(string? json, out FlowGraph graph)
    {
        graph = Empty;
        if (string.IsNullOrWhiteSpace(json) || json.TrimStart()[0] != '{') return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<FlowGraph>(json, Json);
            if (parsed is null) return false;
            graph = new FlowGraph(parsed.Nodes ?? [], parsed.Edges ?? []);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>Checks the graph against a catalog: unknown types, broken connections, triggers with inputs, unreachable nodes and loops.</summary>
    public IReadOnlyList<FlowIssue> Validate(FlowCatalog catalog)
    {
        var issues = new List<FlowIssue>();
        var ids = new HashSet<string>();
        foreach (var n in Nodes)
        {
            if (string.IsNullOrWhiteSpace(n.Id)) issues.Add(new(FlowIssueLevel.Error, null, "A node has no id."));
            else if (!ids.Add(n.Id)) issues.Add(new(FlowIssueLevel.Error, n.Id, $"Two nodes share the id '{n.Id}'."));
            if (catalog.Find(n.Type) is null) issues.Add(new(FlowIssueLevel.Error, n.Id, $"'{Label(n)}' has an unknown type '{n.Type}'."));
        }

        var edgeIds = new HashSet<string>();
        var seen = new HashSet<(string, string, string)>();
        foreach (var e in Edges)
        {
            if (!edgeIds.Add(e.Id)) issues.Add(new(FlowIssueLevel.Error, null, $"Two connections share the id '{e.Id}'."));
            var from = Node(e.From);
            var to = Node(e.To);
            if (from is null || to is null)
            {
                issues.Add(new(FlowIssueLevel.Error, from is null ? e.To : e.From, $"A connection leads {(from is null ? "from" : "to")} a node that does not exist."));
                continue;
            }
            if (e.From == e.To) issues.Add(new(FlowIssueLevel.Error, e.From, $"'{Label(from)}' is connected to itself."));
            if (!seen.Add((e.From, e.FromPort, e.To))) issues.Add(new(FlowIssueLevel.Warning, e.From, $"'{Label(from)}' is connected to '{Label(to)}' twice."));
            if (catalog.Find(from.Type) is { } ft && ft.Outputs.All(p => p.Id != e.FromPort))
                issues.Add(new(FlowIssueLevel.Error, e.From, $"'{Label(from)}' has no output '{e.FromPort}'."));
            if (catalog.Find(to.Type) is { HasInput: false })
                issues.Add(new(FlowIssueLevel.Error, e.To, $"'{Label(to)}' is a trigger and cannot take an input."));
        }

        var triggers = Nodes.Where(n => catalog.Find(n.Type) is { HasInput: false }).ToList();
        if (Nodes.Count > 0 && triggers.Count == 0) issues.Add(new(FlowIssueLevel.Warning, null, "The workflow has no trigger, so nothing starts it."));

        // Reached from a trigger by following the connections; a node outside that set never runs.
        var reached = new HashSet<string>();
        var queue = new Queue<string>(triggers.Select(t => t.Id));
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!reached.Add(id)) continue;
            foreach (var e in Outgoing(id)) queue.Enqueue(e.To);
        }
        if (triggers.Count > 0)
            foreach (var n in Nodes.Where(n => !reached.Contains(n.Id) && catalog.Find(n.Type) is { HasInput: true }))
                issues.Add(new(FlowIssueLevel.Warning, n.Id, $"'{Label(n)}' is not connected to a trigger, so it never runs."));

        if (HasLoop()) issues.Add(new(FlowIssueLevel.Warning, null, "The workflow contains a loop; make sure something ends it."));
        foreach (var loop in Nodes.Where(n => n.Type == FlowLoop.NodeType).DistinctBy(n => n.Id))
            if (LoopBody(loop.Id, out var loopError) is null) issues.Add(new(FlowIssueLevel.Error, loop.Id, loopError));
        return issues;
    }

    /// <summary>
    /// The connections that close a loop: from a node of a loop's body back into that loop. They are not part of the run order;
    /// the loop node runs its body instead.
    /// </summary>
    public IReadOnlyList<FlowEdge> LoopBackEdges()
    {
        var result = new List<FlowEdge>();
        foreach (var loop in Nodes.Where(n => n.Type == FlowLoop.NodeType).Select(n => n.Id).Distinct())
        {
            var body = new HashSet<string>();
            var queue = new Queue<string>(Edges.Where(e => e.From == loop && e.FromPort == FlowLoop.BodyPort && e.To != loop).Select(e => e.To));
            while (queue.Count > 0)
            {
                var id = queue.Dequeue();
                if (id == loop || !body.Add(id)) continue;
                foreach (var e in Edges.Where(e => e.From == id)) queue.Enqueue(e.To);
            }
            result.AddRange(Edges.Where(e => e.To == loop && body.Contains(e.From)));
        }
        return result;
    }

    /// <summary>
    /// The body of a loop as a chain of nodes, each handing items to the next, the last one back into the loop. Null with
    /// <paramref name="error"/> when the body is not one chain: no connection or more than one from the body port, a branch
    /// inside the body, or a body that does not come back to the loop.
    /// </summary>
    public IReadOnlyList<FlowLoopStep>? LoopBody(string loopId, out string? error)
    {
        error = null;
        var starts = Edges.Where(e => e.From == loopId && e.FromPort == FlowLoop.BodyPort).ToList();
        if (starts.Count != 1)
        {
            error = "Connect the loop's \"Each item\" output to the first node of its body (one connection).";
            return null;
        }
        var byId = Nodes.GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.First());
        var chain = new List<FlowLoopStep>();
        var seen = new HashSet<string>();
        var current = starts[0].To;
        while (true)
        {
            if (!byId.TryGetValue(current, out var node))
            {
                error = "The loop body connects to a node that does not exist.";
                return null;
            }
            if (!seen.Add(current))
            {
                error = $"The loop body loops back into \"{current}\" before it reaches the loop.";
                return null;
            }
            var outgoing = Edges.Where(e => e.From == current).ToList();
            if (outgoing.Count != 1)
            {
                error = $"The loop body must be one chain: \"{current}\" has {outgoing.Count} outgoing connections.";
                return null;
            }
            chain.Add(new FlowLoopStep(node, outgoing[0].FromPort));
            if (outgoing[0].To == loopId) return chain;
            current = outgoing[0].To;
        }
    }

    /// <summary>
    /// The nodes in a valid run order (every node after the ones that feed it), found by following the connections from the triggers.
    /// Nodes on a loop, and nodes no trigger reaches, are left out.
    /// </summary>
    public IReadOnlyList<FlowNode> ExecutionOrder(FlowCatalog catalog)
    {
        var byId = Nodes.GroupBy(n => n.Id).ToDictionary(g => g.Key, g => g.First());
        var back = LoopBackEdges().ToHashSet();
        var edges = Edges.Where(e => byId.ContainsKey(e.From) && byId.ContainsKey(e.To) && e.From != e.To && !back.Contains(e)).ToList();
        var reached = new HashSet<string>();
        var queue = new Queue<string>(Nodes.Where(n => catalog.Find(n.Type) is { HasInput: false }).Select(n => n.Id));
        while (queue.Count > 0)
        {
            var id = queue.Dequeue();
            if (!reached.Add(id)) continue;
            foreach (var e in edges.Where(e => e.From == id)) queue.Enqueue(e.To);
        }
        var indegree = reached.ToDictionary(id => id, id => edges.Count(e => e.To == id && reached.Contains(e.From)));
        var ready = new Queue<string>(Nodes.Where(n => reached.Contains(n.Id) && indegree[n.Id] == 0).Select(n => n.Id));
        var order = new List<FlowNode>();
        while (ready.Count > 0)
        {
            var id = ready.Dequeue();
            order.Add(byId[id]);
            foreach (var e in edges.Where(e => e.From == id && reached.Contains(e.To)))
                if (--indegree[e.To] == 0) ready.Enqueue(e.To);
        }
        return order;
    }

    /// <summary>
    /// Lays the nodes out left to right in columns by their longest path from the start, top to bottom within a column in the
    /// order they are listed. Positions only: nothing else changes.
    /// </summary>
    public FlowGraph AutoLayout(double columnGap = 300, double rowGap = 120, double originX = 40, double originY = 40)
    {
        if (Nodes.Count == 0) return this;
        var ids = Nodes.Select(n => n.Id).Distinct().ToList();
        var index = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        var edges = Edges.Where(e => index.ContainsKey(e.From) && index.ContainsKey(e.To) && e.From != e.To).ToList();

        // Break loops first: a link back to a node still on the path is ignored for the layout.
        var state = new int[ids.Count];
        var back = new HashSet<FlowEdge>();
        void Walk(int n)
        {
            state[n] = 1;
            foreach (var e in edges.Where(e => index[e.From] == n))
            {
                var t = index[e.To];
                if (state[t] == 1) back.Add(e);
                else if (state[t] == 0) Walk(t);
            }
            state[n] = 2;
        }
        var hasIncoming = edges.Select(e => e.To).ToHashSet();
        foreach (var id in ids.Where(i => !hasIncoming.Contains(i)).Concat(ids)) if (state[index[id]] == 0) Walk(index[id]);

        var forward = edges.Where(e => !back.Contains(e)).ToList();
        var column = new int[ids.Count];
        var indegree = new int[ids.Count];
        foreach (var e in forward) indegree[index[e.To]]++;
        var queue = new Queue<int>(Enumerable.Range(0, ids.Count).Where(i => indegree[i] == 0));
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            foreach (var e in forward.Where(e => index[e.From] == n))
            {
                var t = index[e.To];
                column[t] = Math.Max(column[t], column[n] + 1);
                if (--indegree[t] == 0) queue.Enqueue(t);
            }
        }

        var row = new Dictionary<int, int>();
        var placed = Nodes.Select(n =>
        {
            var c = column[index[n.Id]];
            var r = row.GetValueOrDefault(c);
            row[c] = r + 1;
            return n with { X = originX + c * columnGap, Y = originY + r * rowGap };
        }).ToList();
        return this with { Nodes = placed };
    }

    private bool HasLoop()
    {
        var ids = Nodes.Select(n => n.Id).Distinct().ToHashSet();
        var back = LoopBackEdges().ToHashSet();
        var next = Edges.Where(e => ids.Contains(e.From) && ids.Contains(e.To) && !back.Contains(e)).GroupBy(e => e.From).ToDictionary(g => g.Key, g => g.Select(e => e.To).ToList());
        var state = new Dictionary<string, int>();
        bool Visit(string id)
        {
            state[id] = 1;
            foreach (var t in next.GetValueOrDefault(id) ?? [])
            {
                var s = state.GetValueOrDefault(t);
                if (s == 1 || (s == 0 && Visit(t))) return true;
            }
            state[id] = 2;
            return false;
        }
        return ids.Any(id => state.GetValueOrDefault(id) == 0 && Visit(id));
    }

    private static string Label(FlowNode n) => string.IsNullOrWhiteSpace(n.Title) ? n.Id : n.Title;
}
