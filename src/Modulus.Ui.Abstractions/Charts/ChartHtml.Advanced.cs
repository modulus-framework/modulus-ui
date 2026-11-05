using System.Text;

namespace Modulus.Ui.Abstractions.Charts;

/// <summary>One trading period of a candlestick chart: open, high, low and close, with an optional volume.</summary>
public sealed record Candle(string Label, double Open, double High, double Low, double Close, double Volume = 0);

/// <summary>A flow from one node to another in a Sankey chart. Nodes are named by the links that mention them.</summary>
public sealed record SankeyLink(string From, string To, double Value);

/// <summary>
/// One box of an org chart. <paramref name="ParentId"/> is null (or not in the list) for a root, so several roots draw several trees.
/// <paramref name="Href"/> makes the title a link (relative, http or https only).
/// </summary>
public sealed record OrgNode(string Id, string? ParentId, string Title, string? Subtitle = null, string? Badge = null, string? Href = null);

/// <summary>
/// Advanced visuals that paid suites (Syncfusion, DevExpress, Telerik) sell: radar, candlestick, Sankey and org charts.
/// Same contract as the other charts: server-rendered HTML + SVG, CSS-only hover, a screen-reader table (the org chart is a
/// nested list), so Blazor, MVC and Razor Pages render identical markup and nothing needs JavaScript.
/// </summary>
public static partial class ChartHtml
{
    // Radar ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// Several measures of one or more subjects on shared spokes (supplier scorecards, skills, KPIs per plant). Needs at least three axes.
    /// The scale runs from zero to <paramref name="max"/>, or to the next tidy tick above the largest value.
    /// </summary>
    public static string Radar(IReadOnlyList<string> axes, IReadOnlyList<ChartSeries> series, ChartOptions? options = null, double? max = null)
    {
        var o = options ?? new ChartOptions();
        if (axes.Count < 3 || series.Count == 0) return Empty(o);
        var data = series.Max(s => s.Values.Count == 0 ? 0 : s.Values.Take(axes.Count).Max());
        var ticks = max is > 0 ? ChartGeometry.NiceTicks(max.Value, 4) : ChartGeometry.NiceTicks(data <= 0 ? 1 : data, 4);
        var top = max is > 0 ? max.Value : ticks[^1];
        if (top <= 0) top = 1;
        const double Cx = 50, Cy = 50, R = 34;
        double Ang(int i) => -Math.PI / 2 + 2 * Math.PI * i / axes.Count;
        (double X, double Y) At(int i, double fraction) => (Cx + R * fraction * Math.Cos(Ang(i)), Cy + R * fraction * Math.Sin(Ang(i)));
        string Poly(Func<int, (double X, double Y)> point) => string.Join(' ', Enumerable.Range(0, axes.Count).Select(i => { var p = point(i); return $"{F(p.X)},{F(p.Y)}"; }));

        var sb = new StringBuilder();
        sb.Append($"""<div class="m-chart m-radar" role="figure" aria-label="{E(o.Title)}">""");
        if (series.Count > 1)
        {
            sb.Append("""<ul class="m-legend">""");
            for (var s = 0; s < series.Count; s++)
                sb.Append($"""<li><span class="m-key m-key-rect" style="--c:{Slot(o, s)}"></span>{E(series[s].Name)}</li>""");
            sb.Append("</ul>");
        }
        sb.Append($"""<div class="m-radar-plot" style="max-width:{Math.Max(o.Height, 200)}px"><svg viewBox="0 0 100 100" aria-hidden="true">""");
        var rings = ticks.Where(t => t > 0 && t < top * 0.999).Append(top).ToList();
        foreach (var tick in rings)
            sb.Append($"""<polygon class="m-radar-ring" points="{Poly(i => At(i, tick / top))}"/>""");
        for (var i = 0; i < axes.Count; i++)
        {
            var end = At(i, 1);
            sb.Append($"""<line class="m-radar-spoke" x1="{F(Cx)}" y1="{F(Cy)}" x2="{F(end.X)}" y2="{F(end.Y)}"/>""");
        }
        for (var s = 0; s < series.Count; s++)
            sb.Append($"""<polygon class="m-radar-area" style="--c:{Slot(o, s)}" points="{Poly(i => At(i, Math.Clamp(RadarValue(series[s], i) / top, 0, 1)))}"/>""");
        sb.Append("</svg>");
        // Ring values along the first spoke, then the axis names just outside the outer ring.
        foreach (var tick in rings)
        {
            var p = At(0, tick / top);
            sb.Append($"""<span class="m-radar-tick" aria-hidden="true" style="left:{F(p.X)}%;top:{F(p.Y)}%">{E(AxisLabel(tick, o))}</span>""");
        }
        for (var i = 0; i < axes.Count; i++)
        {
            var p = At(i, 1.2);
            var side = Math.Abs(p.X - Cx) < 4 ? "" : p.X > Cx ? " m-radar-right" : " m-radar-left";
            sb.Append($"""<span class="m-radar-axis{side}" aria-hidden="true" style="left:{F(p.X)}%;top:{F(p.Y)}%" title="{E(axes[i])}">{E(axes[i])}</span>""");
        }
        // Points last, so they sit on top and keep their hover.
        for (var s = 0; s < series.Count; s++)
            for (var i = 0; i < axes.Count; i++)
            {
                var v = RadarValue(series[s], i);
                var p = At(i, Math.Clamp(v / top, 0, 1));
                var flip = p.X > 65 ? " m-flip" : p.X < 35 ? " m-flip-l" : "";
                sb.Append($"""<span class="m-radar-pt m-hov{flip}" style="left:{F(p.X)}%;top:{F(p.Y)}%;--c:{Slot(o, s)}"><span class="m-tip"><span class="m-tip-label">{E(axes[i])}{(series.Count > 1 ? " · " + E(series[s].Name) : "")}</span><span class="m-tip-value">{E(Format(v, o))}</span></span></span>""");
            }
        sb.Append("</div>");
        SimpleTable(sb, o.Title, series.Select(s => s.Name).ToList(), Enumerable.Range(0, axes.Count).Select(i => (axes[i], series.Select(s => Format(RadarValue(s, i), o)).ToArray())));
        sb.Append("</div>");
        return sb.ToString();
    }

    private static double RadarValue(ChartSeries s, int i) => i < s.Values.Count ? Math.Max(0, s.Values[i]) : 0;

    // Candlestick ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Open, high, low and close per period (share, FX or commodity prices, plant output ranges). A body runs from open to close, a wick
    /// from low to high; a period that closed at or above its open is "up". With volumes, a strip of bars sits under the plot.
    /// </summary>
    public static string Candlestick(IReadOnlyList<Candle> candles, ChartOptions? options = null)
    {
        var o = options ?? new ChartOptions();
        if (candles.Count == 0) return Empty(o);
        var lo = candles.Min(c => Math.Min(Math.Min(c.Low, c.High), Math.Min(c.Open, c.Close)));
        var hi = candles.Max(c => Math.Max(Math.Max(c.Low, c.High), Math.Max(c.Open, c.Close)));
        // Prices rarely start at zero: the axis hugs the data, with a tidy step.
        var span = hi - lo <= 0 ? Math.Max(Math.Abs(hi), 1) : hi - lo;
        var step = ChartGeometryStep(span / 4);
        double y0 = Math.Floor((lo - span * 0.05) / step) * step, y1 = Math.Ceiling((hi + span * 0.05) / step) * step;
        if (y1 <= y0) y1 = y0 + step;
        double Yp(double v) => Math.Clamp((v - y0) / (y1 - y0) * 100, 0, 100);
        var hasVolume = candles.Any(c => c.Volume > 0);
        var maxVolume = hasVolume ? candles.Max(c => c.Volume) : 0;
        var every = Math.Max(1, (int)Math.Ceiling(candles.Count / (double)Math.Max(1, o.MaxXLabels)));

        var sb = new StringBuilder();
        sb.Append($"""<div class="m-chart m-xychart m-candle" role="figure" aria-label="{E(o.Title)}">""");
        sb.Append($"""<ul class="m-legend"><li><span class="m-key m-key-rect" style="--c:{Positive}"></span>Up (close at or above open)</li><li><span class="m-key m-key-rect" style="--c:{Negative}"></span>Down</li></ul>""");
        sb.Append($"""<div class="m-xy-plot" style="height:{o.Height}px" aria-hidden="true">""");
        for (var v = y0; v <= y1 + step * 0.001; v += step)
            sb.Append($"""<div class="m-xy-grid" style="bottom:{F(Yp(v))}%"><span class="m-xy-y">{E(AxisLabel(v, o))}</span></div>""");
        sb.Append("""<div class="m-candle-row">""");
        for (var i = 0; i < candles.Count; i++)
        {
            var c = candles[i];
            var up = c.Close >= c.Open;
            var bodyLo = Math.Min(c.Open, c.Close);
            var bodyHi = Math.Max(c.Open, c.Close);
            var wickLo = Math.Min(c.Low, bodyLo);
            var wickHi = Math.Max(c.High, bodyHi);
            var change = c.Open == 0 ? 0 : (c.Close - c.Open) / c.Open;
            var flip = i > candles.Count * 0.65 ? " m-flip" : i < candles.Count * 0.2 ? " m-flip-l" : "";
            sb.Append($"""<span class="m-candle-slot m-hov{flip}" style="--c:{(up ? Positive : Negative)}">""");
            sb.Append($"""<span class="m-candle-wick" style="bottom:{F(Yp(wickLo))}%;height:{F(Math.Max(0.4, Yp(wickHi) - Yp(wickLo)))}%"></span>""");
            sb.Append($"""<span class="m-candle-body" style="bottom:{F(Yp(bodyLo))}%;height:{F(Math.Max(0.5, Yp(bodyHi) - Yp(bodyLo)))}%"></span>""");
            sb.Append($"""<span class="m-tip"><span class="m-tip-label">{E(c.Label)} · {(up ? "up" : "down")} {E(change.ToString("+0.0%;−0.0%;0.0%", Culture))}</span>""");
            sb.Append($"""<span class="m-tip-value">Open {E(Format(c.Open, o))} · High {E(Format(c.High, o))}</span><span class="m-tip-value">Low {E(Format(c.Low, o))} · Close {E(Format(c.Close, o))}</span>""");
            if (c.Volume > 0) sb.Append($"""<span class="m-tip-value">Volume {E(c.Volume.ToString("N0", Culture))}</span>""");
            sb.Append("</span></span>");
        }
        sb.Append("</div></div>");
        if (hasVolume)
        {
            sb.Append("""<div class="m-candle-vol" aria-hidden="true">""");
            foreach (var c in candles)
                sb.Append($"""<span class="m-candle-vbar" style="height:{Pct(c.Volume, maxVolume)}%;--c:{(c.Close >= c.Open ? Positive : Negative)}"></span>""");
            sb.Append("</div>");
        }
        sb.Append("""<div class="m-candle-x" aria-hidden="true">""");
        for (var i = 0; i < candles.Count; i++)
            sb.Append($"""<span>{(i % every == 0 ? E(candles[i].Label) : "")}</span>""");
        sb.Append("</div>");
        var headers = new List<string> { "Open", "High", "Low", "Close", "Change" };
        if (hasVolume) headers.Add("Volume");
        SimpleTable(sb, o.Title, headers, candles.Select(c =>
        {
            var cells = new List<string> { Format(c.Open, o), Format(c.High, o), Format(c.Low, o), Format(c.Close, o), (c.Open == 0 ? 0 : (c.Close - c.Open) / c.Open).ToString("+0.0%;−0.0%;0.0%", Culture) };
            if (hasVolume) cells.Add(c.Volume.ToString("N0", Culture));
            return (c.Label, cells.ToArray());
        }));
        sb.Append("</div>");
        return sb.ToString();
    }

    private static double ChartGeometryStep(double raw)
    {
        if (raw <= 0) return 1;
        var pow = Math.Pow(10, Math.Floor(Math.Log10(raw)));
        var f = raw / pow;
        return (f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10) * pow;
    }

    // Sankey ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Where a quantity goes: material from suppliers through plants to customers, a budget through departments to cost types, cash from
    /// revenue to profit. Columns follow the longest path; a node is as tall as the larger of what enters and leaves it. The link that
    /// closes a loop, and zero or negative flows, are left out.
    /// </summary>
    public static string Sankey(IReadOnlyList<SankeyLink> links, ChartOptions? options = null)
    {
        var o = options ?? new ChartOptions();
        var valid = links.Where(l => l.Value > 0 && !string.IsNullOrWhiteSpace(l.From) && !string.IsNullOrWhiteSpace(l.To) && l.From != l.To).ToList();
        if (valid.Count == 0) return Empty(o);

        var names = new List<string>();
        foreach (var l in valid) { if (!names.Contains(l.From)) names.Add(l.From); if (!names.Contains(l.To)) names.Add(l.To); }
        var index = names.Select((n, i) => (n, i)).ToDictionary(x => x.n, x => x.i);

        // A link that closes a loop (it leads back to a node still being walked) is dropped; the rest of the graph stays.
        var state = new int[names.Count]; // 0 new, 1 on the current path, 2 finished
        var back = new HashSet<SankeyLink>();
        var outgoing = valid.GroupBy(l => index[l.From]).ToDictionary(g => g.Key, g => g.ToList());
        void Walk(int n)
        {
            state[n] = 1;
            foreach (var l in outgoing.GetValueOrDefault(n) ?? [])
            {
                var t = index[l.To];
                if (state[t] == 1) back.Add(l);
                else if (state[t] == 0) Walk(t);
            }
            state[n] = 2;
        }
        for (var i = 0; i < names.Count; i++) if (state[i] == 0) Walk(i);
        valid = valid.Where(l => !back.Contains(l)).ToList();
        if (valid.Count == 0) return Empty(o);

        // Layers by longest path over what is left (it has no loops now, so Kahn visits every node).
        var layer = new int[names.Count];
        var indegree = new int[names.Count];
        foreach (var l in valid) indegree[index[l.To]]++;
        var queue = new Queue<int>(Enumerable.Range(0, names.Count).Where(i => indegree[i] == 0));
        var remaining = valid.GroupBy(l => index[l.From]).ToDictionary(g => g.Key, g => g.ToList());
        while (queue.Count > 0)
        {
            var n = queue.Dequeue();
            foreach (var l in remaining.GetValueOrDefault(n) ?? [])
            {
                var t = index[l.To];
                layer[t] = Math.Max(layer[t], layer[n] + 1);
                if (--indegree[t] == 0) queue.Enqueue(t);
            }
        }

        var columns = layer.Max() + 1;
        var inSum = new double[names.Count];
        var outSum = new double[names.Count];
        foreach (var l in valid) { outSum[index[l.From]] += l.Value; inSum[index[l.To]] += l.Value; }
        var size = Enumerable.Range(0, names.Count).Select(i => Math.Max(inSum[i], outSum[i])).ToArray();
        var used = Enumerable.Range(0, names.Count).Where(i => size[i] > 0).ToList();

        // One vertical scale for every column, set by the fullest one, so equal flows are equally thick everywhere.
        const double Gap = 10;
        double height = o.Height;
        var scale = Enumerable.Range(0, columns)
            .Select(c => used.Where(i => layer[i] == c).ToList())
            .Where(col => col.Count > 0)
            .Min(col => Math.Max(0.0001, (height - Gap * (col.Count - 1)) / col.Sum(i => size[i])));
        var top = new double[names.Count];
        for (var c = 0; c < columns; c++)
        {
            var y = 0.0;
            foreach (var i in used.Where(i => layer[i] == c).OrderByDescending(i => size[i]))
            {
                top[i] = y;
                y += size[i] * scale + Gap;
            }
        }

        const double NodePct = 2;
        double Left(int i) => columns == 1 ? 0 : layer[i] * (100 - NodePct) / (columns - 1);
        var outOffset = new double[names.Count];
        var inOffset = new double[names.Count];

        var sb = new StringBuilder();
        sb.Append($"""<div class="m-chart m-sankey" role="figure" aria-label="{E(o.Title)}"><div class="m-sk-plot" style="height:{o.Height}px">""");
        sb.Append($"""<svg viewBox="0 0 100 {F(height)}" preserveAspectRatio="none" aria-hidden="true">""");
        // Sort each node's flows by where they lead, so ribbons cross as little as they can.
        var ordered = valid.OrderBy(l => layer[index[l.From]]).ThenBy(l => top[index[l.To]]).ToList();
        var inOrder = valid.GroupBy(l => index[l.To]).ToDictionary(g => g.Key, g => g.OrderBy(l => top[index[l.From]]).ToList());
        var outOrder = valid.GroupBy(l => index[l.From]).ToDictionary(g => g.Key, g => g.OrderBy(l => top[index[l.To]]).ToList());
        var yStart = new Dictionary<SankeyLink, double>();
        var yEnd = new Dictionary<SankeyLink, double>();
        foreach (var (node, list) in outOrder) foreach (var l in list) { yStart[l] = top[node] + outOffset[node] + l.Value * scale / 2; outOffset[node] += l.Value * scale; }
        foreach (var (node, list) in inOrder) foreach (var l in list) { yEnd[l] = top[node] + inOffset[node] + l.Value * scale / 2; inOffset[node] += l.Value * scale; }
        foreach (var l in ordered)
        {
            int a = index[l.From], b = index[l.To];
            double x0 = Left(a) + NodePct, x1 = Left(b), xm = (x0 + x1) / 2;
            sb.Append($"""<path class="m-sk-link" style="--c:{Slot(o, a)};stroke-width:{F(Math.Max(1, l.Value * scale))}px" d="M{F(x0)},{F(yStart[l])} C{F(xm)},{F(yStart[l])} {F(xm)},{F(yEnd[l])} {F(x1)},{F(yEnd[l])}"><title>{E(l.From)} → {E(l.To)}: {E(Format(l.Value, o))}</title></path>""");
        }
        sb.Append("</svg>");
        foreach (var i in used)
        {
            var last = layer[i] == columns - 1 && columns > 1;
            var flip = Left(i) > 60 ? " m-flip" : "";
            sb.Append($"""<span class="m-sk-node m-hov{flip}" style="left:{F(Left(i))}%;top:{F(top[i])}px;height:{F(Math.Max(2, size[i] * scale))}px;--c:{Slot(o, i)}"><span class="m-tip"><span class="m-tip-label">{E(names[i])}</span><span class="m-tip-value">In {E(Format(inSum[i], o))} · Out {E(Format(outSum[i], o))}</span></span></span>""");
            sb.Append($"""<span class="m-sk-label{(last ? " m-sk-label-left" : "")}" aria-hidden="true" style="{(last ? $"right:{F(100 - Left(i))}%" : $"left:{F(Left(i) + NodePct)}%")};top:{F(top[i] + Math.Max(2, size[i] * scale) / 2)}px"><strong>{E(names[i])}</strong> {E(Compact(size[i], o))}</span>""");
        }
        sb.Append("</div>");
        SimpleTable(sb, o.Title, ["Flow"], valid.Select(l => ($"{l.From} → {l.To}", new[] { Format(l.Value, o) })));
        sb.Append("</div>");
        return sb.ToString();
    }

    // Org chart ------------------------------------------------------------------------------------------------

    /// <summary>
    /// A reporting hierarchy (people, cost centres, accounts, a bill of materials) as boxes joined by lines, drawn as nested lists so a
    /// screen reader walks it as a tree and it needs no script. Boxes show a title, a subtitle, a badge and the number of direct children.
    /// </summary>
    public static string OrgChart(IReadOnlyList<OrgNode> nodes, ChartOptions? options = null)
    {
        var o = options ?? new ChartOptions();
        if (nodes.Count == 0) return Empty(o);
        var ids = new HashSet<string>(nodes.Select(n => n.Id));
        var children = nodes.Where(n => n.ParentId is not null && ids.Contains(n.ParentId) && n.ParentId != n.Id)
            .GroupBy(n => n.ParentId!).ToDictionary(g => g.Key, g => g.ToList());
        var roots = nodes.Where(n => n.ParentId is null || !ids.Contains(n.ParentId) || n.ParentId == n.Id).ToList();
        var seen = new HashSet<string>();

        void Write(StringBuilder sb, OrgNode node, int depth)
        {
            if (!seen.Add(node.Id) || depth > 40) return;
            var kids = children.TryGetValue(node.Id, out var k) ? k.Where(c => !seen.Contains(c.Id)).ToList() : [];
            sb.Append("<li><div class=\"m-org-node\">");
            var title = SafeHref(node.Href) is { } href ? $"""<a class="m-org-title" href="{E(href)}">{E(node.Title)}</a>""" : $"""<span class="m-org-title">{E(node.Title)}</span>""";
            sb.Append(title);
            if (!string.IsNullOrEmpty(node.Subtitle)) sb.Append($"""<span class="m-org-sub">{E(node.Subtitle)}</span>""");
            if (!string.IsNullOrEmpty(node.Badge) || kids.Count > 0)
            {
                sb.Append("""<span class="m-org-meta">""");
                if (!string.IsNullOrEmpty(node.Badge)) sb.Append($"""<span class="m-org-badge">{E(node.Badge)}</span>""");
                if (kids.Count > 0) sb.Append($"""<span class="m-org-count" title="{kids.Count} direct">{kids.Count}</span>""");
                sb.Append("</span>");
            }
            sb.Append("</div>");
            if (kids.Count > 0)
            {
                sb.Append("<ul>");
                foreach (var child in kids) Write(sb, child, depth + 1);
                sb.Append("</ul>");
            }
            sb.Append("</li>");
        }

        var body = new StringBuilder();
        foreach (var root in roots) Write(body, root, 0);
        // A cycle with no root (A reports to B, B to A) would draw nothing: start from the first node instead.
        if (body.Length == 0) Write(body, nodes[0], 0);
        return $"""<div class="m-chart m-orgchart" role="figure" aria-label="{E(o.Title)}"><ul class="m-org">{body}</ul></div>""";
    }

    /// <summary>Relative, http and https links only: a data or script URL in a node never reaches the page.</summary>
    private static string? SafeHref(string? href)
    {
        if (string.IsNullOrWhiteSpace(href)) return null;
        href = href.Trim();
        if (href.StartsWith("//", StringComparison.Ordinal)) return null;
        if (href.StartsWith('/') || href.StartsWith('#') || href.StartsWith('?')) return href;
        return Uri.TryCreate(href, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps) ? href
            : !href.Contains(':') ? href : null;
    }
}
