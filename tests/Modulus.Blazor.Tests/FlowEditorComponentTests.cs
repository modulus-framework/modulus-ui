using Bunit;
using Microsoft.AspNetCore.Components;
using Modulus.Blazor.Components.Extras;
using Modulus.Ui.Abstractions.Flow;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>AppFlowEditor: what it renders, how it takes the edits the browser script reports, and when it redraws the canvas.</summary>
public class FlowEditorComponentTests : AsyncBunitContext
{
    public FlowEditorComponentTests() => JSInterop.Mode = JSRuntimeMode.Loose;

    private static FlowGraph Graph(params string[] ids) => new(
        ids.Select((id, i) => new FlowNode(id, i == 0 ? "trigger.manual" : "ai.chat", i * 300, 0)).ToList(),
        ids.Skip(1).Select((id, i) => new FlowEdge($"e{i}", ids[i], id)).ToList());

    private IRenderedComponent<AppFlowEditor> Render(FlowGraph graph, Action<ComponentParameterCollectionBuilder<AppFlowEditor>>? more = null) =>
        Render<AppFlowEditor>(p => { p.Add(c => c.Graph, graph); more?.Invoke(p); });

    [Fact]
    public void RendersTheEditor_WithItsHiddenInputsAsRazorBesideTheMarkup()
    {
        var cut = Render(Graph("t", "a"));

        Assert.Equal(2, cut.FindAll(".m-flow-node").Count);
        Assert.Single(cut.FindAll("path.m-flow-edge"));
        Assert.NotNull(cut.Find(".m-flow[data-m-flow-outside]"));
        // The inputs belong to the component (Blazor delivers change only to an input with its own handler), not to the markup string.
        Assert.Single(cut.FindAll("input[data-m-flow-value]"));
        Assert.Single(cut.FindAll("input[data-m-flow-event]"));
        Assert.Equal(cut.Find(".m-flow").ParentElement, cut.Find("input[data-m-flow-value]").ParentElement);
        Assert.True(FlowGraph.TryParse(cut.Find("input[data-m-flow-value]").GetAttribute("value"), out var back));
        Assert.Equal(2, back.Nodes.Count);
    }

    [Fact]
    public void AnEditFromTheBrowser_ReachesGraphChanged_AsTheNewGraph()
    {
        FlowGraph? got = null;
        var cut = Render(Graph("t", "a"), p => p.Add(c => c.GraphChanged, g => got = g));
        var moved = Graph("t", "a") with { Nodes = [new("t", "trigger.manual", 40, 40), new("a", "ai.chat", 300, 0)] };

        cut.Find("input[data-m-flow-value]").Change(moved.ToJson());

        Assert.NotNull(got);
        Assert.Equal(40, got!.Node("t")!.X);
        Assert.Single(got.Edges);
    }

    [Fact]
    public void WhenThePageHandsBackTheGraphItJustGot_TheCanvasIsNotRedrawn()
    {
        var graph = Graph("t", "a");
        var held = graph;
        var cut = Render(graph, p => p.Add(c => c.GraphChanged, g => held = g));
        var edited = graph with { Nodes = [new("t", "trigger.manual", 80, 20), new("a", "ai.chat", 300, 0)] };
        cut.Find("input[data-m-flow-value]").Change(edited.ToJson());
        var renders = cut.RenderCount;

        // The page keeps what the editor reported and passes it back: the canvas the browser already shows stays as it is.
        cut.Render(p => p.Add(c => c.Graph, held));
        Assert.Equal(renders, cut.RenderCount);

        // A different graph from the page (a template loaded, a reset) replaces the canvas.
        cut.Render(p => p.Add(c => c.Graph, Graph("t", "a", "b")));
        Assert.Equal(3, cut.FindAll(".m-flow-node").Count);
        Assert.True(cut.RenderCount > renders);
    }

    [Fact]
    public void APageThatIgnoresTheEdit_GetsItsOwnGraphBackOnTheNextRender()
    {
        var graph = Graph("t", "a");
        var cut = Render(graph);
        cut.Find("input[data-m-flow-value]").Change(Graph("t", "a", "b").ToJson());

        // The page did not accept the edit, so a re-render with its graph shows that graph again.
        cut.Render(p => p.Add(c => c.Graph, graph).Add(c => c.Height, 500));

        Assert.Equal(2, cut.FindAll(".m-flow-node").Count);
    }

    [Fact]
    public void OpeningANode_ReportsItsId_AndSelectionOnlyWhenSomethingListens()
    {
        string? opened = null;
        FlowEvent? selected = null;
        var cut = Render(Graph("t", "a"), p => p.Add(c => c.OnOpen, id => opened = id).Add(c => c.OnSelect, e => selected = e));

        cut.Find("input[data-m-flow-event]").Change("""{"type":"open","id":"a"}""");
        cut.Find("input[data-m-flow-event]").Change("""{"type":"select","nodes":["t","a"]}""");

        Assert.Equal("a", opened);
        Assert.Equal(["t", "a"], selected!.Nodes);
        Assert.NotNull(cut.Find(".m-flow[data-m-flow-select]"));
        Assert.Null(Render(Graph("t")).Find(".m-flow").GetAttribute("data-m-flow-select"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[]")]
    [InlineData("""{"type":"open"}""")]
    public void NoiseInTheInputs_IsIgnored(string value)
    {
        var changed = 0;
        var opened = 0;
        var cut = Render(Graph("t"), p => p.Add(c => c.GraphChanged, _ => changed++).Add(c => c.OnOpen, _ => opened++));

        cut.Find("input[data-m-flow-event]").Change(value);

        Assert.Equal(0, changed);
        Assert.Equal(0, opened);
    }

    [Fact]
    public async Task SetStatus_UpdateNode_AndFit_CallTheBrowserScript()
    {
        var cut = Render(Graph("t", "a"));

        await cut.InvokeAsync(() => cut.Instance.SetStatusAsync("a", FlowStatus.Success, "3 items"));
        await cut.InvokeAsync(() => cut.Instance.UpdateNodeAsync("a", title: "Summarise", config: new Dictionary<string, string> { ["prompt"] = "x" }));
        await cut.InvokeAsync(() => cut.Instance.FitAsync());

        var calls = JSInterop.Invocations.Select(i => i.Identifier).ToList();
        Assert.Contains("modulusFlow.setStatus", calls);
        Assert.Contains("modulusFlow.updateNode", calls);
        Assert.Contains("modulusFlow.fit", calls);
        var status = JSInterop.Invocations.Single(i => i.Identifier == "modulusFlow.setStatus");
        Assert.Equal(["a", "success", "3 items"], status.Arguments.Skip(1).Select(a => a?.ToString()));
    }

    [Fact]
    public void OptionsReachTheMarkup_AndAChangeOfOptionRedrawsTheCanvas()
    {
        var cut = Render(Graph("t"), p => p.Add(c => c.ReadOnly, true).Add(c => c.Height, 420).Add(c => c.Label, "Support flow"));

        Assert.Contains("m-flow-readonly", cut.Find(".m-flow").ClassName);
        Assert.Empty(cut.FindAll(".m-flow-palette"));
        Assert.Contains("height:420px", cut.Find(".m-flow").GetAttribute("style"));
        Assert.Equal("Support flow", cut.Find(".m-flow").GetAttribute("aria-label"));

        cut.Render(p => p.Add(c => c.Graph, Graph("t")).Add(c => c.ReadOnly, false));
        Assert.NotEmpty(cut.FindAll(".m-flow-palette"));
    }
}

public class FlowEventTests
{
    [Fact]
    public void ParsesOpenSelectAndPending_AndRefusesGraphsAndNoise()
    {
        Assert.True(FlowEvent.TryParse("""{"type":"open","id":"n1"}""", out var open));
        Assert.Equal(("open", "n1"), (open.Type, open.Id));
        Assert.True(FlowEvent.TryParse("""{"type":"select","nodes":["a","b"],"edge":null}""", out var sel));
        Assert.Equal(["a", "b"], sel.Nodes);
        Assert.True(FlowEvent.TryParse("""{"type":"pending","from":"a","port":"true"}""", out var pend));
        Assert.Equal(("a", "true"), (pend.From, pend.Port));

        Assert.False(FlowEvent.TryParse(FlowGraph.Empty.ToJson(), out _));
        Assert.False(FlowEvent.TryParse("""{"type":""}""", out _));
        Assert.False(FlowEvent.TryParse("{ broken", out _));
        Assert.False(FlowEvent.TryParse(null, out _));
    }

    [Fact]
    public void AGraphIsNeverTakenForAnEvent_AndAnEventIsNeverTakenForAGraph()
    {
        var graph = new FlowGraph([new("a", "ai.chat", 0, 0)], []);

        Assert.False(FlowEvent.TryParse(graph.ToJson(), out _));
        // An event has no nodes list of FlowNode objects, but "nodes" as ids parses as a graph attempt and must fail cleanly.
        Assert.False(FlowGraph.TryParse("""{"type":"select","nodes":["a","b"]}""", out _));
    }
}
