using System.Globalization;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.AspNetCore.WebUtilities;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Infinite scrolling for a server-rendered table: put it after the last row of the page. When it scrolls into view (or
/// its button is pressed) it loads the rows after <see cref="Cursor"/> from <see cref="Url"/> and replaces itself with
/// them. The response is those rows and the next <c>&lt;app-load-more&gt;</c> (none after the last page):
/// <code>
/// &lt;tbody&gt;
///     @foreach (var row in page.Items) { &lt;tr&gt;…&lt;/tr&gt; }
///     &lt;app-load-more url="/orders/rows?status=open" cursor="@page.NextCursor" colspan="7" end-text="All orders loaded" /&gt;
/// &lt;/tbody&gt;
/// </code>
/// Pair it with <c>TableQuery.Page</c> and <c>CountMode.None</c> for the rows endpoint: the cursor continues after the last
/// row's sort value (keyset paging), so the thousandth page costs the same as the first. A request that is still running
/// is not repeated (<c>hx-sync="this:drop"</c>).
/// </summary>
[HtmlTargetElement("app-load-more", TagStructure = TagStructure.WithoutEndTag)]
public class LoadMoreTagHelper : TagHelper
{
    /// <summary>Endpoint returning the next rows (with the table's filters and sort in its query string).</summary>
    public string Url { get; set; } = "";

    /// <summary>Where the next rows start (<c>TablePage.NextCursor</c>); null on the last page.</summary>
    public string? Cursor { get; set; }

    /// <summary>Columns the row spans.</summary>
    public int Colspan { get; set; } = 1;

    public string Text { get; set; } = "Load more";

    /// <summary>Shown after the last row; nothing when null.</summary>
    public string? EndText { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "tr";
        output.TagMode = TagMode.StartTagAndEndTag;
        var colspan = Math.Max(1, Colspan).ToString(CultureInfo.InvariantCulture);
        if (Cursor is null)
        {
            if (EndText is null) { output.SuppressOutput(); return; }
            output.AddClasses("m-load-end");
            output.Content.SetHtmlContent($"""<td class="ant-table-cell" colspan="{colspan}">{Html.E(EndText)}</td>""");
            return;
        }

        output.AddClasses("m-load-more");
        output.Attributes.SetAttribute("hx-get", QueryHelpers.AddQueryString(Url, "cursor", Cursor));
        output.Attributes.SetAttribute("hx-trigger", "revealed, click from:find button");
        // The row replaces itself, whatever the table's container would inherit (a list's boost area selects its own
        // element and pushes URLs; these rows are neither).
        output.Attributes.SetAttribute("hx-target", "this");
        output.Attributes.SetAttribute("hx-select", "unset");
        output.Attributes.SetAttribute("hx-swap", "outerHTML");
        output.Attributes.SetAttribute("hx-push-url", "false");
        output.Attributes.SetAttribute("hx-sync", "this:drop");
        output.Content.SetHtmlContent(
            $"""<td class="ant-table-cell" colspan="{colspan}"><button type="button" class="ant-btn ant-btn-link"><span class="m-load-more-spin" aria-hidden="true">{AntIcons.Span("loading", AntDesign.IconThemeType.Outline, "anticon-spin")}</span><span>{Html.E(Text)}</span></button></td>""");
    }
}

/// <summary>
/// A virtual table: however many rows match, only the ones in view (and a few either side) are in the page. Rows come
/// from <see cref="Src"/> in blocks (<c>?current={block}&amp;pageSize={block size}</c>, the same query any numbered page
/// uses), rendered by the server as <c>&lt;tr&gt;</c> rows, so a row looks exactly as it does in a paged table.
/// <code>
/// &lt;app-virtual-table src="/orders/rows?status=open" total="@page.Total" row-height="40" height="560" scroll-x="1100"&gt;
///     &lt;colgroup&gt;…&lt;/colgroup&gt;
///     &lt;thead class="ant-table-thead"&gt;…&lt;/thead&gt;
///     &lt;template data-m-virtual-seed&gt;@* the first block, rendered with the page *@&lt;/template&gt;
/// &lt;/app-virtual-table&gt;
/// </code>
/// <c>modulus-virtual.js</c> keeps a small cache of blocks, cancels requests for blocks scrolled past, and draws on
/// animation frames. Rows must have the fixed <see cref="RowHeight"/> (cells don't wrap). The header stays in view.
/// </summary>
[HtmlTargetElement("app-virtual-table")]
public class VirtualTableTagHelper : TagHelper
{
    /// <summary>Endpoint returning a block of rows as <c>&lt;tr&gt;</c> elements.</summary>
    public string Src { get; set; } = "";

    /// <summary>Rows in all (from the count of the first page).</summary>
    public int Total { get; set; }

    /// <summary>Height of every row in pixels.</summary>
    public int RowHeight { get; set; } = 40;

    /// <summary>Height of the scrolling area in pixels.</summary>
    public int Height { get; set; } = 560;

    /// <summary>Rows per request (at most 200).</summary>
    public int Block { get; set; } = 100;

    /// <summary>Table width in pixels; wider tables also scroll horizontally.</summary>
    public int? ScrollX { get; set; }

    /// <summary>Accessible name of the scrolling region.</summary>
    public string Label { get; set; } = "Table";

    /// <summary>default | middle | small</summary>
    public string Size { get; set; } = "middle";

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var inner = (await output.GetChildContentAsync()).GetContent();
        var c = CultureInfo.InvariantCulture;
        var block = Math.Clamp(Block, 10, 200);
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-table-wrapper", "m-virtual");
        output.Attributes.SetAttribute("data-m-virtual", "");
        output.Attributes.SetAttribute("data-src", Src);
        output.Attributes.SetAttribute("data-total", Math.Max(0, Total).ToString(c));
        output.Attributes.SetAttribute("data-row-height", Math.Max(16, RowHeight).ToString(c));
        output.Attributes.SetAttribute("data-block", block.ToString(c));
        output.Attributes.SetAttribute("style", $"--m-row-h: {Math.Max(16, RowHeight).ToString(c)}px");
        var table = Html.Cx("ant-table", Size == "default" ? null : $"ant-table-{Size}", ScrollX is null ? null : "ant-table-scroll-horizontal");
        var width = ScrollX is { } x ? $"width: {x.ToString(c)}px; min-width: 100%; " : "width: 100%; ";
        output.Content.SetHtmlContent(
            $"""<div class="{table}"><div class="ant-table-container"><div class="ant-table-content m-virtual-scroll" style="height: {Math.Max(120, Height).ToString(c)}px" tabindex="0" role="region" aria-label="{Html.E(Label)}"><table style="{width}table-layout: fixed;" aria-rowcount="{(Math.Max(0, Total) + 1).ToString(c)}">{inner}<tbody class="ant-table-tbody" data-m-virtual-body></tbody></table></div></div></div>""");
    }
}
