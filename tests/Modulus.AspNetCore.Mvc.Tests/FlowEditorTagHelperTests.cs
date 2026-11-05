using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions.Flow;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

/// <summary>&lt;app-flow-editor&gt;: the same markup as the shared renderer, with the graph posted as JSON in one field.</summary>
public class FlowEditorTagHelperTests
{
    private static readonly EmptyModelMetadataProvider Metadata = new();

    private static ModelExpression For<T>(string name, T model) => new(name, Metadata.GetModelExplorerForType(typeof(T), model));

    private static FlowGraph Sample() => new(
        [new("t", "trigger.webhook", 0, 0), new("a", "ai.chat", 300, 0) { Title = "Summarise" }],
        [new("e1", "t", "a")]);

    private static async Task<string> RunAsync(FlowEditorTagHelper helper, TagHelperAttributeList? attributes = null)
    {
        var output = new TagHelperOutput("app-flow-editor", attributes ?? [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent()));
        await helper.ProcessAsync(new TagHelperContext("app-flow-editor", [], new Dictionary<object, object>(), "id"), output);
        Assert.Null(output.TagName);
        using var writer = new StringWriter();
        output.Content.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    [Fact]
    public async Task Graph_MatchesTheSharedRenderer_AndPostsInTheNamedField()
    {
        var html = await RunAsync(new FlowEditorTagHelper { Graph = Sample(), Name = "Workflow", Height = 480, Label = "Support flow" });

        Assert.Equal(FlowHtml.Editor(Sample(), FlowCatalog.Default, new FlowOptions { Name = "Workflow", Height = 480, Label = "Support flow" }), html);
        Assert.Contains("data-m-flow-value name=\"Workflow\"", html);
    }

    [Fact]
    public async Task AspFor_TakesAFlowGraphProperty_OrJsonInAString_AndNamesTheField()
    {
        var fromGraph = await RunAsync(new FlowEditorTagHelper { For = For("Flow", Sample()) });
        var fromJson = await RunAsync(new FlowEditorTagHelper { For = For("Flow", Sample().ToJson()) });
        var fromBad = await RunAsync(new FlowEditorTagHelper { For = For("Flow", "oops") });

        Assert.Contains("data-m-flow-value name=\"Flow\"", fromGraph);
        Assert.Equal(2, CountOf(fromGraph, "class=\"m-flow-node"));
        Assert.Equal(fromGraph, fromJson);
        Assert.Equal(0, CountOf(fromBad, "class=\"m-flow-node"));
        Assert.Contains("m-flow-empty", fromBad);
    }

    [Fact]
    public async Task Options_ReadOnlyPaletteToolbarGridClassAndSelection_PassThrough()
    {
        var html = await RunAsync(
            new FlowEditorTagHelper { Graph = Sample(), ReadOnly = true, ShowToolbar = false, Grid = 0, EmitSelection = true },
            new TagHelperAttributeList { { "class", "my-flow" } });

        Assert.Contains("m-flow-readonly", html);
        Assert.Contains("my-flow", html);
        Assert.DoesNotContain("m-flow-palette", html);
        Assert.DoesNotContain("m-flow-toolbar", html);
        Assert.Contains("data-grid=\"0\"", html);
        Assert.Contains("data-m-flow-select", html);
    }

    [Fact]
    public async Task ACustomCatalog_ReplacesTheDefaultPalette()
    {
        var catalog = new FlowCatalog([new FlowNodeType("my.step", "My step", "Mine", "tool") { Slot = 2 }]);

        var html = await RunAsync(new FlowEditorTagHelper { Graph = FlowGraph.Empty, Catalog = catalog });

        Assert.Equal(1, CountOf(html, "class=\"m-flow-pitem\""));
        Assert.Contains("My step", html);
        Assert.DoesNotContain("AI chat model", html);
    }

    [Fact]
    public async Task TheHiddenInputRoundTripsThroughFlowGraph()
    {
        var html = await RunAsync(new FlowEditorTagHelper { Graph = Sample(), Name = "W" });
        var start = html.IndexOf("data-m-flow-value", StringComparison.Ordinal);
        var value = html[(html.IndexOf("value=\"", start, StringComparison.Ordinal) + 7)..];
        value = System.Net.WebUtility.HtmlDecode(value[..value.IndexOf('"')]);

        Assert.True(FlowGraph.TryParse(value, out var back));
        Assert.Equal("Summarise", back.Node("a")!.Title);
        Assert.Empty(back.Validate(FlowCatalog.Default));
    }

    private static int CountOf(string html, string needle)
    {
        var count = 0;
        for (var i = html.IndexOf(needle, StringComparison.Ordinal); i >= 0; i = html.IndexOf(needle, i + needle.Length, StringComparison.Ordinal)) count++;
        return count;
    }
}
