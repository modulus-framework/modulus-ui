using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.Ui.Abstractions.Flow;

/// <summary>What the editor looks like and allows. The flavours pass these straight through.</summary>
public sealed record FlowOptions
{
    /// <summary>Form field name of the hidden input that carries the graph JSON; null for none (Blazor reads it from the change event).</summary>
    public string? Name { get; init; }

    /// <summary>Canvas height in px.</summary>
    public int Height { get; init; } = 560;

    /// <summary>View and select only: pan, zoom and open nodes, but no dragging, connecting or deleting. For showing the result of a run.</summary>
    public bool ReadOnly { get; init; }

    public bool ShowPalette { get; init; } = true;

    public bool ShowToolbar { get; init; } = true;

    /// <summary>Nodes snap to this many px when dropped; 0 for free placement.</summary>
    public int Grid { get; init; } = 20;

    /// <summary>Accessible name of the editor.</summary>
    public string Label { get; init; } = "Workflow editor";

    public string? CssClass { get; init; }

    /// <summary>
    /// Also report selection changes through the event input (see <see cref="FlowEvent"/>). Opening a node is always reported;
    /// selection is not, because in Blazor each report is a round trip to the server.
    /// </summary>
    public bool EmitSelection { get; init; }

    /// <summary>
    /// Render the two hidden inputs (<c>[data-m-flow-value]</c> and <c>[data-m-flow-event]</c>) with the editor. Blazor turns this off and
    /// renders them itself next to the editor: Blazor only delivers <c>change</c> to a handler on the input itself, and markup written
    /// as a string cannot carry one.
    /// </summary>
    public bool Inputs { get; init; } = true;
}

/// <summary>
/// What the editor reports besides the graph: <c>open</c> (a node was double-clicked or Enter was pressed on it), <c>select</c>
/// (with <see cref="FlowOptions.EmitSelection"/>) and <c>pending</c> (a connection was let go over empty canvas). It arrives as JSON in
/// the editor's <c>[data-m-flow-event]</c> input, followed by <c>change</c>, so Blazor and htmx can listen like they do for the graph.
/// </summary>
public sealed record FlowEvent(string Type)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public string? Id { get; init; }

    public IReadOnlyList<string>? Nodes { get; init; }

    public string? Edge { get; init; }

    public string? From { get; init; }

    public string? Port { get; init; }

    /// <summary>Reads an event; false for a graph, empty text or anything else.</summary>
    public static bool TryParse(string? json, out FlowEvent flowEvent)
    {
        flowEvent = new FlowEvent("");
        if (string.IsNullOrWhiteSpace(json) || json.TrimStart()[0] != '{') return false;
        try
        {
            var parsed = JsonSerializer.Deserialize<FlowEvent>(json, Json);
            if (parsed is null || string.IsNullOrEmpty(parsed.Type)) return false;
            flowEvent = parsed;
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}

/// <summary>The sizes the editor and the server markup agree on: a node's width and height, where its ports sit and how a connection curves.</summary>
public static class FlowGeometry
{
    public const double NodeWidth = 220;

    /// <summary>Tall enough for one row per output, never less than a single-output node.</summary>
    public static double NodeHeight(int outputs) => Math.Max(64, 28 + 24 * outputs);

    /// <summary>The y of output <paramref name="index"/> from the node's top.</summary>
    public static double OutputY(int index, int outputs) => outputs <= 1 ? NodeHeight(outputs) / 2 : 26 + 24 * index;

    public static double InputY(int outputs) => NodeHeight(outputs) / 2;

    /// <summary>A cubic curve from an output (x1, y1) to an input (x2, y2) that leaves and arrives horizontally.</summary>
    public static string EdgePath(double x1, double y1, double x2, double y2)
    {
        var dx = Math.Max(40, Math.Abs(x2 - x1) * 0.5);
        string F(double v) => Math.Round(v, 1).ToString(CultureInfo.InvariantCulture);
        return $"M{F(x1)},{F(y1)} C{F(x1 + dx)},{F(y1)} {F(x2 - dx)},{F(y2)} {F(x2)},{F(y2)}";
    }
}

/// <summary>
/// The visual workflow designer (an n8n or Zapier style canvas): a palette of node kinds, nodes you drag, connections you draw from an
/// output dot to an input dot, pan and zoom, undo and redo. This is the markup the first paint shows; <c>modulus-flow.js</c> (loaded
/// when the editor appears) makes it interactive and keeps the same DOM, so Blazor, MVC and Razor Pages behave alike. Every change
/// writes the graph JSON to <c>[data-m-flow-value]</c> and raises <c>change</c> there, plus <c>m-flow-change</c> on the editor.
/// </summary>
public static class FlowHtml
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static string Editor(FlowGraph graph, FlowCatalog catalog, FlowOptions? options = null)
    {
        var o = options ?? new FlowOptions();
        var sb = new StringBuilder();
        var cls = "m-flow" + (o.ReadOnly ? " m-flow-readonly" : "") + (o.ShowPalette && !o.ReadOnly ? " m-flow-has-palette" : "") +
                  (graph.Nodes.Count == 0 ? " m-flow-empty" : "") + (string.IsNullOrEmpty(o.CssClass) ? "" : " " + o.CssClass);
        // opacity:0 until the stylesheet and script are in (the script clears it), so the first paint is never unstyled.
        sb.Append($"""<div class="{E(cls)}" data-m-flow data-grid="{o.Grid}"{(o.ReadOnly ? " data-readonly" : "")}{(o.EmitSelection ? " data-m-flow-select" : "")}{(o.Inputs ? "" : " data-m-flow-outside")} role="application" aria-label="{E(o.Label)}" aria-describedby="m-flow-help" """);
        sb.Append($"""data-catalog="{E(CatalogJson(catalog))}" style="height:{o.Height}px;opacity:0">""");

        if (o.ShowPalette && !o.ReadOnly) Palette(sb, catalog);

        sb.Append("""<div class="m-flow-main"><div class="m-flow-viewport" tabindex="0" data-m-flow-viewport><div class="m-flow-stage" data-m-flow-stage>""");
        sb.Append("""<svg class="m-flow-edges" width="1" height="1" aria-hidden="true">""");
        foreach (var e in graph.Edges)
            if (EdgePath(graph, catalog, e) is { } d)
                sb.Append($"""<path class="m-flow-edge-hit" data-edge="{E(e.Id)}" d="{d}"/><path class="m-flow-edge" data-edge="{E(e.Id)}" data-from="{E(e.From)}" data-port="{E(e.FromPort)}" data-to="{E(e.To)}" d="{d}"/>""");
        sb.Append("</svg>");
        foreach (var n in graph.Nodes) Node(sb, n, catalog.Find(n.Type), graph);
        sb.Append("</div>");
        sb.Append("""<div class="m-flow-hint" aria-hidden="true">Pick a node on the left, or drag one here, then drag from its dot to connect it.</div></div>""");
        if (o.ShowToolbar) Toolbar(sb, o);
        sb.Append("</div>");

        if (o.Inputs)
        {
            sb.Append($"""<input type="hidden" data-m-flow-value{(o.Name is null ? "" : $" name=\"{E(o.Name)}\"")} value="{E(graph.ToJson())}" />""");
            sb.Append("""<input type="hidden" data-m-flow-event />""");
        }
        sb.Append("""<span class="m-flow-live" data-m-flow-live aria-live="polite" role="status"></span>""");
        sb.Append("""<span class="m-flow-live" id="m-flow-help">Tab to a node. Enter opens it, Delete removes it, arrow keys move it, C then Enter on another node connects them, U disconnects it, D disables it, Ctrl+Z undoes.</span>""");
        sb.Append("</div>");
        return sb.ToString();
    }

    /// <summary>The path of a connection between two nodes of the graph, or null when one of them (or the output) is missing.</summary>
    public static string? EdgePath(FlowGraph graph, FlowCatalog catalog, FlowEdge e)
    {
        var from = graph.Node(e.From);
        var to = graph.Node(e.To);
        if (from is null || to is null) return null;
        var ft = catalog.Find(from.Type);
        var tt = catalog.Find(to.Type);
        var outs = ft?.Outputs ?? [new FlowPort("out")];
        var index = Math.Max(0, outs.ToList().FindIndex(p => p.Id == e.FromPort));
        var toOuts = tt?.Outputs.Count ?? 1;
        return FlowGeometry.EdgePath(from.X + FlowGeometry.NodeWidth, from.Y + FlowGeometry.OutputY(index, outs.Count), to.X, to.Y + FlowGeometry.InputY(toOuts));
    }

    private static void Node(StringBuilder sb, FlowNode n, FlowNodeType? type, FlowGraph graph)
    {
        var outs = type?.Outputs ?? [new FlowPort("out")];
        var h = FlowGeometry.NodeHeight(outs.Count);
        var title = string.IsNullOrWhiteSpace(n.Title) ? type?.Title ?? n.Type : n.Title;
        var status = n.Status == FlowStatus.None ? "" : $" data-status=\"{n.Status.ToString().ToLowerInvariant()}\"";
        var label = $"{title}, {type?.Title ?? n.Type}" + (n.Disabled ? ", disabled" : "") + (n.Status == FlowStatus.None ? "" : $", {n.Status.ToString().ToLowerInvariant()}");
        sb.Append($"""<div class="m-flow-node{(n.Disabled ? " m-flow-disabled" : "")}{(type is { HasInput: false } ? " m-flow-trigger" : "")}" role="group" tabindex="0" data-id="{E(n.Id)}" data-type="{E(n.Type)}"{status} """);
        sb.Append($"""aria-label="{E(label)}" style="left:{F(n.X)}px;top:{F(n.Y)}px;height:{F(h)}px;--c:var(--m-series-{type?.Slot ?? 8})">""");
        sb.Append($"""<span class="m-flow-ico" aria-hidden="true">{ShellRenderer.Icon(type?.Icon ?? "question-circle", "m-flow-ti")}</span>""");
        sb.Append($"""<span class="m-flow-text"><span class="m-flow-title">{E(title)}</span><span class="m-flow-sub">{E(string.IsNullOrWhiteSpace(n.Note) ? type?.Title ?? n.Type : n.Note)}</span></span>""");
        sb.Append("""<span class="m-flow-state" aria-hidden="true"></span>""");
        if (type is null or { HasInput: true })
            sb.Append($"""<span class="m-flow-port m-flow-in" data-port="in" style="top:{F(FlowGeometry.InputY(outs.Count))}px"></span>""");
        for (var i = 0; i < outs.Count; i++)
            sb.Append($"""<span class="m-flow-port m-flow-out" data-port="{E(outs[i].Id)}" style="top:{F(FlowGeometry.OutputY(i, outs.Count))}px">{(outs.Count > 1 ? $"<span class=\"m-flow-plabel\">{E(outs[i].Label)}</span>" : "")}</span>""");
        sb.Append("</div>");
    }

    private static void Palette(StringBuilder sb, FlowCatalog catalog)
    {
        sb.Append("""<aside class="m-flow-palette" aria-label="Nodes"><div class="m-flow-search"><input type="search" class="ant-input" placeholder="Search nodes" aria-label="Search nodes" data-m-flow-search autocomplete="off" /></div><div class="m-flow-plist">""");
        foreach (var category in catalog.Categories)
        {
            sb.Append($"""<section class="m-flow-pgroup" data-category="{E(category)}"><h4>{E(category)}</h4>""");
            foreach (var t in catalog.Types.Where(t => t.Category == category))
            {
                sb.Append($"""<button type="button" class="m-flow-pitem" draggable="true" data-type="{E(t.Key)}" data-search="{E((t.Title + " " + t.Description + " " + t.Category).ToLowerInvariant())}" """);
                sb.Append($"""style="--c:var(--m-series-{t.Slot})"{(t.Description is null ? "" : $" title=\"{E(t.Description)}\"")}>""");
                sb.Append($"""<span class="m-flow-ico" aria-hidden="true">{ShellRenderer.Icon(t.Icon, "m-flow-ti")}</span><span class="m-flow-ptext"><b>{E(t.Title)}</b>{(t.Description is null ? "" : $"<small>{E(t.Description)}</small>")}</span></button>""");
            }
            sb.Append("</section>");
        }
        sb.Append("""<p class="m-flow-none" hidden>No node matches.</p></div></aside>""");
    }

    private static void Toolbar(StringBuilder sb, FlowOptions o)
    {
        sb.Append("""<div class="m-flow-toolbar" role="toolbar" aria-label="Canvas">""");
        void Button(string act, string icon, string label, bool editOnly = false) =>
            sb.Append($"""<button type="button" class="m-flow-btn" data-m-flow-act="{act}" aria-label="{label}" title="{label}"{(editOnly && o.ReadOnly ? " hidden" : "")}>{ShellRenderer.Icon(icon)}</button>""");
        // Phones: the palette is folded away until this button opens it (the stylesheet shows the button only there).
        if (o.ShowPalette && !o.ReadOnly)
            sb.Append($"""<button type="button" class="m-flow-btn m-flow-btn-palette" data-m-flow-act="palette" aria-label="Add a node" title="Add a node">{ShellRenderer.Icon("plus")}</button>""");
        Button("zoom-out", "zoom-out", "Zoom out");
        sb.Append("""<span class="m-flow-zoom" data-m-flow-zoom aria-live="off">100%</span>""");
        Button("zoom-in", "zoom-in", "Zoom in");
        Button("fit", "fullscreen", "Fit to screen");
        sb.Append("""<span class="m-flow-sep"></span>""");
        Button("undo", "undo", "Undo", true);
        Button("redo", "redo", "Redo", true);
        Button("layout", "layout", "Tidy up the layout", true);
        sb.Append("</div>");
    }

    private static string CatalogJson(FlowCatalog catalog) => JsonSerializer.Serialize(catalog.Types.Select(t => new
    {
        key = t.Key,
        title = t.Title,
        slot = t.Slot,
        hasInput = t.HasInput,
        outputs = t.Outputs.Select(p => new { id = p.Id, label = p.Label }),
        defaults = t.Defaults,
    }), Json);

    private static string F(double v) => Math.Round(v, 1).ToString(CultureInfo.InvariantCulture);

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
}
