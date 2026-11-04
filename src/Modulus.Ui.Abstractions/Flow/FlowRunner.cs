using System.Diagnostics;

namespace Modulus.Ui.Abstractions.Flow;

/// <summary>What a node receives: the node, its kind, and the items that reached it (from every connection that carried any).</summary>
public sealed record FlowContext(FlowNode Node, FlowNodeType Type, IReadOnlyList<FlowItem> Items)
{
    /// <summary>The limits of the run this node is part of.</summary>
    public FlowRunOptions Options { get; init; } = new();

    /// <summary>A setting of the node, with <c>{{path}}</c> filled in from <paramref name="item"/> (or as written when there is none).</summary>
    public string Setting(string key, FlowItem? item = null)
    {
        var raw = Node.Config.TryGetValue(key, out var v) ? v : Type.Defaults.TryGetValue(key, out var d) ? d : "";
        return item is null ? FlowTemplate.RenderSecrets(raw, Options.Secret) : FlowTemplate.Render(raw, item, Options.Secret);
    }
}

/// <summary>What a node hands on: items per output port (a port with none is a branch that did not fire), or a wait.</summary>
public sealed class FlowResult
{
    private readonly Dictionary<string, IReadOnlyList<FlowItem>> _outputs = [];

    public IReadOnlyDictionary<string, IReadOnlyList<FlowItem>> Outputs => _outputs;

    /// <summary>A line the canvas shows under the node's title ("3 items", "refund request").</summary>
    public string? Note { get; init; }

    /// <summary>The node paused the run (waiting for a person, a timer, an outside event); nothing after it runs this time.</summary>
    public bool Waiting { get; init; }

    /// <summary>Items out of the node's only (or first) output.</summary>
    public static FlowResult Out(IEnumerable<FlowItem> items, string? note = null) => new FlowResult { Note = note }.To("out", items);

    /// <summary>Items out of one named port; chain <see cref="To"/> for several.</summary>
    public FlowResult To(string port, IEnumerable<FlowItem> items)
    {
        _outputs[port] = items.ToList();
        return this;
    }

    public static FlowResult Wait(string? note = null) => new() { Waiting = true, Note = note };
}

public delegate Task<FlowResult> FlowHandler(FlowContext context, CancellationToken cancellation);

/// <summary>One node's part of a run.</summary>
public sealed record FlowNodeRun(string NodeId, FlowStatus Status, int ItemsOut, string? Note, string? Error, TimeSpan Duration,
    IReadOnlyDictionary<string, IReadOnlyList<FlowItem>> Outputs);

/// <summary>The outcome of a run: <see cref="FlowStatus.Success"/>, <see cref="FlowStatus.Error"/> (a node failed) or <see cref="FlowStatus.Waiting"/>.</summary>
public sealed record FlowRunResult(IReadOnlyList<FlowNodeRun> Steps, FlowStatus Status, TimeSpan Duration)
{
    public FlowNodeRun? Step(string nodeId) => Steps.LastOrDefault(s => s.NodeId == nodeId);

    /// <summary>The graph with each node's status and note set from this run, ready for the editor (read-only view of a past run).</summary>
    public FlowGraph ApplyTo(FlowGraph graph) => graph with
    {
        Nodes = graph.Nodes.Select(n => Step(n.Id) is { } s ? n with { Status = s.Status, Note = s.Error ?? s.Note ?? n.Note } : n with { Status = FlowStatus.None }).ToList(),
    };
}

/// <summary>Limits and reporting of one run.</summary>
public sealed record FlowRunOptions
{
    /// <summary>Called as each node starts (<see cref="FlowStatus.Running"/>) and again when it ends: the page paints the canvas from it.</summary>
    public Func<FlowNodeRun, Task>? OnStep { get; init; }

    /// <summary>Stop everything after the first failed node. By default only the failed node's own branch stops.</summary>
    public bool StopOnError { get; init; }

    /// <summary>Items a node may hand on in total; more fails the node (a guard against a node that multiplies items without end).</summary>
    public int MaxItems { get; init; } = 10_000;

    /// <summary>The longest a single node may take; null for no limit besides the cancellation token.</summary>
    public TimeSpan? NodeTimeout { get; init; }

    /// <summary>
    /// Resolves <c>{{secret.name}}</c> in node settings: the app returns the value (from its configuration or vault), or null
    /// when it has none, which fails the node. Secret values are never written into a graph or an output.
    /// </summary>
    public Func<string, string?>? Secret { get; init; }

    /// <summary>The longest a Wait node waits, whatever it is set to.</summary>
    public TimeSpan MaxWait { get; init; } = TimeSpan.FromSeconds(10);
}

/// <summary>
/// Runs a <see cref="FlowGraph"/>: from the triggers, each node once, in an order where every node comes after the ones that feed it.
/// What a node does is a handler you register for its kind (<see cref="On"/>); the control-flow nodes (If, Switch, Merge, Wait, Set fields,
/// Filter, triggers) are built in. A node receives the items from every connection that carried some; a connection from an output that did
/// not fire carries none, so a branch not taken is skipped. A disabled node passes its items straight on. Loops are not followed (the
/// nodes on one do not run), as <see cref="FlowGraph.ExecutionOrder"/> defines.
/// </summary>
public sealed class FlowRunner
{
    private readonly FlowCatalog _catalog;
    private readonly Dictionary<string, FlowHandler> _handlers = [];

    public FlowRunner(FlowCatalog? catalog = null, bool builtIns = true)
    {
        _catalog = catalog ?? FlowCatalog.Default;
        if (builtIns) AddBuiltIns();
    }

    /// <summary>Registers (or replaces) what nodes of this kind do.</summary>
    public FlowRunner On(string type, FlowHandler handler)
    {
        _handlers[type] = handler;
        return this;
    }

    /// <summary>Registers a handler that works one item at a time.</summary>
    public FlowRunner OnEach(string type, Func<FlowContext, FlowItem, CancellationToken, Task<FlowItem>> handler, string? note = null) =>
        On(type, async (ctx, ct) =>
        {
            var outputs = new List<FlowItem>(ctx.Items.Count);
            foreach (var item in ctx.Items) outputs.Add(await handler(ctx, item, ct));
            return FlowResult.Out(outputs, note);
        });

    public bool Handles(string type) => _handlers.ContainsKey(type);

    /// <summary>Runs the graph once. <paramref name="input"/> is what the triggers hand on (one empty item when omitted).</summary>
    public async Task<FlowRunResult> RunAsync(FlowGraph graph, IEnumerable<FlowItem>? input = null, FlowRunOptions? options = null, CancellationToken cancellation = default)
    {
        var o = options ?? new FlowRunOptions();
        var start = (input ?? [FlowItem.Empty]).ToList();
        var clock = Stopwatch.StartNew();
        var steps = new List<FlowNodeRun>();
        var produced = new Dictionary<string, IReadOnlyDictionary<string, IReadOnlyList<FlowItem>>>();
        var status = FlowStatus.Success;
        var total = 0;

        async Task Report(FlowNodeRun step)
        {
            if (o.OnStep is not null) await o.OnStep(step);
        }

        foreach (var node in graph.ExecutionOrder(_catalog))
        {
            cancellation.ThrowIfCancellationRequested();
            var type = _catalog.Find(node.Type);
            if (type is null)
            {
                var unknown = new FlowNodeRun(node.Id, FlowStatus.Error, 0, null, $"Unknown node type '{node.Type}'.", TimeSpan.Zero, new Dictionary<string, IReadOnlyList<FlowItem>>());
                produced[node.Id] = new Dictionary<string, IReadOnlyList<FlowItem>>();
                status = FlowStatus.Error;
                steps.Add(unknown);
                await Report(unknown);
                if (o.StopOnError) break;
                continue;
            }
            var items = type.IsTrigger
                ? start
                : graph.Incoming(node.Id).SelectMany(e => produced.TryGetValue(e.From, out var ports) && ports.TryGetValue(e.FromPort, out var carried) ? carried : []).ToList();

            if (items.Count == 0)
            {
                // Nothing reached it: a branch not taken, or everything upstream failed or waits.
                produced[node.Id] = new Dictionary<string, IReadOnlyList<FlowItem>>();
                continue;
            }

            if (node.Disabled)
            {
                var pass = new Dictionary<string, IReadOnlyList<FlowItem>> { [type.Outputs[0].Id] = items };
                produced[node.Id] = pass;
                var skipped = new FlowNodeRun(node.Id, FlowStatus.None, items.Count, "Disabled: items passed through", null, TimeSpan.Zero, pass);
                steps.Add(skipped);
                await Report(skipped);
                continue;
            }

            await Report(new FlowNodeRun(node.Id, FlowStatus.Running, 0, null, null, TimeSpan.Zero, new Dictionary<string, IReadOnlyList<FlowItem>>()));
            var watch = Stopwatch.StartNew();
            FlowNodeRun step;
            try
            {
                if (!_handlers.TryGetValue(node.Type, out var handler))
                    throw new InvalidOperationException($"No handler is registered for '{node.Type}'.");
                using var timeout = o.NodeTimeout is { } t ? CancellationTokenSource.CreateLinkedTokenSource(cancellation) : null;
                timeout?.CancelAfter(o.NodeTimeout!.Value);
                var result = await handler(new FlowContext(node, type, items) { Options = o }, timeout?.Token ?? cancellation);
                var outputs = result.Outputs.Where(p => p.Value.Count > 0).ToDictionary(p => p.Key, p => p.Value);
                var count = outputs.Values.Sum(v => v.Count);
                total += count;
                if (total > o.MaxItems) throw new InvalidOperationException($"The run produced more than {o.MaxItems} items.");
                produced[node.Id] = result.Waiting ? new Dictionary<string, IReadOnlyList<FlowItem>>() : outputs;
                if (result.Waiting && status != FlowStatus.Error) status = FlowStatus.Waiting;
                step = new FlowNodeRun(node.Id, result.Waiting ? FlowStatus.Waiting : FlowStatus.Success, count, result.Note ?? (count == 1 ? "1 item" : $"{count} items"), null, watch.Elapsed, outputs);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                produced[node.Id] = new Dictionary<string, IReadOnlyList<FlowItem>>();
                status = FlowStatus.Error;
                var message = e is OperationCanceledException ? $"Took longer than {o.NodeTimeout}." : e.Message;
                step = new FlowNodeRun(node.Id, FlowStatus.Error, 0, null, message, watch.Elapsed, new Dictionary<string, IReadOnlyList<FlowItem>>());
            }
            steps.Add(step);
            await Report(step);
            if (step.Status == FlowStatus.Error && o.StopOnError) break;
        }
        return new FlowRunResult(steps, status, clock.Elapsed);
    }

    // The nodes that only route or reshape items: no outside system involved, so they are the same everywhere.
    private void AddBuiltIns()
    {
        FlowHandler pass = (ctx, _) => Task.FromResult(FlowResult.Out(ctx.Items));
        foreach (var type in _catalog.Types.Where(t => t.IsTrigger)) _handlers[type.Key] = pass;
        _handlers["logic.merge"] = (ctx, _) => Task.FromResult(FlowResult.Out(ctx.Items, $"{ctx.Items.Count} merged"));

        _handlers["logic.if"] = (ctx, _) =>
        {
            var yes = new List<FlowItem>();
            var no = new List<FlowItem>();
            foreach (var item in ctx.Items) (FlowTemplate.Test(ctx.Setting("condition"), item) ? yes : no).Add(item);
            return Task.FromResult(new FlowResult { Note = $"{yes.Count} true, {no.Count} false" }.To("true", yes).To("false", no));
        };

        // Rules are the settings rule1, rule2 ... (conditions), routed to "case-1", "case-2" ...; items that match none go to "fallback".
        _handlers["logic.switch"] = (ctx, _) =>
        {
            var ports = ctx.Type.Outputs.Select(p => p.Id).ToList();
            var routed = ports.ToDictionary(p => p, _ => new List<FlowItem>());
            foreach (var item in ctx.Items)
            {
                var target = "fallback";
                for (var i = 1; i < ports.Count; i++)
                    if (ctx.Node.Config.TryGetValue($"rule{i}", out var rule) && FlowTemplate.Test(rule, item)) { target = ports[i - 1]; break; }
                routed[routed.ContainsKey(target) ? target : ports[^1]].Add(item);
            }
            var result = new FlowResult { Note = string.Join(", ", routed.Where(p => p.Value.Count > 0).Select(p => $"{p.Key}: {p.Value.Count}")) };
            foreach (var (port, list) in routed) result.To(port, list);
            return Task.FromResult(result);
        };

        _handlers["logic.wait"] = async (ctx, ct) =>
        {
            var seconds = double.TryParse(ctx.Setting("seconds"), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var s) ? Math.Max(0, s) : 0;
            seconds = Math.Min(seconds, ctx.Options.MaxWait.TotalSeconds);
            await Task.Delay(TimeSpan.FromSeconds(seconds), ct);
            return FlowResult.Out(ctx.Items, $"waited {seconds:0.#} s");
        };

        // "fields": one "name = value" per line; values may use {{path}} of the item. A dotted name sets inside a nested value.
        _handlers["data.set"] = (ctx, _) =>
        {
            var lines = ctx.Setting("fields").Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Where(l => l.Contains('=')).ToList();
            var outputs = ctx.Items.Select(item => lines.Aggregate(item, (acc, line) =>
            {
                var at = line.IndexOf('=');
                return acc.With(line[..at].Trim(), FlowTemplate.Render(line[(at + 1)..].Trim(), item));
            })).ToList();
            return Task.FromResult(FlowResult.Out(outputs, $"{lines.Count} field{(lines.Count == 1 ? "" : "s")} set"));
        };

        _handlers["data.filter"] = (ctx, _) =>
        {
            var kept = ctx.Items.Where(i => FlowTemplate.Test(ctx.Setting("condition"), i)).ToList();
            return Task.FromResult(FlowResult.Out(kept, $"{kept.Count} of {ctx.Items.Count} kept"));
        };
    }
}
