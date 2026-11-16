using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Wraps a server-rendered <c>&lt;table&gt;</c> in Ant Design's table chrome and styles its cells:
/// <code>&lt;app-table size="small"&gt;&lt;thead&gt;…&lt;/thead&gt;&lt;tbody&gt;…&lt;/tbody&gt;&lt;/app-table&gt;</code>
/// <para>
/// Column picker: mark optional <c>&lt;col&gt;</c>, <c>&lt;th&gt;</c> and <c>&lt;td&gt;</c> elements with
/// <c>data-col="key"</c> and pass <c>hidden-columns</c>; hidden ones are left out (see <see cref="TableColumnsTagHelper"/>).
/// <c>density</c> binds the size to the toolbar's density menu (<see cref="TableSettingsTagHelper"/>).
/// A <c>&lt;table-footer&gt;</c> child (e.g. the pager) renders under the table.
/// </para>
/// </summary>
[HtmlTargetElement("app-table")]
public partial class TableTagHelper : TagHelper
{
    /// <summary>default | middle | small</summary>
    public string Size { get; set; } = "default";

    public bool Bordered { get; set; }

    /// <summary>Shown when the body has no rows.</summary>
    public string EmptyText { get; set; } = "No data";

    /// <summary>Accessible table caption (visually hidden).</summary>
    public string? Caption { get; set; }

    /// <summary>Keys of <c>data-col</c> columns to leave out.</summary>
    public IEnumerable<string>? HiddenColumns { get; set; }

    /// <summary>Bind the size to the density menu of the enclosing <c>tableSelection</c> scope.</summary>
    public bool Density { get; set; }

    /// <summary>Table width in pixels; wider tables scroll horizontally inside the card.</summary>
    public int? ScrollX { get; set; }

    /// <summary>auto | fixed (fixed honours &lt;col&gt; widths).</summary>
    public string Layout { get; set; } = "auto";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new Dictionary<string, string>();
        context.Items[typeof(TableTagHelper)] = slots;
        var html = (await output.GetChildContentAsync()).GetContent();
        slots.TryGetValue("table-footer", out var footer);
        foreach (var key in HiddenColumns ?? [])
            html = HiddenColumn(key).Replace(html, "");
        html = Unclassed().Replace(html, m => m.Groups[1].Value switch
        {
            "thead" => """<thead class="ant-table-thead" """,
            "tbody" => """<tbody class="ant-table-tbody" """,
            "tfoot" => """<tfoot class="ant-table-summary" """,
            "th" or "td" => $"""<{m.Groups[1].Value} class="ant-table-cell" """,
            _ => m.Value,
        });
        html = BodyRows().Replace(html, m => BodyRow().Replace(m.Value, """<tr class="ant-table-row" """));
        if (EmptyBody().IsMatch(html))
        {
            var cols = Math.Max(1, HeaderCells().Matches(html).Count);
            html = EmptyBody().Replace(html, $"""<tbody class="ant-table-tbody"><tr class="ant-table-placeholder"><td class="ant-table-cell" colspan="{cols}">{EmptyTagHelper.Render(EmptyText, "ant-empty-small")}</td></tr></tbody>""");
        }
        var caption = Caption is null ? "" : $"""<caption class="m-sr-only">{Html.E(Caption)}</caption>""";
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-table-wrapper");
        var table = Html.Cx("ant-table", Size == "default" || Density ? null : $"ant-table-{Size}", Bordered ? "ant-table-bordered" : null, ScrollX is null ? null : "ant-table-scroll-horizontal");
        var content = ScrollX is null ? "" : """ style="overflow: auto hidden;" """.TrimEnd();
        var tableStyle = (ScrollX is { } x ? $"width: {x}px; min-width: 100%; " : "") + $"table-layout: {(Layout == "fixed" ? "fixed" : "auto")};";
        output.Content.SetHtmlContent(
            $"""<div class="ant-spin-nested-loading"><div class="ant-spin-container"><div class="{table}"{(Density ? " :class=\"density\"" : "")}><div class="ant-table-container"><div class="ant-table-content"{content}><table style="{tableStyle}">{caption}{html}</table></div></div></div>{footer}</div></div>""");
    }

    private static Regex HiddenColumn(string key)
    {
        var k = Regex.Escape(Html.E(key));
        return new Regex($"""<col(?=[\s/>])[^>]*\sdata-col="{k}"[^>]*>|<(th|td)(?=[\s>])[^>]*\sdata-col="{k}"[^>]*>.*?</\1>""", RegexOptions.Singleline);
    }

    [GeneratedRegex(@"<(thead|tbody|tfoot|th|td)(?=[\s>])(?![^>]*\sclass=)\s?")]
    private static partial Regex Unclassed();

    [GeneratedRegex(@"<tbody[^>]*>.*?</tbody>", RegexOptions.Singleline)]
    private static partial Regex BodyRows();

    [GeneratedRegex(@"<tr(?=[\s>])(?![^>]*\sclass=)\s?")]
    private static partial Regex BodyRow();

    [GeneratedRegex(@"<tbody class=""ant-table-tbody""\s*>\s*</tbody>")]
    private static partial Regex EmptyBody();

    [GeneratedRegex(@"<th[\s>]")]
    private static partial Regex HeaderCells();
}
