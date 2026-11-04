using Modulus.Ui.Abstractions.Flow;
using Modulus.Ui.Abstractions.Shell;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The workflow designer's model (graph, JSON, validation, run order, layout) and the markup it renders.</summary>
public class FlowModelTests
{
    private static readonly FlowCatalog Catalog = FlowCatalog.Default;

    private static FlowGraph Sample() => new(
    [
        new("t", "trigger.webhook", 0, 0),
        new("ai", "ai.chat", 300, 0) { Title = "Summarise ticket", Config = new Dictionary<string, string> { ["prompt"] = "Summarise: {{body}}" } },
        new("if", "logic.if", 600, 0),
        new("mail", "action.email", 900, 0),
        new("slack", "action.slack", 900, 120),
    ],
    [
        new("e1", "t", "ai"),
        new("e2", "ai", "if"),
        new("e3", "if", "mail") { FromPort = "true" },
        new("e4", "if", "slack") { FromPort = "false" },
    ]);

    [Fact]
    public void EveryCatalogIconAndToolbarIconResolvesToASvg()
    {
        var names = Catalog.Types.Select(t => t.Icon).Concat(["zoom-out", "zoom-in", "fullscreen", "undo", "redo", "layout", "question-circle", "close", "delete", "plus"]).Distinct();
        var missing = names.Where(n => !ShellRenderer.Icon(n).Contains("<path", StringComparison.Ordinal)).ToList();

        Assert.True(missing.Count == 0, "No icon for: " + string.Join(", ", missing));
    }

    [Fact]
    public void CatalogKeysAreUnique_TriggersHaveNoInput_AndSlotsAreColours()
    {
        Assert.Equal(Catalog.Types.Count, Catalog.Types.Select(t => t.Key).Distinct().Count());
        Assert.All(Catalog.Types.Where(t => t.Category == "Triggers"), t => Assert.True(t.IsTrigger));
        Assert.All(Catalog.Types.Where(t => t.Category != "Triggers"), t => Assert.True(t.HasInput));
        Assert.All(Catalog.Types, t => Assert.InRange(t.Slot, 1, 8));
        Assert.All(Catalog.Types, t => Assert.NotEmpty(t.Outputs));
    }

    [Fact]
    public void Json_RoundTripsNodesEdgesConfigAndStatus()
    {
        var graph = Sample() with { Nodes = Sample().Nodes.Select(n => n.Id == "ai" ? n with { Status = FlowStatus.Success, Disabled = true, Note = "3 items" } : n).ToList() };

        Assert.True(FlowGraph.TryParse(graph.ToJson(), out var back));

        Assert.Equal(5, back.Nodes.Count);
        Assert.Equal(4, back.Edges.Count);
        var ai = back.Node("ai")!;
        Assert.Equal("Summarise ticket", ai.Title);
        Assert.Equal("Summarise: {{body}}", ai.Config["prompt"]);
        Assert.Equal(FlowStatus.Success, ai.Status);
        Assert.True(ai.Disabled);
        Assert.Equal("false", back.Edges.Single(e => e.Id == "e4").FromPort);
        Assert.Contains("\"status\":\"success\"", graph.ToJson());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{ broken")]
    public void TryParse_RefusesWhatIsNotAGraph(string? text) => Assert.False(FlowGraph.TryParse(text, out _));

    [Fact]
    public void TryParse_AcceptsAnEmptyDocumentAndMissingLists()
    {
        Assert.True(FlowGraph.TryParse("{}", out var g));
        Assert.Empty(g.Nodes);
        Assert.Empty(g.Edges);
    }

    [Fact]
    public void Validate_AGoodGraphHasNoIssues() => Assert.Empty(Sample().Validate(Catalog));

    [Fact]
    public void Validate_FindsBrokenConnectionsUnknownTypesAndTriggerInputs()
    {
        var graph = new FlowGraph(
        [
            new("a", "trigger.manual", 0, 0), new("b", "ai.chat", 0, 0), new("b", "ai.chat", 0, 0), new("x", "nope", 0, 0), new("c", "ai.chat", 0, 0),
        ],
        [
            new("e1", "a", "missing"), new("e2", "b", "b"), new("e3", "c", "a"), new("e4", "a", "c") { FromPort = "ghost" },
        ]);

        var errors = graph.Validate(Catalog).Where(i => i.Level == FlowIssueLevel.Error).Select(i => i.Message).ToList();

        Assert.Contains(errors, m => m.Contains("share the id 'b'"));
        Assert.Contains(errors, m => m.Contains("unknown type 'nope'"));
        Assert.Contains(errors, m => m.Contains("does not exist"));
        Assert.Contains(errors, m => m.Contains("connected to itself"));
        Assert.Contains(errors, m => m.Contains("is a trigger and cannot take an input"));
        Assert.Contains(errors, m => m.Contains("has no output 'ghost'"));
    }

    [Fact]
    public void Validate_WarnsAboutMissingTriggersUnreachableNodesAndLoops()
    {
        var noTrigger = new FlowGraph([new("a", "ai.chat", 0, 0)], []).Validate(Catalog);
        Assert.Contains(noTrigger, i => i.Level == FlowIssueLevel.Warning && i.Message.Contains("no trigger"));

        var loose = new FlowGraph([new("t", "trigger.manual", 0, 0), new("a", "ai.chat", 0, 0)], []).Validate(Catalog);
        Assert.Contains(loose, i => i.NodeId == "a" && i.Message.Contains("never runs"));

        var loop = new FlowGraph([new("t", "trigger.manual", 0, 0), new("a", "ai.chat", 0, 0), new("b", "ai.chat", 0, 0)],
            [new("e1", "t", "a"), new("e2", "a", "b"), new("e3", "b", "a")]).Validate(Catalog);
        Assert.Contains(loop, i => i.Message.Contains("loop"));
        Assert.DoesNotContain(loop, i => i.Level == FlowIssueLevel.Error);
    }

    [Fact]
    public void ExecutionOrder_PutsEveryNodeAfterItsInputs_AndLeavesOutUnreachableOnes()
    {
        var graph = Sample() with { Nodes = [.. Sample().Nodes, new FlowNode("orphan", "ai.chat", 0, 300)] };

        var order = graph.ExecutionOrder(Catalog).Select(n => n.Id).ToList();

        Assert.Equal(["t", "ai", "if"], order.Take(3));
        Assert.Equal(5, order.Count);
        Assert.DoesNotContain("orphan", order);
    }

    [Fact]
    public void ExecutionOrder_SkipsTheNodesOfALoop()
    {
        var graph = new FlowGraph([new("t", "trigger.manual", 0, 0), new("a", "ai.chat", 0, 0), new("b", "ai.chat", 0, 0)],
            [new("e1", "t", "a"), new("e2", "a", "b"), new("e3", "b", "a")]);

        Assert.Equal(["t"], graph.ExecutionOrder(Catalog).Select(n => n.Id));
    }

    [Fact]
    public void AutoLayout_PutsNodesInColumnsByLongestPath_AndKeepsEverythingElse()
    {
        var tidy = Sample().AutoLayout();

        Assert.Equal(0 * 300 + 40, tidy.Node("t")!.X);
        Assert.Equal(1 * 300 + 40, tidy.Node("ai")!.X);
        Assert.Equal(3 * 300 + 40, tidy.Node("mail")!.X);
        Assert.Equal(3 * 300 + 40, tidy.Node("slack")!.X);
        Assert.NotEqual(tidy.Node("mail")!.Y, tidy.Node("slack")!.Y);
        Assert.Equal("Summarise ticket", tidy.Node("ai")!.Title);
        Assert.Equal(Sample().Edges, tidy.Edges);
    }

    [Fact]
    public void AutoLayout_SurvivesLoopsAndEmptyGraphs()
    {
        var loop = new FlowGraph([new("a", "ai.chat", 0, 0), new("b", "ai.chat", 0, 0)], [new("e1", "a", "b"), new("e2", "b", "a")]).AutoLayout();
        Assert.Equal(40, loop.Node("a")!.X);
        Assert.Equal(340, loop.Node("b")!.X);
        Assert.Same(FlowGraph.Empty, FlowGraph.Empty.AutoLayout());
    }

    [Fact]
    public void Geometry_PortsSitInsideTheNodeAndSingleOutputsAreCentred()
    {
        foreach (var n in new[] { 1, 2, 3, 5 })
        {
            var h = FlowGeometry.NodeHeight(n);
            for (var i = 0; i < n; i++) Assert.InRange(FlowGeometry.OutputY(i, n), 10, h - 10);
        }
        Assert.Equal(FlowGeometry.NodeHeight(1) / 2, FlowGeometry.OutputY(0, 1));
        Assert.Equal(FlowGeometry.NodeHeight(3) / 2, FlowGeometry.InputY(3));
        Assert.StartsWith("M220,32 C", FlowGeometry.EdgePath(220, 32, 300, 32));
    }

    [Fact]
    public void Editor_RendersNodesEdgesPaletteToolbarAndTheGraphValue()
    {
        var html = FlowHtml.Editor(Sample(), Catalog, new FlowOptions { Name = "Flow", Height = 480 });

        Assert.Equal(5, Count(html, "class=\"m-flow-node"));
        Assert.Equal(4, Count(html, "class=\"m-flow-edge\""));
        Assert.Equal(4, Count(html, "class=\"m-flow-edge-hit\""));
        Assert.Equal(Catalog.Types.Count, Count(html, "class=\"m-flow-pitem\""));
        Assert.Contains("data-m-flow-value name=\"Flow\"", html);
        Assert.Contains("height:480px;opacity:0", html);
        Assert.Contains("data-m-flow-act=\"undo\"", html);
        Assert.Contains("Summarise ticket", html);
        // The If node has a True and a False output; the first path starts at the first output of node "if" (x 600+220).
        Assert.Contains("data-port=\"true\"", html);
        Assert.Contains("data-port=\"false\"", html);
        Assert.Contains("d=\"M820,", html);
        Assert.Contains("data-catalog=", html);
    }

    [Fact]
    public void Editor_ReadOnlyHasNoPaletteAndHidesTheEditButtons()
    {
        var html = FlowHtml.Editor(Sample(), Catalog, new FlowOptions { ReadOnly = true });

        Assert.DoesNotContain("m-flow-palette", html);
        Assert.Contains("m-flow-readonly", html);
        Assert.Contains("data-readonly", html);
        Assert.Contains("data-m-flow-act=\"undo\" aria-label=\"Undo\" title=\"Undo\" hidden", html);
    }

    [Fact]
    public void Editor_ShowsRunStatus_DisabledNodes_AndEmptiness()
    {
        var graph = new FlowGraph([new("t", "trigger.manual", 0, 0) { Status = FlowStatus.Error, Disabled = true, Note = "failed" }], []);

        var html = FlowHtml.Editor(graph, Catalog);

        Assert.Contains("data-status=\"error\"", html);
        Assert.Contains("m-flow-disabled", html);
        Assert.Contains("m-flow-trigger", html);
        Assert.Contains(">failed<", html);
        Assert.DoesNotContain("m-flow-in\"", html);
        Assert.Contains("m-flow-empty", FlowHtml.Editor(FlowGraph.Empty, Catalog));
    }

    [Fact]
    public void Editor_EncodesEverythingAUserCanTypeAndSkipsEdgesToMissingNodes()
    {
        var graph = new FlowGraph(
            [new("a\"><script>", "ai.chat", 0, 0) { Title = "<img src=x onerror=alert(1)>", Note = "<b>x</b>" }],
            [new("e", "a\"><script>", "gone")]);

        var html = FlowHtml.Editor(graph, Catalog, new FlowOptions { Label = "\"><script>alert(1)</script>" });

        Assert.DoesNotContain("<script>", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<b>x</b>", html);
        Assert.Equal(0, Count(html, "class=\"m-flow-edge\""));
    }

    private static int Count(string html, string needle)
    {
        var count = 0;
        for (var i = html.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = html.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) count++;
        return count;
    }
}
