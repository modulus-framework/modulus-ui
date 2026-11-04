using Modulus.Ui.Abstractions.Flow;
using Xunit;

namespace Modulus.Blazor.Tests;

public class FlowLoopTests
{
    /// <summary>trigger → loop (body: tag gains an x each round; back into the loop) → done → end.</summary>
    private static FlowGraph Loop(string condition, string maxIterations = "10") => new(
        [
            new("t", "trigger.manual", 0, 0),
            new("loop", "logic.loop", 0, 0) { Config = new Dictionary<string, string> { ["condition"] = condition, ["maxIterations"] = maxIterations } },
            new("body", "data.set", 0, 0) { Config = new Dictionary<string, string> { ["fields"] = "tag = {{tag}}x" } },
            new("end", "data.set", 0, 0) { Config = new Dictionary<string, string> { ["fields"] = "seen = yes" } },
        ],
        [
            new("e1", "t", "loop"),
            new("e2", "loop", "body") { FromPort = FlowLoop.BodyPort },
            new("e3", "body", "loop"),
            new("e4", "loop", "end") { FromPort = FlowLoop.DonePort },
        ]);

    private static readonly FlowItem Start = FlowItem.Of(("tag", ""));

    [Fact]
    public async Task TheBody_RunsUntilTheConditionFails_AndTheItemLeavesOnDone()
    {
        var run = await new FlowRunner().RunAsync(Loop("tag != \"xxx\""), [Start]);

        Assert.Equal(FlowStatus.Success, run.Status);
        Assert.Equal(3, run.Steps.Count(s => s.NodeId == "body"));
        Assert.Equal("3 rounds, 1 done", run.Step("loop")!.Note);
        Assert.Equal(1, run.Step("end")!.ItemsOut);
    }

    [Fact]
    public async Task TheRoundLimit_SendsWhatIsLeftOnDone()
    {
        var run = await new FlowRunner().RunAsync(Loop("", "2"), [Start]);

        Assert.Equal(2, run.Steps.Count(s => s.NodeId == "body"));
        Assert.Equal("2 rounds, 1 done", run.Step("loop")!.Note);
        var done = run.Step("end")!.Outputs.Values.SelectMany(v => v).ToList();
        Assert.Single(done);
    }

    [Fact]
    public async Task EveryItemIsRoundedUntilItLeaves()
    {
        // Two items: one starts with a long tag and leaves at once; the other goes round twice.
        var run = await new FlowRunner().RunAsync(Loop("tag != \"xx\"", "10"), [FlowItem.Of(("tag", "")), FlowItem.Of(("tag", "xx"))]);

        Assert.Equal(2, run.Steps.Count(s => s.NodeId == "body"));
        var done = run.Step("loop")!.Note;
        Assert.Equal("2 rounds, 2 done", done);
    }

    [Fact]
    public async Task ABodyThatBranches_IsReportedAndFailsTheLoop()
    {
        var graph = new FlowGraph(
            [
                new("t", "trigger.manual", 0, 0),
                new("loop", "logic.loop", 0, 0),
                new("if", "logic.if", 0, 0) { Config = new Dictionary<string, string> { ["condition"] = "" } },
                new("z", "data.set", 0, 0),
            ],
            [
                new("e1", "t", "loop"),
                new("e2", "loop", "if") { FromPort = FlowLoop.BodyPort },
                new("e3", "if", "loop") { FromPort = "true" },
                new("e4", "if", "z") { FromPort = "false" },
            ]);

        var issues = graph.Validate(FlowCatalog.Default);
        Assert.Contains(issues, i => i.Level == FlowIssueLevel.Error && i.NodeId == "loop");

        var run = await new FlowRunner().RunAsync(graph, [Start]);
        Assert.Equal(FlowStatus.Error, run.Step("loop")!.Status);
        Assert.Contains("one chain", run.Step("loop")!.Error);
    }

    [Fact]
    public void AMissingBodyConnection_IsAValidationError()
    {
        var graph = new FlowGraph([new("t", "trigger.manual", 0, 0), new("loop", "logic.loop", 0, 0)], [new("e1", "t", "loop")]);
        var issues = graph.Validate(FlowCatalog.Default);
        Assert.Contains(issues, i => i.Level == FlowIssueLevel.Error && i.NodeId == "loop" && i.Message.Contains("Each item"));
    }

    [Fact]
    public void ALoop_IsNotReportedAsAnUnboundedCycle_andItsBodyIsNotInTheOrderTwice()
    {
        var graph = Loop("tag != \"xxx\"");
        Assert.DoesNotContain(graph.Validate(FlowCatalog.Default), i => i.Message.Contains("loop;"));
        Assert.Equal([("e3", "body")], graph.LoopBackEdges().Select(e => (e.Id, e.From)));
        var order = graph.ExecutionOrder(FlowCatalog.Default).Select(n => n.Id).ToList();
        Assert.Equal(new[] { "t", "loop", "body", "end" }, order);
    }
}
