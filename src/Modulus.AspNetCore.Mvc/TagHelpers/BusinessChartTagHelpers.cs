using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Charts;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

// Business / ERP charts. Markup comes from ChartHtml, the same renderer the Blazor components use,
// so both flavors output identical HTML.

/// <summary>Horizontal bars, largest first: <c>&lt;app-ranked-bar-chart items="Model.Customers" limit="8" show-share /&gt;</c>.</summary>
[HtmlTargetElement("app-ranked-bar-chart", TagStructure = TagStructure.WithoutEndTag)]
public class RankedBarChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<ChartSlice> Items { get; set; } = [];
    public int Limit { get; set; }
    public bool ShowShare { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.RankedBars(Items, Options(), Limit, ShowShare));
}

/// <summary>Horizontal stacked bars: <c>&lt;app-stacked-bar-chart rows="…" series="…" normalize ordinal /&gt;</c>.</summary>
[HtmlTargetElement("app-stacked-bar-chart", TagStructure = TagStructure.WithoutEndTag)]
public class StackedBarChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<string> Rows { get; set; } = [];
    public IReadOnlyList<ChartSeries> Series { get; set; } = [];
    public bool Normalize { get; set; }
    public bool Ordinal { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.StackedBars(Rows, Series, Options(), Normalize, Ordinal));
}

/// <summary>Columns plus lines on one axis: <c>&lt;app-combo-chart labels="…" columns="…" lines="…" /&gt;</c>.</summary>
[HtmlTargetElement("app-combo-chart", TagStructure = TagStructure.WithoutEndTag)]
public class ComboChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<string> Labels { get; set; } = [];
    public IReadOnlyList<ChartSeries> Columns { get; set; } = [];
    public IReadOnlyList<ChartSeries> Lines { get; set; } = [];
    public bool Stacked { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Combo(Labels, Columns, Lines, Options(stacked: Stacked)));
}

/// <summary>Bridge through signed changes: <c>&lt;app-waterfall-chart steps="Model.Bridge" prefix="$" /&gt;</c>.</summary>
[HtmlTargetElement("app-waterfall-chart", TagStructure = TagStructure.WithoutEndTag)]
public class WaterfallChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<WaterfallStep> Steps { get; set; } = [];

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Waterfall(Steps, Options()));
}

/// <summary>Ordered stages with conversion: <c>&lt;app-funnel-chart stages="Model.Pipeline" /&gt;</c>.</summary>
[HtmlTargetElement("app-funnel-chart", TagStructure = TagStructure.WithoutEndTag)]
public class FunnelChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<ChartSlice> Stages { get; set; } = [];

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Funnel(Stages, Options()));
}

/// <summary>Half-ring gauge: <c>&lt;app-gauge value="92.4" max="100" target="95" suffix="%" caption="On-time delivery" /&gt;</c>.</summary>
[HtmlTargetElement("app-gauge", TagStructure = TagStructure.WithoutEndTag)]
public class GaugeTagHelper : ChartTagHelperBase
{
    public double Value { get; set; }
    public double Max { get; set; } = 100;
    public double? Target { get; set; }
    public string? Caption { get; set; }
    public int Size { get; set; } = 200;

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Gauge(Value, Max, Target, Options(), Caption, Size));
}

/// <summary>KPIs against targets: <c>&lt;app-bullet-chart items="Model.Kpis" /&gt;</c>.</summary>
[HtmlTargetElement("app-bullet-chart", TagStructure = TagStructure.WithoutEndTag)]
public class BulletChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<BulletItem> Items { get; set; } = [];

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Bullet(Items, Options()));
}

/// <summary>Signed variances: <c>&lt;app-variance-chart items="Model.Variance" positive-is-favorable="false" /&gt;</c>.</summary>
[HtmlTargetElement("app-variance-chart", TagStructure = TagStructure.WithoutEndTag)]
public class VarianceChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<ChartSlice> Items { get; set; } = [];
    public bool PositiveIsFavorable { get; set; } = true;

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Variance(Items, Options(), PositiveIsFavorable));
}

/// <summary>Scatter or bubble chart: <c>&lt;app-scatter-chart points="…" x-title="Revenue" y-title="Margin" size-title="Units" quadrants /&gt;</c>.</summary>
[HtmlTargetElement("app-scatter-chart", TagStructure = TagStructure.WithoutEndTag)]
public class ScatterChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<ScatterPoint> Points { get; set; } = [];
    public IReadOnlyList<string> Groups { get; set; } = [];
    public string? XTitle { get; set; }
    public string? YTitle { get; set; }
    public string? SizeTitle { get; set; }
    public string XFormat { get; set; } = "N0";
    public string XPrefix { get; set; } = "";
    public string XSuffix { get; set; } = "";
    public bool Quadrants { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Scatter(Points, Options(), new ScatterOptions
        {
            XTitle = XTitle,
            YTitle = YTitle,
            SizeTitle = SizeTitle,
            XFormat = XFormat,
            XPrefix = XPrefix,
            XSuffix = XSuffix,
            Groups = Groups,
            Quadrants = Quadrants,
        }));
}

/// <summary>Treemap: <c>&lt;app-treemap items="Model.InventoryByCategory" prefix="$" /&gt;</c>.</summary>
[HtmlTargetElement("app-treemap", TagStructure = TagStructure.WithoutEndTag)]
public class TreemapTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<ChartSlice> Items { get; set; } = [];
    public double Aspect { get; set; } = 2.4;

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Treemap(Items, Options(), Aspect));
}

/// <summary>ABC / Pareto analysis: <c>&lt;app-pareto-chart items="Model.SkuRevenue" prefix="$" /&gt;</c>.</summary>
[HtmlTargetElement("app-pareto-chart", TagStructure = TagStructure.WithoutEndTag)]
public class ParetoChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<ChartSlice> Items { get; set; } = [];
    public double ACut { get; set; } = 80;
    public double BCut { get; set; } = 95;
    public string ValueName { get; set; } = "Share";

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Pareto(Items, Options(), ACut, BCut, ValueName));
}

/// <summary>Several measures on shared spokes: <c>&lt;app-radar-chart axes="Model.Axes" series="Model.Scores" max="5" /&gt;</c>.</summary>
[HtmlTargetElement("app-radar-chart", TagStructure = TagStructure.WithoutEndTag)]
public class RadarChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<string> Axes { get; set; } = [];
    public IReadOnlyList<ChartSeries> Series { get; set; } = [];
    public double? Max { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Radar(Axes, Series, Options(), Max));
}

/// <summary>Open / high / low / close per period: <c>&lt;app-candlestick-chart candles="Model.Prices" prefix="$" /&gt;</c>.</summary>
[HtmlTargetElement("app-candlestick-chart", TagStructure = TagStructure.WithoutEndTag)]
public class CandlestickChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<Candle> Candles { get; set; } = [];

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Candlestick(Candles, Options()));
}

/// <summary>Flows between nodes: <c>&lt;app-sankey-chart flows="Model.Flows" height="320" /&gt;</c>.</summary>
[HtmlTargetElement("app-sankey-chart", TagStructure = TagStructure.WithoutEndTag)]
public class SankeyChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<SankeyLink> Flows { get; set; } = [];

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Sankey(Flows, Options()));
}

/// <summary>A reporting hierarchy: <c>&lt;app-org-chart nodes="Model.People" /&gt;</c>.</summary>
[HtmlTargetElement("app-org-chart", TagStructure = TagStructure.WithoutEndTag)]
public class OrgChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<OrgNode> Nodes { get; set; } = [];

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.OrgChart(Nodes, Options()));
}
