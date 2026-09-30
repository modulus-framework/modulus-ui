using System.Text;
using Microsoft.AspNetCore.Razor.TagHelpers;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Ant Design pagination driven by htmx. Each page link issues
/// <c>hx-get="{url}?current=N"</c> into <c>hx-target</c>, carrying the elements in <c>hx-include</c>
/// (typically the filter form) so search state survives paging.
/// </summary>
[HtmlTargetElement("app-pagination", TagStructure = TagStructure.WithoutEndTag)]
public class PaginationTagHelper : TagHelper
{
    public int Total { get; set; }
    public int Current { get; set; } = 1;
    public int PageSize { get; set; } = 10;

    /// <summary>Endpoint returning the table partial.</summary>
    public string Url { get; set; } = "";

    [HtmlAttributeName("hx-target")] public string? HxTarget { get; set; }
    [HtmlAttributeName("hx-include")] public string? HxInclude { get; set; }

    /// <summary>Extra query string appended to every link (sort state), without a leading '&amp;'.</summary>
    public string? Query { get; set; }

    /// <summary>default | small</summary>
    public string Size { get; set; } = "default";

    /// <summary>Shows "1–10 of 85" before the pages.</summary>
    public bool ShowTotal { get; set; } = true;

    /// <summary>Aligns right with table spacing (under a table). Set false for standalone use.</summary>
    public bool Table { get; set; } = true;

    /// <summary>
    /// Load pages with htmx (hx-get into <c>hx-target</c>). Defaults to true when hx-target is set;
    /// otherwise pages are plain links, which also work with <c>hx-boost</c>.
    /// </summary>
    public bool? Htmx { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var pages = Math.Max(1, (int)Math.Ceiling(Total / (double)PageSize));
        var current = Math.Clamp(Current, 1, pages);
        var from = Total == 0 ? 0 : (current - 1) * PageSize + 1;
        var to = Math.Min(current * PageSize, Total);

        output.TagName = "ul";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("ant-pagination", Size == "small" ? "ant-pagination-mini" : null, Table ? "ant-table-pagination" : null, Table ? "ant-table-pagination-right" : null);
        output.Attributes.SetAttribute("aria-label", "Pagination");

        var sb = new StringBuilder(ShowTotal ? $"""<li class="ant-pagination-total-text">{from}–{to} of {Total:N0}</li>""" : "");
        sb.Append(Item("prev", current - 1, current == 1, AntIcons.Span("left"), "Previous page", false));
        foreach (var p in Window(current, pages))
        {
            if (p == 0)
            {
                // AntD's jump item: ••• that turns into a double arrow on hover and moves five pages.
                var next = sb.ToString().Contains("ant-pagination-item-active");
                var ellipsis = $"""<div class="ant-pagination-item-container">{AntIcons.Span(next ? "double-right" : "double-left", cssClass: "ant-pagination-item-link-icon")}<span class="ant-pagination-item-ellipsis">•••</span></div>""";
                sb.Append(next
                    ? Item("jump-next", Math.Min(pages, current + 5), false, ellipsis, "Next 5 pages", false)
                    : Item("jump-prev", Math.Max(1, current - 5), false, ellipsis, "Previous 5 pages", false));
                continue;
            }
            sb.Append(Item("item", p, false, p.ToString(), $"Page {p}", p == current));
        }
        sb.Append(Item("next", current + 1, current == pages, AntIcons.Span("right"), "Next page", false));
        output.Content.SetHtmlContent(sb.ToString());
    }

    private string Item(string kind, int page, bool disabled, string label, string aria, bool active)
    {
        var cls = kind == "item"
            ? $"ant-pagination-item ant-pagination-item-{page}{(active ? " ant-pagination-item-active" : "")}"
            : $"ant-pagination-{kind}{(kind.StartsWith("jump") ? $" ant-pagination-{kind}-custom-icon" : "")}{(disabled ? " ant-pagination-disabled" : "")}";
        if (disabled || active)
            return $"""<li class="{cls}"{(active ? " aria-current=\"page\"" : " aria-disabled=\"true\"")}><a class="{(kind == "item" ? "" : "ant-pagination-item-link")}" aria-label="{aria}">{label}</a></li>""";
        var query = $"current={page}&pageSize={PageSize}" + (string.IsNullOrEmpty(Query) ? "" : "&" + Query);
        var href = $"{Url}?{query}";
        var linkClass = kind == "item" ? "" : "ant-pagination-item-link";
        if (!(Htmx ?? HxTarget is not null))
            return $"""<li class="{cls}"><a class="{linkClass}" href="{Html.E(href)}" aria-label="{aria}">{label}</a></li>""";
        var target = HxTarget is null ? "" : $""" hx-target="{Html.E(HxTarget)}" """;
        var include = HxInclude is null ? "" : $""" hx-include="{Html.E(HxInclude)}" """;
        return $"""<li class="{cls}"><a class="{linkClass}" href="#" aria-label="{aria}" hx-get="{Html.E(href)}"{target}{include}>{label}</a></li>""";
    }

    /// <summary>Pages to show; 0 marks an ellipsis (<see cref="Modulus.Ui.Abstractions.Paging.Window"/>).</summary>
    private static IEnumerable<int> Window(int current, int pages) => Modulus.Ui.Abstractions.Paging.Window(current, pages);
}
