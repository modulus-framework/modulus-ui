using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Charts;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

// Tabler page pieces and the multi-series charts. Markup comes from the same renderers the Blazor
// components use (ShellRenderer, ChartHtml), so both flavors output identical HTML.

/// <summary>Tabler outline icon: <c>&lt;app-ti name="users" /&gt;</c>.</summary>
[HtmlTargetElement("app-ti", TagStructure = TagStructure.WithoutEndTag)]
public class TablerIconTagHelper : TagHelper
{
    public string Name { get; set; } = "";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var cls = output.Attributes.TryGetAttribute("class", out var c) ? c.Value?.ToString() : null;
        output.TagName = null;
        output.Content.SetHtmlContent(ShellRenderer.Icon(Name, cls));
    }
}

/// <summary>
/// Tabler page header; child content renders as the actions on the right:
/// <code>&lt;app-page-header pretitle="Overview" title="Dashboard"&gt;&lt;app-button type="primary"&gt;New&lt;/app-button&gt;&lt;/app-page-header&gt;</code>
/// </summary>
[HtmlTargetElement("app-page-header")]
public class PageHeaderTagHelper : TagHelper
{
    public string? Pretitle { get; set; }
    public string Title { get; set; } = "";
    public string? Subtitle { get; set; }

    /// <summary>Raw HTML after the title text, e.g. a status badge.</summary>
    public string? TitleExtraHtml { get; set; }

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var actions = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-page-header");
        output.Content.SetHtmlContent(
            """<div class="m-page-header-text">""" +
            (Pretitle is null ? "" : $"""<div class="m-pretitle">{Html.E(Pretitle)}</div>""") +
            $"""<h1 class="m-page-title">{Html.E(Title)} {TitleExtraHtml}</h1>""" +
            (Subtitle is null ? "" : $"""<p class="m-page-subtitle">{Html.E(Subtitle)}</p>""") +
            "</div>" +
            (string.IsNullOrWhiteSpace(actions) ? "" : $"""<div class="m-page-actions">{actions}</div>"""));
    }
}

/// <summary>Attributes shared by the chart tag helpers.</summary>
public abstract class ChartTagHelperBase : TagHelper
{
    public int Height { get; set; } = 260;
    public string ValueFormat { get; set; } = "N0";
    public string Prefix { get; set; } = "";
    public string Suffix { get; set; } = "";
    public string? Title { get; set; }
    public int FirstSlot { get; set; } = 1;
    public int MaxXLabels { get; set; } = 12;

    /// <summary>Legend entries hide and show their series (line, column and combo charts).</summary>
    public bool LegendToggle { get; set; }

    /// <summary>Drill-down: one link per x-axis label, null for none (line, column and combo charts).</summary>
    public IReadOnlyList<string?>? Links { get; set; }

    protected ChartOptions Options(bool area = false, bool stacked = false, string? centerLabel = null) => new()
    {
        Height = Height, ValueFormat = ValueFormat, Prefix = Prefix, Suffix = Suffix, Title = Title,
        FirstSlot = FirstSlot, MaxXLabels = MaxXLabels, Area = area, Stacked = stacked, CenterLabel = centerLabel,
        LegendToggle = LegendToggle, Links = Links,
    };

    protected static void Emit(TagHelperOutput output, string html)
    {
        output.TagName = null;
        output.Content.SetHtmlContent(html);
    }
}

/// <summary>Multi-series line / area chart: <c>&lt;app-line-chart labels="Model.Months" series="Model.Series" area /&gt;</c>.</summary>
[HtmlTargetElement("app-line-chart", TagStructure = TagStructure.WithoutEndTag)]
public class LineChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<string> Labels { get; set; } = [];
    public IReadOnlyList<ChartSeries> Series { get; set; } = [];
    public bool Area { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Line(Labels, Series, Options(area: Area)));
}

/// <summary>Grouped or stacked columns: <c>&lt;app-column-chart labels="…" series="…" stacked /&gt;</c>.</summary>
[HtmlTargetElement("app-column-chart", TagStructure = TagStructure.WithoutEndTag)]
public class ColumnChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<string> Labels { get; set; } = [];
    public IReadOnlyList<ChartSeries> Series { get; set; } = [];
    public bool Stacked { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Columns(Labels, Series, Options(stacked: Stacked)));
}

/// <summary>Donut with a value legend: <c>&lt;app-donut-chart slices="Model.Channels" center-label="Visits" /&gt;</c>.</summary>
[HtmlTargetElement("app-donut-chart", TagStructure = TagStructure.WithoutEndTag)]
public class DonutChartTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<ChartSlice> Slices { get; set; } = [];
    public string? CenterLabel { get; set; }
    public int Size { get; set; } = 180;

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Donut(Slices, Options(centerLabel: CenterLabel), Size));
}

/// <summary>Heatmap: <c>&lt;app-heatmap rows="…" columns="…" values="…" /&gt;</c>.</summary>
[HtmlTargetElement("app-heatmap", TagStructure = TagStructure.WithoutEndTag)]
public class HeatmapTagHelper : ChartTagHelperBase
{
    public IReadOnlyList<string> Rows { get; set; } = [];
    public IReadOnlyList<string> Columns { get; set; } = [];
    public IReadOnlyList<IReadOnlyList<double>> Values { get; set; } = [];

    public override void Process(TagHelperContext context, TagHelperOutput output) =>
        Emit(output, ChartHtml.Heatmap(Rows, Columns, Values, Options()));
}
