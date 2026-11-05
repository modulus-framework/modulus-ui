using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Flow;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Visual workflow designer (an n8n or Zapier style canvas) for AI and automation flows: drag nodes from the palette, draw
/// connections from an output dot to an input dot, pan, zoom, undo. The graph posts as JSON in a hidden input named <c>name</c>
/// (or the <c>asp-for</c> property), so a form saves it and htmx can post it on <c>change</c>:
/// <code>
/// &lt;form method="post"&gt;
///   &lt;app-flow-editor asp-for="Workflow" height="600" /&gt;       @* a string (JSON) or a FlowGraph property *@
///   &lt;app-button type="submit"&gt;Save&lt;/app-button&gt;
/// &lt;/form&gt;
/// </code>
/// Read it back with <see cref="FlowGraph.TryParse"/>. The editor raises <c>m-flow-change</c>, <c>m-flow-open</c> (a node was opened),
/// <c>m-flow-select</c> and <c>m-flow-pending</c> on itself; <c>modulusFlow.setStatus(editor, id, 'success')</c> shows a run on it.
/// The same editor is <c>AppFlowEditor</c> in Blazor.
/// </summary>
[HtmlTargetElement("app-flow-editor", TagStructure = TagStructure.WithoutEndTag)]
public class FlowEditorTagHelper : TagHelper
{
    [HtmlAttributeName("asp-for")] public ModelExpression? For { get; set; }

    /// <summary>The field name of the posted JSON; defaults to the <c>asp-for</c> property.</summary>
    public string? Name { get; set; }

    /// <summary>The workflow to show; takes precedence over <c>asp-for</c>.</summary>
    public FlowGraph? Graph { get; set; }

    /// <summary>The node kinds the palette offers; <see cref="FlowCatalog.Default"/> when omitted.</summary>
    public FlowCatalog? Catalog { get; set; }

    public int Height { get; set; } = 560;

    [HtmlAttributeName("read-only")] public bool ReadOnly { get; set; }

    [HtmlAttributeName("show-palette")] public bool ShowPalette { get; set; } = true;

    [HtmlAttributeName("show-toolbar")] public bool ShowToolbar { get; set; } = true;

    public int Grid { get; set; } = 20;

    public string Label { get; set; } = "Workflow editor";

    /// <summary>Report selection changes through the event input as well (htmx can listen with <c>hx-trigger="change from:…"</c>).</summary>
    [HtmlAttributeName("emit-selection")] public bool EmitSelection { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var graph = Graph ?? FromModel(For?.Model);
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        var html = FlowHtml.Editor(graph, Catalog ?? FlowCatalog.Default, new FlowOptions
        {
            Name = Name ?? For?.Name, Height = Height, ReadOnly = ReadOnly, ShowPalette = ShowPalette, ShowToolbar = ShowToolbar, Grid = Grid,
            Label = Label, CssClass = cls, EmitSelection = EmitSelection,
        });
        output.TagName = null;
        output.Content.SetHtmlContent(html);
    }

    private static FlowGraph FromModel(object? model) => model switch
    {
        FlowGraph g => g,
        string s when FlowGraph.TryParse(s, out var parsed) => parsed,
        _ => FlowGraph.Empty,
    };
}
