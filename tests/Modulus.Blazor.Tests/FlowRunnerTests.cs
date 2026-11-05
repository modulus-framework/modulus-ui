using Modulus.Ui.Abstractions.Flow;
using Xunit;

namespace Modulus.Blazor.Tests;

public class FlowTemplateTests
{
    private static readonly FlowItem Order = FlowItem.Of(
        ("message", "My order is LATE and I want a refund"),
        ("total", 120.5),
        ("vip", true),
        ("customer", new Dictionary<string, object?> { ["name"] = "Ada", ["email"] = "ada@example.com" }),
        ("articles", new List<object?> { "Returns", "Shipping" }));

    [Fact]
    public void Render_FillsPathsFromNestedValuesAndLists_AndLeavesMissingOnesEmpty()
    {
        Assert.Equal("Hi Ada <ada@example.com>, see Returns!", FlowTemplate.Render("Hi {{customer.name}} <{{ customer.email }}>, see {{articles.0}}!", Order));
        Assert.Equal("[]", FlowTemplate.Render("[{{nothing.here}}]", Order));
        Assert.Equal("120.5 true", FlowTemplate.Render("{{total}} {{vip}}", Order));
        Assert.Equal("", FlowTemplate.Render(null, Order));
    }

    [Theory]
    [InlineData("", true)]
    [InlineData("vip", true)]
    [InlineData("!vip", false)]
    [InlineData("missing", false)]
    [InlineData("!missing", true)]
    [InlineData("total > 100", true)]
    [InlineData("total >= 120.5", true)]
    [InlineData("total < 100", false)]
    [InlineData("total == 120.5", true)]
    [InlineData("total != 120.5", false)]
    [InlineData("message contains \"refund\"", true)]
    [InlineData("message contains 'REFUND'", true)]
    [InlineData("message startsWith \"my order\"", true)]
    [InlineData("message endsWith \"refund\"", true)]
    [InlineData("message contains \"lawyer\"", false)]
    [InlineData("customer.name == \"ada\"", true)]
    [InlineData("customer.name == {{customer.name}}", true)]
    [InlineData("vip == true", true)]
    [InlineData("articles.1 == \"Shipping\"", true)]
    [InlineData("message contains \"a > b\"", false)]
    public void Test_Conditions(string condition, bool expected) => Assert.Equal(expected, FlowTemplate.Test(condition, Order));

    [Fact]
    public void Test_NumbersCompareAsNumbers_NotAsText()
    {
        var item = FlowItem.Of(("n", "9"));

        Assert.True(FlowTemplate.Test("n < 10", item)); // as text "9" > "10"
        Assert.True(FlowTemplate.Test("n == 9.0", item));
    }

    [Fact]
    public void With_SetsNestedValuesWithoutChangingTheOriginal()
    {
        var changed = Order.With("customer.tier", "gold").With("flag", 1);

        Assert.Equal("gold", changed.Text("customer.tier"));
        Assert.Equal("Ada", changed.Text("customer.name"));
        Assert.Equal("", Order.Text("customer.tier"));
        Assert.Equal("", Order.Text("flag"));
    }
}

public class FlowRunnerTests
{
    private static FlowGraph Chain(params (string Id, string Type)[] nodes) => new(
        nodes.Select(n => new FlowNode(n.Id, n.Type, 0, 0)).ToList(),
        nodes.Skip(1).Select((n, i) => new FlowEdge($"e{i}", nodes[i].Id, n.Id)).ToList());

    private static FlowItem Message(string text) => FlowItem.Of(("message", text));

    private static FlowRunner Runner() => new FlowRunner()
        .OnEach("ai.chat", (c, i, _) => Task.FromResult(i.With("reply", FlowTemplate.Render(c.Setting("prompt"), i))))
        .OnEach("action.email", (c, i, _) => Task.FromResult(i.With("sent", true)));

    /// <summary>trigger → if (message contains "refund") → true: chat → email   false: slack-less end.</summary>
    private static FlowGraph Branching() => new(
    [
        new("t", "trigger.chat", 0, 0),
        new("if", "logic.if", 0, 0) { Config = new Dictionary<string, string> { ["condition"] = "message contains \"refund\"" } },
        new("yes", "ai.chat", 0, 0) { Config = new Dictionary<string, string> { ["prompt"] = "Refund for: {{message}}" } },
        new("no", "ai.chat", 0, 0) { Config = new Dictionary<string, string> { ["prompt"] = "FAQ for: {{message}}" } },
        new("merge", "logic.merge", 0, 0),
        new("mail", "action.email", 0, 0),
    ],
    [
        new("e1", "t", "if"), new("e2", "if", "yes") { FromPort = "true" }, new("e3", "if", "no") { FromPort = "false" },
        new("e4", "yes", "merge"), new("e5", "no", "merge"), new("e6", "merge", "mail"),
    ]);

    [Fact]
    public async Task ARun_FollowsTheConnections_AndPassesItemsThrough()
    {
        var graph = Chain(("t", "trigger.manual"), ("a", "ai.chat"), ("b", "action.email"));
        graph = graph with { Nodes = graph.Nodes.Select(n => n.Id == "a" ? n with { Config = new Dictionary<string, string> { ["prompt"] = "Hello {{name}}" } } : n).ToList() };

        var run = await Runner().RunAsync(graph, [FlowItem.Of(("name", "Ada"))]);

        Assert.Equal(FlowStatus.Success, run.Status);
        Assert.Equal(["t", "a", "b"], run.Steps.Select(s => s.NodeId));
        var last = run.Step("b")!.Outputs["out"].Single();
        Assert.Equal("Hello Ada", last.Text("reply"));
        Assert.Equal("true", last.Text("sent"));
        Assert.Equal("1 item", run.Step("a")!.Note);
    }

    [Fact]
    public async Task OnlyTheBranchThatFires_Runs_AndAMergeCollectsWhatArrived()
    {
        var run = await Runner().RunAsync(Branching(), [Message("I want a refund please")]);

        Assert.Equal(FlowStatus.Success, run.Status);
        Assert.Contains(run.Steps, s => s.NodeId == "yes");
        Assert.DoesNotContain(run.Steps, s => s.NodeId == "no");
        Assert.Equal("1 true, 0 false", run.Step("if")!.Note);
        var sent = run.Step("mail")!.Outputs["out"].Single();
        Assert.Equal("Refund for: I want a refund please", sent.Text("reply"));

        var other = await Runner().RunAsync(Branching(), [Message("Where do I find my invoice")]);
        Assert.DoesNotContain(other.Steps, s => s.NodeId == "yes");
        Assert.Equal("FAQ for: Where do I find my invoice", other.Step("mail")!.Outputs["out"].Single().Text("reply"));
    }

    [Fact]
    public async Task SeveralItems_AreSplitByTheCondition_AndMergedAgain()
    {
        var run = await Runner().RunAsync(Branching(), [Message("refund"), Message("hello"), Message("refund me")]);

        Assert.Equal("2 true, 1 false", run.Step("if")!.Note);
        Assert.Equal(3, run.Step("mail")!.ItemsOut);
    }

    [Fact]
    public async Task Switch_RoutesByRule_AndSendsTheRestToTheFallback()
    {
        var graph = new FlowGraph(
        [
            new("t", "trigger.manual", 0, 0),
            new("sw", "logic.switch", 0, 0) { Config = new Dictionary<string, string> { ["rule1"] = "total > 100", ["rule2"] = "total > 10" } },
            new("big", "ai.chat", 0, 0), new("mid", "ai.chat", 0, 0), new("small", "ai.chat", 0, 0),
        ],
        [new("e1", "t", "sw"), new("e2", "sw", "big") { FromPort = "case-1" }, new("e3", "sw", "mid") { FromPort = "case-2" }, new("e4", "sw", "small") { FromPort = "fallback" }]);

        var run = await Runner().RunAsync(graph, [FlowItem.Of(("total", 500)), FlowItem.Of(("total", 50)), FlowItem.Of(("total", 2)), FlowItem.Of(("total", 150))]);

        Assert.Equal(2, run.Step("big")!.ItemsOut);
        Assert.Equal(1, run.Step("mid")!.ItemsOut);
        Assert.Equal(1, run.Step("small")!.ItemsOut);
    }

    [Fact]
    public async Task SetFieldsAndFilter_ReshapeTheItems()
    {
        var graph = Chain(("t", "trigger.manual"), ("set", "data.set"), ("filter", "data.filter"));
        graph = graph with
        {
            Nodes = graph.Nodes.Select(n => n.Id switch
            {
                "set" => n with { Config = new Dictionary<string, string> { ["fields"] = "greeting = Hi {{name}}\ncustomer.tier = gold" } },
                "filter" => n with { Config = new Dictionary<string, string> { ["condition"] = "name != \"Bob\"" } },
                _ => n,
            }).ToList(),
        };

        var run = await Runner().RunAsync(graph, [FlowItem.Of(("name", "Ada")), FlowItem.Of(("name", "Bob"))]);

        var items = run.Step("filter")!.Outputs["out"];
        Assert.Single(items);
        Assert.Equal("Hi Ada", items[0].Text("greeting"));
        Assert.Equal("gold", items[0].Text("customer.tier"));
        Assert.Equal("1 of 2 kept", run.Step("filter")!.Note);
    }

    [Fact]
    public async Task ADisabledNode_PassesItsItemsStraightOn()
    {
        var graph = Chain(("t", "trigger.manual"), ("a", "ai.chat"), ("b", "action.email"));
        graph = graph with { Nodes = graph.Nodes.Select(n => n.Id == "a" ? n with { Disabled = true } : n).ToList() };

        var run = await Runner().RunAsync(graph, [Message("hi")]);

        Assert.Equal(FlowStatus.None, run.Step("a")!.Status);
        Assert.Contains("Disabled", run.Step("a")!.Note);
        var out2 = run.Step("b")!.Outputs["out"].Single();
        Assert.Equal("", out2.Text("reply")); // the chat node did not run
        Assert.Equal("true", out2.Text("sent"));
    }

    [Fact]
    public async Task AFailedNode_StopsItsOwnBranchOnly_AndTheRunIsAnError()
    {
        var graph = new FlowGraph(
        [new("t", "trigger.manual", 0, 0), new("bad", "ai.chat", 0, 0), new("after", "action.email", 0, 0), new("good", "action.email", 0, 0)],
        [new("e1", "t", "bad"), new("e2", "bad", "after"), new("e3", "t", "good")]);
        var runner = new FlowRunner().On("ai.chat", (_, _) => throw new InvalidOperationException("The model is down."))
            .OnEach("action.email", (_, i, _) => Task.FromResult(i));

        var run = await runner.RunAsync(graph);

        Assert.Equal(FlowStatus.Error, run.Status);
        Assert.Equal("The model is down.", run.Step("bad")!.Error);
        Assert.Equal(FlowStatus.Error, run.Step("bad")!.Status);
        Assert.Null(run.Step("after"));
        Assert.Equal(FlowStatus.Success, run.Step("good")!.Status);
    }

    [Fact]
    public async Task StopOnError_EndsTheRunAtTheFirstFailure()
    {
        var graph = new FlowGraph(
        [new("t", "trigger.manual", 0, 0), new("bad", "ai.chat", 0, 0), new("good", "action.email", 0, 0)],
        [new("e1", "t", "bad"), new("e2", "t", "good")]);
        var runner = new FlowRunner().On("ai.chat", (_, _) => throw new InvalidOperationException("boom")).OnEach("action.email", (_, i, _) => Task.FromResult(i));

        var run = await runner.RunAsync(graph, options: new FlowRunOptions { StopOnError = true });

        Assert.Equal(["t", "bad"], run.Steps.Select(s => s.NodeId));
    }

    [Fact]
    public async Task ANodeWithoutAHandler_OrOfAnUnknownType_IsAnErrorNotACrash()
    {
        var graph = new FlowGraph([new("t", "trigger.manual", 0, 0), new("a", "ai.agent", 0, 0), new("x", "nope", 0, 0)], [new("e1", "t", "a"), new("e2", "t", "x")]);

        var run = await new FlowRunner().RunAsync(graph);

        Assert.Contains("No handler is registered for 'ai.agent'", run.Step("a")!.Error);
        Assert.Equal(FlowStatus.Error, run.Status);
        Assert.Equal(FlowStatus.Success, run.Step("t")!.Status);
    }

    [Fact]
    public async Task AWaitingNode_PausesItsBranch_AndTheRunWaits()
    {
        var graph = Chain(("t", "trigger.manual"), ("approve", "ai.human"), ("mail", "action.email"));
        var runner = new FlowRunner().On("ai.human", (_, _) => Task.FromResult(FlowResult.Wait("Asked the manager"))).OnEach("action.email", (_, i, _) => Task.FromResult(i));

        var run = await runner.RunAsync(graph);

        Assert.Equal(FlowStatus.Waiting, run.Status);
        Assert.Equal(FlowStatus.Waiting, run.Step("approve")!.Status);
        Assert.Equal("Asked the manager", run.Step("approve")!.Note);
        Assert.Null(run.Step("mail"));
    }

    [Fact]
    public async Task OnStep_ReportsRunningThenTheResult_ForEveryNode()
    {
        var seen = new List<(string, FlowStatus)>();
        var graph = Chain(("t", "trigger.manual"), ("a", "ai.chat"));

        await Runner().RunAsync(graph, options: new FlowRunOptions { OnStep = s => { seen.Add((s.NodeId, s.Status)); return Task.CompletedTask; } });

        Assert.Equal([("t", FlowStatus.Running), ("t", FlowStatus.Success), ("a", FlowStatus.Running), ("a", FlowStatus.Success)], seen);
    }

    [Fact]
    public async Task Cancellation_StopsTheRun()
    {
        using var cts = new CancellationTokenSource();
        var graph = Chain(("t", "trigger.manual"), ("a", "ai.chat"), ("b", "action.email"));
        var runner = Runner().On("ai.chat", (_, _) => { cts.Cancel(); return Task.FromResult(FlowResult.Out([FlowItem.Empty])); });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runner.RunAsync(graph, cancellation: cts.Token));
    }

    [Fact]
    public async Task NodeTimeout_FailsASlowNode()
    {
        var graph = Chain(("t", "trigger.manual"), ("slow", "ai.chat"));
        var runner = new FlowRunner().On("ai.chat", async (_, ct) => { await Task.Delay(TimeSpan.FromSeconds(5), ct); return FlowResult.Out([FlowItem.Empty]); });

        var run = await runner.RunAsync(graph, options: new FlowRunOptions { NodeTimeout = TimeSpan.FromMilliseconds(50) });

        Assert.Equal(FlowStatus.Error, run.Step("slow")!.Status);
        Assert.Contains("longer than", run.Step("slow")!.Error);
    }

    [Fact]
    public async Task WaitIsCappedByTheRun_NoMatterWhatItIsSetTo()
    {
        var graph = Chain(("t", "trigger.manual"), ("w", "logic.wait"));
        graph = graph with { Nodes = graph.Nodes.Select(n => n.Id == "w" ? n with { Config = new Dictionary<string, string> { ["seconds"] = "3600" } } : n).ToList() };

        var started = DateTime.UtcNow;
        var run = await new FlowRunner().RunAsync(graph, options: new FlowRunOptions { MaxWait = TimeSpan.FromMilliseconds(30) });

        Assert.True(DateTime.UtcNow - started < TimeSpan.FromSeconds(2));
        Assert.Equal(FlowStatus.Success, run.Status);
    }

    [Fact]
    public async Task AGuardStopsANodeThatMultipliesItems()
    {
        var graph = Chain(("t", "trigger.manual"), ("boom", "ai.chat"));
        var runner = new FlowRunner().On("ai.chat", (_, _) => Task.FromResult(FlowResult.Out(Enumerable.Range(0, 500).Select(_ => FlowItem.Empty))));

        var run = await runner.RunAsync(graph, options: new FlowRunOptions { MaxItems = 100 });

        Assert.Equal(FlowStatus.Error, run.Step("boom")!.Status);
        Assert.Contains("more than 100 items", run.Step("boom")!.Error);
    }

    [Fact]
    public async Task NodesOnALoop_AreNotRun_AndNothingHangs()
    {
        var graph = new FlowGraph([new("t", "trigger.manual", 0, 0), new("a", "ai.chat", 0, 0), new("b", "ai.chat", 0, 0)],
            [new("e1", "t", "a"), new("e2", "a", "b"), new("e3", "b", "a")]);

        var run = await Runner().RunAsync(graph);

        Assert.Equal(["t"], run.Steps.Select(s => s.NodeId));
    }

    [Fact]
    public async Task ApplyTo_PutsEachNodesResultOnTheGraphForTheEditor()
    {
        var graph = Branching();

        var run = await Runner().RunAsync(graph, [Message("refund")]);
        var shown = run.ApplyTo(graph);

        Assert.Equal(FlowStatus.Success, shown.Node("yes")!.Status);
        Assert.Equal(FlowStatus.None, shown.Node("no")!.Status); // not taken
        Assert.Equal("1 true, 0 false", shown.Node("if")!.Note);
        Assert.Equal(graph.Edges, shown.Edges);
    }

    [Fact]
    public async Task AnEmptyGraph_OrOneWithoutATrigger_RunsNothing()
    {
        Assert.Empty((await new FlowRunner().RunAsync(FlowGraph.Empty)).Steps);
        Assert.Empty((await new FlowRunner().RunAsync(new FlowGraph([new("a", "ai.chat", 0, 0)], []))).Steps);
    }
}
