using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Charts;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// KPI card (Ant Design Pro ChartCard). Children: <c>&lt;stat-chart&gt;</c> and <c>&lt;stat-footer&gt;</c>.
/// </summary>
[HtmlTargetElement("app-statistic-card")]
public class StatisticCardTagHelper : TagHelper
{
    public string? Title { get; set; }
    public string? Tooltip { get; set; }
    public string? Value { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new Dictionary<string, string>();
        context.Items[typeof(StatisticCardTagHelper)] = slots;
        await output.GetChildContentAsync();

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-card", "m-stat-card");

        var tip = Tooltip is null ? "" : $"""<span title="{Html.E(Tooltip)}">{AntIcons.Span("info-circle")}</span>""";
        var sb = new StringBuilder("""<div class="ant-card-body">""")
            .Append($"""<div class="m-stat-card-head"><span>{Html.E(Title)}</span>{tip}</div>""")
            .Append($"""<div class="m-stat-card-value">{Html.E(Value)}</div>""");
        if (slots.TryGetValue("stat-chart", out var chart)) sb.Append($"""<div class="m-stat-card-chart">{chart}</div>""");
        if (slots.TryGetValue("stat-footer", out var footer)) sb.Append($"""<div class="m-stat-card-footer">{footer}</div>""");
        output.Content.SetHtmlContent(sb.Append("</div>").ToString());
    }
}

[HtmlTargetElement("stat-chart", ParentTag = "app-statistic-card")]
[HtmlTargetElement("stat-footer", ParentTag = "app-statistic-card")]
public class StatisticCardSlotTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(StatisticCardTagHelper), out var slots))
            ((Dictionary<string, string>)slots)[context.TagName] = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}

/// <summary>Area sparkline — identical markup to Blazor's &lt;Sparkline&gt;.</summary>
[HtmlTargetElement("app-sparkline", TagStructure = TagStructure.WithoutEndTag)]
public class SparklineTagHelper : TagHelper
{
    public IReadOnlyList<ChartPoint> Data { get; set; } = [];
    public string Format { get; set; } = "N0";
    public string? AriaLabel { get; set; }

    /// <summary>Fill under the line. False draws a plain line.</summary>
    public bool Area { get; set; } = true;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var (line, area) = ChartGeometry.Sparkline(Data.Select(p => p.Value).ToList(), 100, 46);
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-chart", "m-sparkline");
        output.Attributes.SetAttribute("role", "img");
        if (AriaLabel is not null) output.Attributes.SetAttribute("aria-label", AriaLabel);
        var sb = new StringBuilder($"""<svg viewBox="0 0 100 46" preserveAspectRatio="none" aria-hidden="true">{(Area ? $"<path class=\"m-spark-area\" d=\"{area}\"></path>" : "")}<path class="m-spark-line" d="{line}"></path></svg><div class="m-hits" aria-hidden="true">""");
        foreach (var p in Data)
            sb.Append($"""<span class="m-hit"><span class="m-tip"><span class="m-tip-label">{Html.E(p.Label)}</span><span class="m-tip-value">{p.Value.ToString(Format)}</span></span></span>""");
        output.Content.SetHtmlContent(sb.Append("</div>").ToString());
    }
}

/// <summary>Compact column chart — identical markup to Blazor's &lt;MiniBar&gt;.</summary>
[HtmlTargetElement("app-mini-bar", TagStructure = TagStructure.WithoutEndTag)]
public class MiniBarTagHelper : TagHelper
{
    public IReadOnlyList<ChartPoint> Data { get; set; } = [];
    public string Format { get; set; } = "N0";
    public string? AriaLabel { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var max = Data.Count == 0 ? 0 : Data.Max(p => p.Value);
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-chart", "m-sparkline");
        output.Attributes.SetAttribute("role", "img");
        if (AriaLabel is not null) output.Attributes.SetAttribute("aria-label", AriaLabel);
        var sb = new StringBuilder("""<div class="m-bars m-bars-mini" aria-hidden="true">""");
        foreach (var p in Data)
            sb.Append($"""<div class="m-bar-slot"><span class="m-bar" style="height:{ChartGeometry.Percent(p.Value, max)}%"><span class="m-tip"><span class="m-tip-label">{Html.E(p.Label)}</span><span class="m-tip-value">{p.Value.ToString(Format)}</span></span></span></div>""");
        output.Content.SetHtmlContent(sb.Append("</div>").ToString());
    }
}

/// <summary>Single-series column chart — identical markup to Blazor's &lt;BarChart&gt;.</summary>
[HtmlTargetElement("app-bar-chart", TagStructure = TagStructure.WithoutEndTag)]
public class BarChartTagHelper : TagHelper
{
    public IReadOnlyList<ChartPoint> Data { get; set; } = [];
    public string? Title { get; set; }
    public int Height { get; set; } = 280;
    public string Format { get; set; } = "N0";
    public int MaxXLabels { get; set; } = 12;

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var max = Data.Count == 0 ? 0 : Data.Max(p => p.Value);
        var ticks = ChartGeometry.NiceTicks(max);
        var top = ticks[^1] > 0 ? ticks[^1] : 1;
        var every = Math.Max(1, (int)Math.Ceiling(Data.Count / (double)Math.Max(1, MaxXLabels)));

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-chart", "m-barchart");
        output.AddStyle($"height:{Height}px");

        var sb = new StringBuilder("""<div class="m-barchart-plot" aria-hidden="true">""");
        foreach (var t in ticks)
            sb.Append($"""<div class="{(t == 0 ? "m-gridline m-baseline" : "m-gridline")}" style="bottom:{ChartGeometry.Percent(t, top)}%"><span class="m-axis-y">{ChartGeometry.Compact(t)}</span></div>""");
        sb.Append("""<div class="m-bars">""");
        foreach (var p in Data)
            sb.Append($"""<div class="m-bar-slot"><span class="m-bar" style="height:{ChartGeometry.Percent(p.Value, top)}%"><span class="m-tip"><span class="m-tip-label">{Html.E(p.Label)}</span><span class="m-tip-value">{p.Value.ToString(Format)}</span></span></span></div>""");
        sb.Append("""</div></div><div class="m-barchart-x" aria-hidden="true">""");
        for (var i = 0; i < Data.Count; i++)
            sb.Append(i % every == 0 ? $"<span>{Html.E(Data[i].Label)}</span>" : $"""<span class="m-skip">{Html.E(Data[i].Label)}</span>""");
        sb.Append($"""</div><table class="m-sr-only"><caption>{Html.E(Title)}</caption><tbody>""");
        foreach (var p in Data)
            sb.Append($"""<tr><th scope="row">{Html.E(p.Label)}</th><td>{p.Value.ToString(Format)}</td></tr>""");
        output.Content.SetHtmlContent(sb.Append("</tbody></table>").ToString());
    }
}

/// <summary>
/// Page header + content, same DOM as ProLayout's PageContainer.
/// Children: <c>&lt;page-extra&gt;</c> (header actions) and <c>&lt;page-content&gt;</c> (description).
/// </summary>
[HtmlTargetElement("app-page-container")]
public class PageContainerTagHelper : TagHelper
{
    public string? Title { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new Dictionary<string, string>();
        context.Items[typeof(PageContainerTagHelper)] = slots;
        var body = (await output.GetChildContentAsync()).GetContent();

        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-pro-page-container");
        var extra = slots.TryGetValue("page-extra", out var e) ? $"""<div class="ant-page-header-heading-extra">{e}</div>""" : "";
        var content = slots.TryGetValue("page-content", out var c)
            ? $"""<div class="ant-page-header-content"><div class="ant-pro-page-container-detail"><div class="ant-pro-page-container-main">{c}</div></div></div>"""
            : "";
        output.Content.SetHtmlContent(
            $"""<div class="ant-pro-page-container-warp"><div class="ant-page-header"><div class="ant-page-header-heading"><div class="ant-page-header-heading-left"><h1 class="ant-page-header-heading-title" style="margin:0">{Html.E(Title)}</h1></div>{extra}</div>{content}</div></div>""" +
            $"""<div class="ant-pro-grid-content"><div class="ant-pro-grid-content-children"><div class="ant-pro-page-container-children-content">{body}</div></div></div>""");
    }
}

[HtmlTargetElement("page-extra", ParentTag = "app-page-container")]
[HtmlTargetElement("page-content", ParentTag = "app-page-container")]
public class PageContainerSlotTagHelper : TagHelper
{
    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        if (context.Items.TryGetValue(typeof(PageContainerTagHelper), out var slots))
            ((Dictionary<string, string>)slots)[context.TagName] = (await output.GetChildContentAsync()).GetContent();
        output.SuppressOutput();
    }
}
