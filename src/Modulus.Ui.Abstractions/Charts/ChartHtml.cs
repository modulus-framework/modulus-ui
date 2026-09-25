using System.Globalization;
using System.Net;
using System.Text;

namespace Modulus.Ui.Abstractions.Charts;

/// <summary>A named series; values align with the chart's labels.</summary>
public sealed record ChartSeries(string Name, IReadOnlyList<double> Values);

/// <summary>One part of a whole (donut).</summary>
public sealed record ChartSlice(string Label, double Value);

/// <summary>Display options shared by the multi-series charts.</summary>
public sealed record ChartOptions
{
    /// <summary>Plot height in px, excluding legend and x-axis labels.</summary>
    public int Height { get; init; } = 260;

    /// <summary>.NET numeric format for values in tooltips, legends and tables ("N0", "N1", "P0" …).</summary>
    public string ValueFormat { get; init; } = "N0";

    /// <summary>Text before / after every value, e.g. "$" or " h".</summary>
    public string Prefix { get; init; } = "";
    public string Suffix { get; init; } = "";

    /// <summary>Accessible name, also the caption of the screen-reader table.</summary>
    public string? Title { get; init; }

    /// <summary>Fill under the first series (10 % wash).</summary>
    public bool Area { get; init; }

    /// <summary>Stack columns instead of grouping them.</summary>
    public bool Stacked { get; init; }

    /// <summary>Show at most this many x-axis labels; every value stays reachable on hover and in the table.</summary>
    public int MaxXLabels { get; init; } = 12;

    /// <summary>Label shown in the donut centre under the total (e.g. "Orders").</summary>
    public string? CenterLabel { get; init; }

    /// <summary>Categorical slot for a single-series chart (1–8); series use slots 1, 2, 3 … in order.</summary>
    public int FirstSlot { get; init; } = 1;
}

/// <summary>
/// Server-rendered multi-series charts: line/area with a crosshair tooltip, grouped or stacked
/// columns, donut and heatmap. Pure HTML + SVG with CSS-only hover, so Blazor and MVC render
/// identical markup and nothing needs JavaScript. Every chart carries a screen-reader table.
/// <para>
/// Colour follows the data-viz method: series take the validated categorical slots
/// (--m-series-1 … 8) in fixed order; the heatmap uses one sequential blue ramp; marks are thin,
/// grid lines are solid hairlines, a legend appears for two or more series.
/// </para>
/// </summary>
public static class ChartHtml
{
    private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;

    // Line / area ----------------------------------------------------------------------------------

    public static string Line(IReadOnlyList<string> labels, IReadOnlyList<ChartSeries> series, ChartOptions? options = null)
    {
        var o = options ?? new ChartOptions();
        var n = labels.Count;
        if (n == 0 || series.Count == 0) return Empty(o);
        var max = series.SelectMany(s => s.Values).DefaultIfEmpty(0).Max();
        var ticks = ChartGeometry.NiceTicks(max);
        var top = ticks[^1] > 0 ? ticks[^1] : 1;

        var sb = new StringBuilder();
        sb.Append($"""<div class="m-chart m-xychart" role="figure" aria-label="{E(o.Title)}">""");
        Legend(sb, series, o, line: true);
        sb.Append($"""<div class="m-xy-plot" style="height:{o.Height}px" aria-hidden="true">""");
        Grid(sb, ticks, top, o);
        sb.Append("""<svg class="m-xy-svg" viewBox="0 0 1000 1000" preserveAspectRatio="none">""");
        for (var s = 0; s < series.Count; s++)
        {
            var points = Enumerable.Range(0, n).Select(i => (X: (i + 0.5) / n * 1000, Y: 1000 - Value(series[s], i) / top * 1000)).ToList();
            var line = new StringBuilder();
            for (var i = 0; i < points.Count; i++)
                line.Append(i == 0 ? 'M' : 'L').Append(F(points[i].X)).Append(',').Append(F(points[i].Y));
            var color = Slot(o, s);
            // One wash only: stacked translucent areas muddy each other, so later series stay lines.
            if (o.Area && s == 0)
                sb.Append($"""<path class="m-xy-area" d="{line}L{F(points[^1].X)},1000L{F(points[0].X)},1000Z" style="fill:{color}"/>""");
            sb.Append($"""<path class="m-xy-line" d="{line}" style="stroke:{color}"/>""");
        }
        sb.Append("</svg>");
        // End markers: the latest value of each series, ringed in the surface colour.
        for (var s = 0; s < series.Count; s++)
            sb.Append($"""<span class="m-xy-dot" style="left:{F((n - 0.5) / n * 100)}%;bottom:{Pct(Value(series[s], n - 1), top)}%;--c:{Slot(o, s)}"></span>""");
        // Hover columns: crosshair, a dot per series, one tooltip listing every series at that x.
        sb.Append("""<div class="m-xy-slots">""");
        for (var i = 0; i < n; i++)
        {
            sb.Append($"""<div class="m-xy-slot{(i >= n / 2 && n > 3 ? " m-flip" : "")}"><span class="m-xy-cross"></span>""");
            for (var s = 0; s < series.Count; s++)
                sb.Append($"""<span class="m-xy-hover-dot" style="bottom:{Pct(Value(series[s], i), top)}%;--c:{Slot(o, s)}"></span>""");
            Tooltip(sb, labels[i], series, i, o, line: true);
            sb.Append("</div>");
        }
        sb.Append("</div></div>");
        XAxis(sb, labels, o);
        Table(sb, labels, series, o);
        sb.Append("</div>");
        return sb.ToString();
    }

    // Columns (grouped or stacked) ------------------------------------------------------------------------

    public static string Columns(IReadOnlyList<string> labels, IReadOnlyList<ChartSeries> series, ChartOptions? options = null)
    {
        var o = options ?? new ChartOptions();
        var n = labels.Count;
        if (n == 0 || series.Count == 0) return Empty(o);
        var max = o.Stacked
            ? Enumerable.Range(0, n).Max(i => series.Sum(s => Value(s, i)))
            : series.SelectMany(s => s.Values).DefaultIfEmpty(0).Max();
        var ticks = ChartGeometry.NiceTicks(max);
        var top = ticks[^1] > 0 ? ticks[^1] : 1;

        var sb = new StringBuilder();
        sb.Append($"""<div class="m-chart m-xychart m-colchart" role="figure" aria-label="{E(o.Title)}">""");
        Legend(sb, series, o, line: false);
        sb.Append($"""<div class="m-xy-plot" style="height:{o.Height}px" aria-hidden="true">""");
        Grid(sb, ticks, top, o);
        sb.Append("""<div class="m-xy-slots">""");
        for (var i = 0; i < n; i++)
        {
            sb.Append($"""<div class="m-xy-slot m-col-slot{(i >= n / 2 && n > 3 ? " m-flip" : "")}">""");
            if (o.Stacked)
            {
                var total = series.Sum(s => Value(s, i));
                sb.Append($"""<div class="m-col-stack" style="height:{Pct(total, top)}%">""");
                for (var s = 0; s < series.Count; s++)
                {
                    var v = Value(series[s], i);
                    if (v <= 0) continue;
                    sb.Append($"""<span class="m-col-seg" style="flex-grow:{F(v)};--c:{Slot(o, s)}"></span>""");
                }
                sb.Append("</div>");
            }
            else
            {
                sb.Append("""<div class="m-col-group">""");
                for (var s = 0; s < series.Count; s++)
                    sb.Append($"""<span class="m-col-bar" style="height:{Pct(Value(series[s], i), top)}%;--c:{Slot(o, s)}"></span>""");
                sb.Append("</div>");
            }
            Tooltip(sb, labels[i], series, i, o, line: false);
            sb.Append("</div>");
        }
        sb.Append("</div></div>");
        XAxis(sb, labels, o);
        Table(sb, labels, series, o);
        sb.Append("</div>");
        return sb.ToString();
    }

    // Donut ------------------------------------------------------------------------------------------------

    /// <summary>Part-to-whole for up to six parts; the legend carries every value and share.</summary>
    public static string Donut(IReadOnlyList<ChartSlice> slices, ChartOptions? options = null, int size = 180)
    {
        var o = options ?? new ChartOptions();
        var total = slices.Sum(s => Math.Max(0, s.Value));
        var sb = new StringBuilder();
        sb.Append($"""<div class="m-chart m-donut" role="figure" aria-label="{E(o.Title)}">""");
        sb.Append($"""<div class="m-donut-ring" style="width:{size}px;height:{size}px" aria-hidden="true"><svg viewBox="0 0 100 100">""");
        const double r = 42, stroke = 12;
        var circumference = 2 * Math.PI * r;
        var gap = slices.Count(s => s.Value > 0) > 1 ? 1.2 : 0; // surface gap between segments
        sb.Append($"""<circle class="m-donut-track" cx="50" cy="50" r="{F(r)}" stroke-width="{F(stroke)}"/>""");
        var offset = 0d;
        for (var i = 0; i < slices.Count; i++)
        {
            var v = Math.Max(0, slices[i].Value);
            if (v <= 0 || total <= 0) continue;
            var len = v / total * circumference;
            var dash = Math.Max(len - gap, 0.1);
            sb.Append($"""<circle class="m-donut-seg" cx="50" cy="50" r="{F(r)}" stroke-width="{F(stroke)}" style="stroke:{Slot(o, i)}" stroke-dasharray="{F(dash)} {F(circumference - dash)}" stroke-dashoffset="{F(-offset)}"><title>{E(slices[i].Label)}: {E(Format(v, o))} ({E(Share(v, total))})</title></circle>""");
            offset += len;
        }
        sb.Append("</svg>");
        sb.Append($"""<div class="m-donut-center"><span class="m-donut-total">{E(Format(total, o))}</span>{(o.CenterLabel is null ? "" : $"""<span class="m-donut-caption">{E(o.CenterLabel)}</span>""")}</div></div>""");
        sb.Append("""<ul class="m-donut-legend">""");
        for (var i = 0; i < slices.Count; i++)
        {
            var v = Math.Max(0, slices[i].Value);
            sb.Append($"""<li><span class="m-key m-key-rect" style="--c:{Slot(o, i)}"></span><span class="m-donut-label">{E(slices[i].Label)}</span><span class="m-donut-value">{E(Format(v, o))}</span><span class="m-donut-share">{E(Share(v, total))}</span></li>""");
        }
        sb.Append("</ul></div>");
        return sb.ToString();
    }

    // Heatmap -------------------------------------------------------------------------------------------------

    /// <summary>
    /// Rows × columns of magnitudes on the sequential blue ramp (5 steps, lightest = near zero),
    /// with a scale legend and a tooltip per cell.
    /// </summary>
    public static string Heatmap(IReadOnlyList<string> rows, IReadOnlyList<string> columns, IReadOnlyList<IReadOnlyList<double>> values, ChartOptions? options = null)
    {
        var o = options ?? new ChartOptions();
        var max = values.SelectMany(r => r).DefaultIfEmpty(0).Max();
        var sb = new StringBuilder();
        sb.Append($"""<div class="m-chart m-heatmap" role="figure" aria-label="{E(o.Title)}">""");
        sb.Append($"""<div class="m-heat-grid" style="grid-template-columns:auto repeat({columns.Count},minmax(0,1fr))" aria-hidden="true"><span></span>""");
        foreach (var c in columns) sb.Append($"""<span class="m-heat-col">{E(c)}</span>""");
        for (var r = 0; r < rows.Count; r++)
        {
            sb.Append($"""<span class="m-heat-row">{E(rows[r])}</span>""");
            for (var c = 0; c < columns.Count; c++)
            {
                var v = r < values.Count && c < values[r].Count ? values[r][c] : 0;
                var step = max <= 0 ? 0 : Math.Min(4, (int)Math.Floor(v / max * 5 - 1e-9));
                sb.Append($"""<span class="m-heat-cell m-heat-{Math.Max(step, 0)}{(c >= columns.Count / 2 ? " m-flip" : "")}"><span class="m-tip"><span class="m-tip-value">{E(Format(v, o))}</span><span class="m-tip-label">{E(rows[r])} · {E(columns[c])}</span></span></span>""");
            }
        }
        sb.Append("</div>");
        sb.Append("""<div class="m-heat-scale" aria-hidden="true"><span>Less</span>""");
        for (var i = 0; i < 5; i++) sb.Append($"""<span class="m-heat-cell m-heat-{i}"></span>""");
        sb.Append("<span>More</span></div>");
        sb.Append($"""<table class="m-sr-only"><caption>{E(o.Title)}</caption><thead><tr><th></th>""");
        foreach (var c in columns) sb.Append($"<th scope=\"col\">{E(c)}</th>");
        sb.Append("</tr></thead><tbody>");
        for (var r = 0; r < rows.Count; r++)
        {
            sb.Append($"<tr><th scope=\"row\">{E(rows[r])}</th>");
            for (var c = 0; c < columns.Count; c++)
                sb.Append($"<td>{E(Format(r < values.Count && c < values[r].Count ? values[r][c] : 0, o))}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table></div>");
        return sb.ToString();
    }

    // Parts ----------------------------------------------------------------------------------------------------

    private static void Legend(StringBuilder sb, IReadOnlyList<ChartSeries> series, ChartOptions o, bool line)
    {
        if (series.Count < 2) return;
        sb.Append("""<ul class="m-legend">""");
        for (var s = 0; s < series.Count; s++)
            sb.Append($"""<li><span class="m-key {(line ? "m-key-line" : "m-key-rect")}" style="--c:{Slot(o, s)}"></span>{E(series[s].Name)}</li>""");
        sb.Append("</ul>");
    }

    private static void Grid(StringBuilder sb, IReadOnlyList<double> ticks, double top, ChartOptions o)
    {
        foreach (var tick in ticks)
            sb.Append($"""<div class="m-xy-grid{(tick == 0 ? " m-xy-base" : "")}" style="bottom:{Pct(tick, top)}%"><span class="m-xy-y">{E(o.Prefix + ChartGeometry.Compact(tick) + (o.Suffix.StartsWith(' ') ? "" : o.Suffix))}</span></div>""");
    }

    private static void XAxis(StringBuilder sb, IReadOnlyList<string> labels, ChartOptions o)
    {
        var every = Math.Max(1, (int)Math.Ceiling(labels.Count / (double)Math.Max(1, o.MaxXLabels)));
        sb.Append("""<div class="m-xy-x" aria-hidden="true">""");
        for (var i = 0; i < labels.Count; i++)
            sb.Append($"""<span{(i % every == 0 ? "" : " class=\"m-skip\"")}>{E(labels[i])}</span>""");
        sb.Append("</div>");
    }

    private static void Tooltip(StringBuilder sb, string label, IReadOnlyList<ChartSeries> series, int i, ChartOptions o, bool line)
    {
        sb.Append($"""<div class="m-xy-tip"><div class="m-xy-tip-title">{E(label)}</div>""");
        for (var s = 0; s < series.Count; s++)
            sb.Append($"""<div class="m-xy-tip-row"><span class="m-key {(line ? "m-key-line" : "m-key-line")}" style="--c:{Slot(o, s)}"></span><strong>{E(Format(Value(series[s], i), o))}</strong><span>{E(series[s].Name)}</span></div>""");
        if (o.Stacked && series.Count > 1)
            sb.Append($"""<div class="m-xy-tip-row m-xy-tip-total"><strong>{E(Format(series.Sum(s => Value(s, i)), o))}</strong><span>Total</span></div>""");
        sb.Append("</div>");
    }

    private static void Table(StringBuilder sb, IReadOnlyList<string> labels, IReadOnlyList<ChartSeries> series, ChartOptions o)
    {
        sb.Append($"""<table class="m-sr-only"><caption>{E(o.Title)}</caption><thead><tr><th scope="col"></th>""");
        foreach (var s in series) sb.Append($"<th scope=\"col\">{E(s.Name)}</th>");
        sb.Append("</tr></thead><tbody>");
        for (var i = 0; i < labels.Count; i++)
        {
            sb.Append($"<tr><th scope=\"row\">{E(labels[i])}</th>");
            foreach (var s in series) sb.Append($"<td>{E(Format(Value(s, i), o))}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");
    }

    private static string Empty(ChartOptions o) =>
        $"""<div class="m-chart m-chart-empty" style="height:{o.Height}px">No data</div>""";

    private static double Value(ChartSeries s, int i) => i < s.Values.Count ? Math.Max(0, s.Values[i]) : 0;

    private static string Slot(ChartOptions o, int index) => $"var(--m-series-{(o.FirstSlot - 1 + index) % 8 + 1})";

    private static string Pct(double value, double top) => F(Math.Clamp(value / top * 100, 0, 100));

    public static string Format(double value, ChartOptions o) => o.Prefix + value.ToString(o.ValueFormat, Culture) + o.Suffix;

    private static string Share(double value, double total) => total <= 0 ? "0%" : (value / total).ToString("P0", Culture).Replace(" %", "%");

    private static string F(double v) => Math.Round(v, 2).ToString(Culture);

    private static string E(string? s) => WebUtility.HtmlEncode(s ?? "");
}
