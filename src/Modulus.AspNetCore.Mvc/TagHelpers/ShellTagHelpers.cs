using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions.Navigation;
using Modulus.Ui.Abstractions.Charts;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

// Named icons, the page header and the multi-series charts. Markup comes from the same renderers the Blazor
// components use (ShellRenderer, ChartHtml), so both flavors output identical HTML.

/// <summary>
/// Named icon: <c>&lt;app-ti name="team" /&gt;</c>. Draws an Ant Design outline icon inline, the way the shell draws its
/// icons (<see cref="ShellRenderer.Icon"/>; <c>tb:</c> names are the Tabler fallbacks). For an Ant Design icon with
/// a theme, spin or rotation, use <c>&lt;app-icon&gt;</c>.
/// </summary>
[HtmlTargetElement("app-ti", TagStructure = TagStructure.WithoutEndTag)]
public class NamedIconTagHelper : TagHelper
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
/// <summary>The breadcrumb of the page header that contains it, handed over while the header's content runs.</summary>
internal sealed class PageHeaderBreadcrumb
{
    public string Html { get; set; } = "";
}

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
        // The app-crumb children (the page's breadcrumb trail) register here while the content runs.
        var crumbs = new ItemList<Crumb>();
        context.Items[typeof(PageHeaderTagHelper)] = crumbs;
        var breadcrumb = new PageHeaderBreadcrumb();
        context.Items[typeof(PageHeaderBreadcrumb)] = breadcrumb;
        var actions = (await output.GetChildContentAsync()).GetContent();
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-page-header");
        var trail = crumbs.Count == 0 ? "" : $"""<div class="m-page-crumbs">{CrumbHtml.Trail(crumbs)}</div>""";
        // Odoo's control panel: with no title, the breadcrumb is the title (the page's path, its last level being the page).
        // With a title, the breadcrumb sits above it.
        var trailIsTitle = breadcrumb.Html.Length > 0 && Title.Length == 0;
        var heading = trailIsTitle
            ? $"""<div class="m-page-title m-page-trail">{breadcrumb.Html}</div>"""
            : $"""<h1 class="m-page-title">{Html.E(Title)} {TitleExtraHtml}</h1>""";
        var above = trailIsTitle ? "" : breadcrumb.Html;
        output.Content.SetHtmlContent(
            """<div class="m-page-header-text">""" +
            above + trail +
            (Pretitle is null ? "" : $"""<div class="m-pretitle">{Html.E(Pretitle)}</div>""") +
            heading +
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
        Height = Height,
        ValueFormat = ValueFormat,
        Prefix = Prefix,
        Suffix = Suffix,
        Title = Title,
        FirstSlot = FirstSlot,
        MaxXLabels = MaxXLabels,
        Area = area,
        Stacked = stacked,
        CenterLabel = centerLabel,
        LegendToggle = LegendToggle,
        Links = Links,
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
