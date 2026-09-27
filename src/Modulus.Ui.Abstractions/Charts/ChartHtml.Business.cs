using System.Text;

namespace Modulus.Ui.Abstractions.Charts;

/// <summary>One bar of a waterfall: a signed change, or (<paramref name="IsTotal"/>) a subtotal drawn from zero to the running total.</summary>
public sealed record WaterfallStep(string Label, double Value, bool IsTotal = false);

/// <summary>
/// One row of a bullet chart: the actual against a target, over optional qualitative bands
/// (ascending upper bounds, e.g. [60, 85, 100] for poor / fair / good).
/// </summary>
public sealed record BulletItem(string Label, double Actual, double Target, IReadOnlyList<double>? Bands = null, string? Note = null);

/// <summary>A point of a scatter or bubble chart. <paramref name="Group"/> indexes <see cref="ScatterOptions.Groups"/>.</summary>
public sealed record ScatterPoint(string Label, double X, double Y, double Size = 0, int Group = 0);

/// <summary>Scatter-specific options: the y axis uses <see cref="ChartOptions"/>, the x axis these.</summary>
public sealed record ScatterOptions
{
    public string? XTitle { get; init; }
    public string? YTitle { get; init; }

    /// <summary>What bubble area encodes (tooltip and caption); points are equal-size dots when no point has a size.</summary>
    public string? SizeTitle { get; init; }

    public string XFormat { get; init; } = "N0";
    public string XPrefix { get; init; } = "";
    public string XSuffix { get; init; } = "";

    /// <summary>Group names (legend). Keep to three: past three, categorical hues stop being safely distinguishable on a scatter.</summary>
    public IReadOnlyList<string> Groups { get; init; } = [];

    /// <summary>Hairlines at the mean x and mean y, splitting the plot into quadrants (e.g. margin × volume).</summary>
    public bool Quadrants { get; init; }
}

/// <summary>
/// Business / ERP charts: ranked bars, combo (on <see cref="Combo"/>), waterfall, funnel, gauge, bullet,
/// variance, scatter / bubble, treemap, Pareto and stacked bars. Same contract as the core charts:
/// server-rendered HTML + SVG, CSS-only hover, a screen-reader table on every chart, and colour by job —
/// categorical slots for identity, the blue ordinal ramp for ordered stages, the blue ↔ red diverging pair
/// for sign (favourable / unfavourable), and a neutral grey for totals and "Other".
/// </summary>
public static partial class ChartHtml
{
    private const string Neutral = "var(--m-chart-neutral)";
    private const string Positive = "var(--m-div-pos)";
    private const string Negative = "var(--m-div-neg)";

    // Ranked bars ----------------------------------------------------------------------------------------------

    /// <summary>
    /// Horizontal bars sorted largest first (top customers, products, vendors). With <paramref name="limit"/>,
    /// the tail folds into one "Other" row so the list stays short.
    /// </summary>
    public static string RankedBars(IReadOnlyList<ChartSlice> items, ChartOptions? options = null, int limit = 0, bool showShare = false)
    {
        var o = options ?? new ChartOptions();
        if (items.Count == 0) return Empty(o);
        var total = items.Sum(i => Math.Max(0, i.Value));
        var rows = items.OrderByDescending(i => i.Value).Select(i => (i.Label, Value: Math.Max(0, i.Value), Other: false)).ToList();
        if (limit > 1 && rows.Count > limit)
            rows = [.. rows.Take(limit - 1), ("Other", rows.Skip(limit - 1).Sum(r => r.Value), true)];
        var max = rows.Max(r => r.Value);

        var sb = new StringBuilder();
        sb.Append($"""<div class="m-chart m-rc{(showShare ? " m-rc-4" : "")}" role="figure" aria-label="{E(o.Title)}">""");
        foreach (var r in rows)
        {
            sb.Append($"""<div class="m-rc-row" aria-hidden="true"><span class="m-rc-label" title="{E(r.Label)}">{E(r.Label)}</span>""");
            sb.Append($"""<span class="m-rc-track"><span class="m-rc-bar" style="width:{Pct(r.Value, max)}%;--c:{(r.Other ? Neutral : Slot(o, 0))}"></span></span>""");
            sb.Append($"""<span class="m-rc-value">{E(Format(r.Value, o))}</span>""");
            if (showShare) sb.Append($"""<span class="m-rc-share">{E(Share(r.Value, total))}</span>""");
            sb.Append("</div>");
        }
        SimpleTable(sb, o.Title, ["Value", "Share"], rows.Select(r => (r.Label, new[] { Format(r.Value, o), Share(r.Value, total) })));
        sb.Append("</div>");
        return sb.ToString();
    }

    // Stacked bars ---------------------------------------------------------------------------------------------

    /// <summary>
    /// Horizontal stacked bars, one row per label (AR / AP aging per customer, stock by warehouse).
    /// <paramref name="normalize"/> scales every row to 100 %; <paramref name="ordinal"/> colours segments on the
    /// blue ramp (light → dark) for ordered buckets such as aging bands, instead of categorical slots.
    /// </summary>
    public static string StackedBars(IReadOnlyList<string> rows, IReadOnlyList<ChartSeries> series, ChartOptions? options = null, bool normalize = false, bool ordinal = false)
    {
        var o = options ?? new ChartOptions();
        if (rows.Count == 0 || series.Count == 0) return Empty(o);
        var totals = Enumerable.Range(0, rows.Count).Select(r => series.Sum(s => Value(s, r))).ToList();
        var max = totals.DefaultIfEmpty(0).Max();
        string Color(int s) => ordinal ? Ordinal(s, series.Count) : Slot(o, s);

        var sb = new StringBuilder();
        sb.Append($"""<div class="m-chart m-stackbars" role="figure" aria-label="{E(o.Title)}">""");
        if (series.Count > 1)
        {
            sb.Append("""<ul class="m-legend">""");
            for (var s = 0; s < series.Count; s++)
                sb.Append($"""<li><span class="m-key m-key-rect" style="--c:{Color(s)}"></span>{E(series[s].Name)}</li>""");
            sb.Append("</ul>");
        }
        sb.Append("""<div class="m-rc">""");
        for (var r = 0; r < rows.Count; r++)
        {
            var total = totals[r];
            sb.Append($"""<div class="m-rc-row" aria-hidden="true"><span class="m-rc-label" title="{E(rows[r])}">{E(rows[r])}</span><span class="m-rc-track">""");
            sb.Append($"""<span class="m-rc-stack" style="width:{(normalize ? "100" : Pct(total, max))}%">""");
            for (var s = 0; s < series.Count; s++)
            {
                var v = Value(series[s], r);
                if (v <= 0) continue;
                sb.Append($"""<span class="m-rc-seg m-hov{(s >= series.Count / 2 ? " m-flip" : "")}" style="flex-grow:{F(v)};--c:{Color(s)}"><span class="m-tip"><span class="m-tip-label">{E(rows[r])} · {E(series[s].Name)}</span><span class="m-tip-value">{E(Format(v, o))} ({E(Share(v, total))})</span></span></span>""");
            }
            sb.Append($"""</span></span><span class="m-rc-value">{E(Format(total, o))}</span></div>""");
        }
        sb.Append("</div>");
        Table(sb, rows, series, o);
        sb.Append("</div>");
        return sb.ToString();
    }

    // Waterfall ------------------------------------------------------------------------------------------------

    /// <summary>
    /// Bridge from a start value to an end value through signed changes (P&amp;L bridge, cash flow, price-volume-mix).
    /// Increases take the blue pole, decreases the red pole, totals neutral grey; each bar carries its change.
    /// </summary>
    public static string Waterfall(IReadOnlyList<WaterfallStep> steps, ChartOptions? options = null)
    {
        var o = options ?? new ChartOptions();
        var n = steps.Count;
        if (n == 0) return Empty(o);
        var bars = new List<(double From, double To)>(n);
        var running = 0d;
        foreach (var step in steps)
        {
            if (step.IsTotal) bars.Add((0, running));
            else { bars.Add((running, running + step.Value)); running += step.Value; }
        }
        var ticks = ChartGeometry.NiceRange(bars.Min(b => Math.Min(b.From, b.To)), bars.Max(b => Math.Max(b.From, b.To)));
        double lo = ticks[0], hi = ticks[^1] > lo ? ticks[^1] : lo + 1;
        string At(double v) => F(Math.Clamp((v - lo) / (hi - lo) * 100, 0, 100));
        string Color(int i) => steps[i].IsTotal ? Neutral : steps[i].Value >= 0 ? Positive : Negative;

        var sb = new StringBuilder();
        sb.Append($"""<div class="m-chart m-xychart m-waterfall" role="figure" aria-label="{E(o.Title)}">""");
        sb.Append($"""<ul class="m-legend"><li><span class="m-key m-key-rect" style="--c:{Positive}"></span>Increase</li><li><span class="m-key m-key-rect" style="--c:{Negative}"></span>Decrease</li>""");
        if (steps.Any(s => s.IsTotal)) sb.Append($"""<li><span class="m-key m-key-rect" style="--c:{Neutral}"></span>Total</li>""");
        sb.Append("</ul>");
        sb.Append($"""<div class="m-xy-plot" style="height:{o.Height}px" aria-hidden="true">""");
        foreach (var tick in ticks)
            sb.Append($"""<div class="m-xy-grid{(tick == 0 ? " m-xy-base" : "")}" style="bottom:{At(tick)}%"><span class="m-xy-y">{E(AxisLabel(tick, o))}</span></div>""");
        sb.Append("""<div class="m-xy-slots">""");
        for (var i = 0; i < n; i++)
        {
            var (from, to) = bars[i];
            var change = steps[i].IsTotal ? to : steps[i].Value;
            sb.Append($"""<div class="m-xy-slot m-col-slot m-wf-slot{(i >= n / 2 && n > 3 ? " m-flip" : "")}">""");
            if (i < n - 1) sb.Append($"""<span class="m-wf-link" style="bottom:{At(to)}%"></span>""");
            sb.Append($"""<span class="m-wf-bar" style="bottom:{At(Math.Min(from, to))}%;height:{F(Math.Max(Math.Abs(to - from) / (hi - lo) * 100, 0.4))}%;--c:{Color(i)}"></span>""");
            sb.Append($"""<span class="m-wf-value{(steps[i].IsTotal ? " m-wf-total" : "")}" style="bottom:{At(Math.Max(from, to))}%">{E(steps[i].IsTotal ? Compact(to, o) : SignedCompact(change, o))}</span>""");
            sb.Append($"""<div class="m-xy-tip"><div class="m-xy-tip-title">{E(steps[i].Label)}</div>""");
            if (!steps[i].IsTotal)
                sb.Append($"""<div class="m-xy-tip-row"><span class="m-key m-key-line" style="--c:{Color(i)}"></span><strong>{E(Signed(change, o))}</strong><span>Change</span></div>""");
            sb.Append($"""<div class="m-xy-tip-row"><span class="m-key m-key-line" style="--c:{Neutral}"></span><strong>{E(Format(to, o))}</strong><span>{(steps[i].IsTotal ? "Total" : "Running total")}</span></div></div>""");
            sb.Append("</div>");
        }
        sb.Append("</div></div>");
        XAxis(sb, steps.Select(s => s.Label).ToList(), o);
        SimpleTable(sb, o.Title, ["Change", "Running total"],
            steps.Select((s, i) => (s.Label, new[] { s.IsTotal ? "" : Signed(s.Value, o), Format(bars[i].To, o) })));
        sb.Append("</div>");
        return sb.ToString();
    }

    // Funnel ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// Ordered stages narrowing to an outcome (lead → quote → order → invoice → paid). Bars are centred and
    /// take the ordinal ramp, darkest first; each row shows its conversion from the stage before.
    /// </summary>
    public static string Funnel(IReadOnlyList<ChartSlice> stages, ChartOptions? options = null)
    {
        var o = options ?? new ChartOptions();
        if (stages.Count == 0) return Empty(o);
        var first = Math.Max(0, stages[0].Value);
        var max = stages.Max(s => Math.Max(0, s.Value));

        var sb = new StringBuilder();
        sb.Append($"""<div class="m-chart m-rc m-funnelchart" role="figure" aria-label="{E(o.Title)}">""");
        var rows = new List<(string, string[])>();
        for (var i = 0; i < stages.Count; i++)
        {
            var v = Math.Max(0, stages[i].Value);
            var prev = i == 0 ? v : Math.Max(0, stages[i - 1].Value);
            var step = i == 0 ? "" : Share(v, prev);
            var overall = Share(v, first);
            sb.Append($"""<div class="m-rc-row" aria-hidden="true"><span class="m-rc-label" title="{E(stages[i].Label)}">{E(stages[i].Label)}</span>""");
            sb.Append($"""<span class="m-rc-track"><span class="m-funnel-bar m-hov{(i >= stages.Count / 2 ? " m-flip-up" : "")}" style="width:{Pct(v, max)}%;--c:{Ordinal(stages.Count - 1 - i, stages.Count)}">""");
            sb.Append($"""<span class="m-tip"><span class="m-tip-label">{E(stages[i].Label)}</span><span class="m-tip-value">{E(Format(v, o))}</span>""");
            if (i > 0) sb.Append($"""<span class="m-tip-label">{E(step)} of {E(stages[i - 1].Label)} · {E(overall)} of {E(stages[0].Label)} · {E(Format(Math.Max(0, prev - v), o))} lost</span>""");
            sb.Append("</span></span></span>");
            sb.Append($"""<span class="m-rc-value"><strong>{E(Format(v, o))}</strong>{(i == 0 ? "" : $"""<span class="m-rc-sub">{E(step)} conv.</span>""")}</span></div>""");
            rows.Add((stages[i].Label, [Format(v, o), step, overall]));
        }
        SimpleTable(sb, o.Title, ["Value", "From previous stage", "From first stage"], rows);
        sb.Append("</div>");
        return sb.ToString();
    }

    // Gauge ----------------------------------------------------------------------------------------------------

    /// <summary>
    /// A single measure against its range (OTIF, capacity utilisation, budget consumed) as a half ring with an
    /// optional target tick. The number is the headline; the arc gives it context.
    /// </summary>
    public static string Gauge(double value, double max, double? target = null, ChartOptions? options = null, string? caption = null, int size = 200)
    {
        var o = options ?? new ChartOptions();
        max = max <= 0 ? 1 : max;
        var fraction = Math.Clamp(value / max, 0, 1);
        const string Arc = "M8,50 A42,42 0 0 1 92,50";
        var sb = new StringBuilder();
        var label = $"{o.Title}: {Format(value, o)} of {Format(max, o)}" + (target is { } t0 ? $", target {Format(t0, o)}" : "");
        sb.Append($"""<div class="m-chart m-gauge" role="img" aria-label="{E(label)}"><div class="m-gauge-dial" style="width:{size}px">""");
        sb.Append($"""<svg viewBox="0 0 100 56" aria-hidden="true"><path class="m-gauge-track" d="{Arc}" pathLength="100"/>""");
        if (fraction > 0)
            sb.Append($"""<path class="m-gauge-fill" d="{Arc}" pathLength="100" stroke-dasharray="{F(fraction * 100)} 200" style="stroke:{Slot(o, 0)}"/>""");
        if (target is { } t)
        {
            var angle = Math.PI * (1 - Math.Clamp(t / max, 0, 1));
            double Px(double r) => 50 + r * Math.Cos(angle);
            double Py(double r) => 50 - r * Math.Sin(angle);
            sb.Append($"""<line class="m-gauge-target" x1="{F(Px(34))}" y1="{F(Py(34))}" x2="{F(Px(50))}" y2="{F(Py(50))}"/>""");
        }
        sb.Append("</svg>");
        sb.Append($"""<div class="m-gauge-center" aria-hidden="true"><span class="m-gauge-value">{E(Format(value, o))}</span>{(caption is null ? "" : $"""<span class="m-gauge-caption">{E(caption)}</span>""")}</div></div>""");
        sb.Append($"""<div class="m-gauge-scale" aria-hidden="true" style="width:{size}px"><span>{E(Format(0, o))}</span>{(target is { } t2 ? $"""<span class="m-gauge-goal"><span class="m-key m-key-line"></span>Target {E(Format(t2, o))}</span>""" : "")}<span>{E(Format(max, o))}</span></div>""");
        sb.Append("</div>");
        return sb.ToString();
    }

    // Bullet ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// KPIs against targets, one row each (sales vs quota, spend vs budget, output vs plan): a thin actual bar
    /// over grey qualitative bands, a target tick, and the attainment in words.
    /// </summary>
    public static string Bullet(IReadOnlyList<BulletItem> items, ChartOptions? options = null)
    {
        var o = options ?? new ChartOptions();
        if (items.Count == 0) return Empty(o);
        var sb = new StringBuilder();
        sb.Append($"""<div class="m-chart m-rc m-bullet" role="figure" aria-label="{E(o.Title)}">""");
        foreach (var item in items)
        {
            var bands = item.Bands ?? [];
            var max = new[] { item.Actual, item.Target, bands.Count > 0 ? bands[^1] : 0 }.Max() * (bands.Count > 0 ? 1 : 1.15);
            max = max <= 0 ? 1 : max;
            var attainment = item.Target == 0 ? "" : Share(item.Actual, item.Target);
            sb.Append($"""<div class="m-rc-row" aria-hidden="true"><span class="m-rc-label"><span title="{E(item.Label)}">{E(item.Label)}</span>{(item.Note is null ? "" : $"""<span class="m-rc-sub">{E(item.Note)}</span>""")}</span>""");
            sb.Append($"""<span class="m-rc-track m-bullet-track m-hov">""");
            // Widest band first so narrower (poorer) bands paint on top, darker.
            for (var b = bands.Count - 1; b >= 0; b--)
                sb.Append($"""<span class="m-bullet-band m-bullet-band-{Math.Min(bands.Count - 1 - b, 2)}" style="width:{Pct(bands[b], max)}%"></span>""");
            sb.Append($"""<span class="m-bullet-bar" style="width:{Pct(Math.Max(0, item.Actual), max)}%;--c:{Slot(o, 0)}"></span>""");
            sb.Append($"""<span class="m-bullet-target" style="left:{Pct(item.Target, max)}%"></span>""");
            sb.Append($"""<span class="m-tip"><span class="m-tip-label">{E(item.Label)}</span><span class="m-tip-value">{E(Format(item.Actual, o))} of {E(Format(item.Target, o))} target</span></span></span>""");
            sb.Append($"""<span class="m-rc-value"><strong>{E(Format(item.Actual, o))}</strong>{(attainment == "" ? "" : $"""<span class="m-rc-sub">{E(attainment)} of target</span>""")}</span></div>""");
        }
        SimpleTable(sb, o.Title, ["Actual", "Target", "Attainment"],
            items.Select(i => (i.Label, new[] { Format(i.Actual, o), Format(i.Target, o), i.Target == 0 ? "" : Share(i.Actual, i.Target) })));
        sb.Append("</div>");
        return sb.ToString();
    }

    // Variance -------------------------------------------------------------------------------------------------

    /// <summary>
    /// Signed differences around a centre line (actual − budget by cost centre, forecast error by product).
    /// <paramref name="positiveIsFavorable"/> decides which side reads as favourable: true for revenue,
    /// false for cost, where overspend is positive.
    /// </summary>
    public static string Variance(IReadOnlyList<ChartSlice> items, ChartOptions? options = null, bool positiveIsFavorable = true)
    {
        var o = options ?? new ChartOptions();
        if (items.Count == 0) return Empty(o);
        var maxAbs = items.Max(i => Math.Abs(i.Value));
        maxAbs = maxAbs <= 0 ? 1 : maxAbs;
        var sb = new StringBuilder();
        sb.Append($"""<div class="m-chart m-variance" role="figure" aria-label="{E(o.Title)}">""");
        sb.Append($"""<ul class="m-legend"><li><span class="m-key m-key-rect" style="--c:{Positive}"></span>Favorable</li><li><span class="m-key m-key-rect" style="--c:{Negative}"></span>Unfavorable</li></ul>""");
        sb.Append("""<div class="m-rc">""");
        foreach (var item in items)
        {
            var favorable = item.Value >= 0 == positiveIsFavorable;
            var width = F(Math.Abs(item.Value) / maxAbs * 50);
            var side = item.Value >= 0 ? "left:50%" : "right:50%";
            sb.Append($"""<div class="m-rc-row" aria-hidden="true"><span class="m-rc-label" title="{E(item.Label)}">{E(item.Label)}</span>""");
            sb.Append($"""<span class="m-rc-track m-var-track"><span class="m-var-bar{(item.Value < 0 ? " m-var-neg" : "")}" style="{side};width:{width}%;--c:{(item.Value == 0 ? Neutral : favorable ? Positive : Negative)}"></span></span>""");
            sb.Append($"""<span class="m-rc-value">{E(Signed(item.Value, o))}</span></div>""");
        }
        sb.Append("</div>");
        SimpleTable(sb, o.Title, ["Variance", "Direction"],
            items.Select(i => (i.Label, new[] { Signed(i.Value, o), i.Value == 0 ? "On plan" : i.Value >= 0 == positiveIsFavorable ? "Favorable" : "Unfavorable" })));
        sb.Append("</div>");
        return sb.ToString();
    }

    // Scatter / bubble -----------------------------------------------------------------------------------------

    /// <summary>
    /// Two measures per item (margin vs revenue, lead time vs defect rate), optionally a third as bubble area,
    /// optionally split into up to three groups. <see cref="ScatterOptions.Quadrants"/> adds mean lines.
    /// </summary>
    public static string Scatter(IReadOnlyList<ScatterPoint> points, ChartOptions? options = null, ScatterOptions? scatter = null)
    {
        var o = options ?? new ChartOptions();
        var so = scatter ?? new ScatterOptions();
        if (points.Count == 0) return Empty(o);
        var yTicks = ChartGeometry.NiceRange(points.Min(p => p.Y), points.Max(p => p.Y));
        var xTicks = ChartGeometry.NiceRange(points.Min(p => p.X), points.Max(p => p.X));
        double y0 = yTicks[0], y1 = yTicks[^1] > y0 ? yTicks[^1] : y0 + 1;
        double x0 = xTicks[0], x1 = xTicks[^1] > x0 ? xTicks[^1] : x0 + 1;
        double Xp(double v) => Math.Clamp((v - x0) / (x1 - x0) * 100, 0, 100);
        double Yp(double v) => Math.Clamp((v - y0) / (y1 - y0) * 100, 0, 100);
        var maxSize = points.Max(p => Math.Max(0, p.Size));
        string Fx(double v) => so.XPrefix + v.ToString(so.XFormat, Culture) + so.XSuffix;

        var sb = new StringBuilder();
        sb.Append($"""<div class="m-chart m-xychart m-scatter" role="figure" aria-label="{E(o.Title)}">""");
        if (so.Groups.Count > 1)
        {
            sb.Append("""<ul class="m-legend">""");
            for (var g = 0; g < so.Groups.Count; g++)
                sb.Append($"""<li><span class="m-key m-key-dot" style="--c:{Slot(o, g)}"></span>{E(so.Groups[g])}</li>""");
            if (maxSize > 0 && so.SizeTitle is not null) sb.Append($"""<li class="m-legend-note">Bubble size: {E(so.SizeTitle)}</li>""");
            sb.Append("</ul>");
        }
        if (so.YTitle is not null) sb.Append($"""<div class="m-axis-title">{E(so.YTitle)}</div>""");
        sb.Append($"""<div class="m-xy-plot" style="height:{o.Height}px" aria-hidden="true">""");
        foreach (var tick in yTicks)
            sb.Append($"""<div class="m-xy-grid{(tick == 0 ? " m-xy-base" : "")}" style="bottom:{F(Yp(tick))}%"><span class="m-xy-y">{E(AxisLabel(tick, o))}</span></div>""");
        foreach (var tick in xTicks)
            sb.Append($"""<div class="m-sc-vgrid{(tick == 0 ? " m-xy-base" : "")}" style="left:{F(Xp(tick))}%"><span>{E(so.XPrefix + ChartGeometry.Compact(tick) + (so.XSuffix.StartsWith(' ') ? "" : so.XSuffix))}</span></div>""");
        if (so.Quadrants)
        {
            sb.Append($"""<div class="m-sc-mean m-sc-mean-x" style="left:{F(Xp(points.Average(p => p.X)))}%"><span>avg</span></div>""");
            sb.Append($"""<div class="m-sc-mean m-sc-mean-y" style="bottom:{F(Yp(points.Average(p => p.Y)))}%"><span>avg</span></div>""");
        }
        // Largest bubbles first, so smaller ones sit on top and stay hoverable.
        foreach (var p in points.OrderByDescending(p => p.Size))
        {
            var d = maxSize > 0 ? 8 + 32 * Math.Sqrt(Math.Max(0, p.Size) / maxSize) : 10;
            var x = Xp(p.X);
            var flip = x > 65 ? " m-flip" : x < 20 ? " m-flip-l" : "";
            sb.Append($"""<span class="m-sc-pt m-hov{flip}{(maxSize > 0 ? " m-sc-bubble" : "")}" style="left:{F(x)}%;bottom:{F(Yp(p.Y))}%;--d:{F(d)}px;--c:{Slot(o, p.Group)}">""");
            sb.Append($"""<span class="m-tip"><span class="m-tip-label">{E(p.Label)}{(so.Groups.Count > 1 && p.Group < so.Groups.Count ? " · " + E(so.Groups[p.Group]) : "")}</span>""");
            sb.Append($"""<span class="m-tip-value">{E(so.YTitle ?? "y")}: {E(Format(p.Y, o))}</span><span class="m-tip-value">{E(so.XTitle ?? "x")}: {E(Fx(p.X))}</span>""");
            if (maxSize > 0) sb.Append($"""<span class="m-tip-value">{E(so.SizeTitle ?? "Size")}: {E(p.Size.ToString("N0", Culture))}</span>""");
            sb.Append("</span></span>");
        }
        sb.Append("</div>");
        sb.Append("""<div class="m-sc-xspace" aria-hidden="true"></div>""");
        if (so.XTitle is not null) sb.Append($"""<div class="m-axis-title m-axis-title-x">{E(so.XTitle)}</div>""");
        var headers = new List<string> { so.XTitle ?? "x", so.YTitle ?? "y" };
        if (maxSize > 0) headers.Add(so.SizeTitle ?? "Size");
        if (so.Groups.Count > 1) headers.Add("Group");
        SimpleTable(sb, o.Title, headers, points.Select(p =>
        {
            var cells = new List<string> { Fx(p.X), Format(p.Y, o) };
            if (maxSize > 0) cells.Add(p.Size.ToString("N0", Culture));
            if (so.Groups.Count > 1) cells.Add(p.Group < so.Groups.Count ? so.Groups[p.Group] : "");
            return (p.Label, cells.ToArray());
        }));
        sb.Append("</div>");
        return sb.ToString();
    }

    // Treemap --------------------------------------------------------------------------------------------------

    /// <summary>
    /// Part-to-whole for many parts (inventory value by category, spend by vendor), where a donut would be
    /// unreadable. Up to eight tiles; the tail folds into "Other". Tiles are tinted so labels stay in text ink.
    /// </summary>
    public static string Treemap(IReadOnlyList<ChartSlice> items, ChartOptions? options = null, double aspect = 2.4)
    {
        var o = options ?? new ChartOptions();
        var parts = items.Where(i => i.Value > 0).OrderByDescending(i => i.Value).Select(i => (i.Label, i.Value, Other: false)).ToList();
        if (parts.Count == 0) return Empty(o);
        if (parts.Count > 8) parts = [.. parts.Take(7), ("Other", parts.Skip(7).Sum(p => p.Value), true)];
        var total = parts.Sum(p => p.Value);
        var width = 100 * aspect;
        var rects = ChartGeometry.Squarify(parts.Select(p => p.Value).ToList(), width, 100);
        string Color(int i) => parts[i].Other ? Neutral : Slot(o, i);

        var sb = new StringBuilder();
        sb.Append($"""<div class="m-chart m-treemap" role="figure" aria-label="{E(o.Title)}">""");
        sb.Append($"""<div class="m-tm-plot" style="height:{o.Height}px" aria-hidden="true">""");
        for (var i = 0; i < parts.Count; i++)
        {
            var (x, y, w, h) = rects[i];
            var wp = w / width * 100;
            // Text only where it fits (≥ ~40px tall at the given height; the name from ~12 % wide, the value from ~22 %);
            // small tiles rely on hover, the legend and the table rather than clipped text.
            var fits = h / 100 * o.Height >= 40 && wp >= 12;
            var size = !fits ? " m-tm-small" : wp < 22 ? " m-tm-novalue" : "";
            sb.Append($"""<div class="m-tm-tile m-hov{(x / width > 0.55 ? " m-flip" : "")}{size}" style="left:{F(x / width * 100)}%;top:{F(y)}%;width:{F(wp)}%;height:{F(h)}%;--c:{Color(i)}">""");
            sb.Append($"""<span class="m-tm-label">{E(parts[i].Label)}</span><span class="m-tm-value">{E(Format(parts[i].Value, o))} · {E(Share(parts[i].Value, total))}</span>""");
            sb.Append($"""<span class="m-tip"><span class="m-tip-label">{E(parts[i].Label)}</span><span class="m-tip-value">{E(Format(parts[i].Value, o))} ({E(Share(parts[i].Value, total))})</span></span></div>""");
        }
        sb.Append("</div>");
        sb.Append("""<ul class="m-legend m-legend-below">""");
        for (var i = 0; i < parts.Count; i++)
            sb.Append($"""<li><span class="m-key m-key-rect" style="--c:{Color(i)}"></span>{E(parts[i].Label)} <span class="m-legend-value">{E(Share(parts[i].Value, total))}</span></li>""");
        sb.Append("</ul>");
        SimpleTable(sb, o.Title, ["Value", "Share"], parts.Select(p => (p.Label, new[] { Format(p.Value, o), Share(p.Value, total) })));
        sb.Append("</div>");
        return sb.ToString();
    }

    // Pareto ---------------------------------------------------------------------------------------------------

    /// <summary>
    /// ABC analysis: items sorted largest first, columns show each item's share and the line the cumulative
    /// share, both in percent on one axis, with the A and B cut-offs marked. Tooltips add the value and class.
    /// </summary>
    public static string Pareto(IReadOnlyList<ChartSlice> items, ChartOptions? options = null, double aCut = 80, double bCut = 95, string valueName = "Share")
    {
        var o = options ?? new ChartOptions();
        var sorted = items.Where(i => i.Value > 0).OrderByDescending(i => i.Value).ToList();
        if (sorted.Count == 0) return Empty(o);
        var total = sorted.Sum(i => i.Value);
        var shares = sorted.Select(i => i.Value / total * 100).ToList();
        var cumulative = new List<double>();
        var classes = new List<char>();
        var run = 0d;
        foreach (var s in shares)
        {
            classes.Add(run < aCut ? 'A' : run < bCut ? 'B' : 'C');
            run += s;
            cumulative.Add(Math.Min(run, 100));
        }
        var po = o with { Prefix = "", Suffix = "%", ValueFormat = "N1" };
        return ColumnsCore(
            sorted.Select(i => i.Label).ToList(),
            [new ChartSeries(valueName, shares)],
            [new ChartSeries("Cumulative", cumulative)],
            po, "m-colchart m-pareto",
            i => $"""<div class="m-xy-tip-row m-xy-tip-total"><strong>{E(Format(sorted[i].Value, o))}</strong><span>Class {classes[i]}</span></div>""",
            [(aCut, $"A · {aCut.ToString("0", Culture)}%"), (bCut, $"B · {bCut.ToString("0", Culture)}%")],
            fixedTop: 100);
    }

    // Parts ----------------------------------------------------------------------------------------------------

    /// <summary>Screen-reader table with a row header per item and the given value columns.</summary>
    private static void SimpleTable(StringBuilder sb, string? title, IReadOnlyList<string> headers, IEnumerable<(string Label, string[] Cells)> rows)
    {
        sb.Append($"""<table class="m-sr-only"><caption>{E(title)}</caption><thead><tr><th scope="col"></th>""");
        foreach (var h in headers) sb.Append($"<th scope=\"col\">{E(h)}</th>");
        sb.Append("</tr></thead><tbody>");
        foreach (var (label, cells) in rows)
        {
            sb.Append($"<tr><th scope=\"row\">{E(label)}</th>");
            foreach (var c in cells) sb.Append($"<td>{E(c)}</td>");
            sb.Append("</tr>");
        }
        sb.Append("</tbody></table>");
    }

    /// <summary>Step <paramref name="step"/> (0 = lightest) of <paramref name="count"/> on the ordinal blue ramp.</summary>
    private static string Ordinal(int step, int count) =>
        count <= 1 ? "var(--m-ord-hi)" : $"color-mix(in oklab, var(--m-ord-hi) {F(step * 100.0 / (count - 1))}%, var(--m-ord-lo))";

    private static string AxisLabel(double tick, ChartOptions o) =>
        (tick < 0 ? "-" : "") + o.Prefix + ChartGeometry.Compact(Math.Abs(tick)) + (o.Suffix.StartsWith(' ') ? "" : o.Suffix);

    private static string Compact(double value, ChartOptions o) =>
        (value < 0 ? "−" : "") + o.Prefix + ChartGeometry.Compact(Math.Abs(value)) + (o.Suffix.StartsWith(' ') ? "" : o.Suffix);

    private static string SignedCompact(double value, ChartOptions o) => (value > 0 ? "+" : "") + Compact(value, o);

    /// <summary>"+$1,200" / "−$800": the sign leads the prefix and uses a true minus.</summary>
    private static string Signed(double value, ChartOptions o) =>
        (value > 0 ? "+" : value < 0 ? "−" : "") + o.Prefix + Math.Abs(value).ToString(o.ValueFormat, Culture) + o.Suffix;
}
