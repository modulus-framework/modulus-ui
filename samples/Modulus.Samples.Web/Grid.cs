using System.Text;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Modulus.AspNetCore.Mvc;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.Samples.Web;

/// <summary>
/// Server-rendered Tabler datagrid helpers: links that keep the current filters, sortable headers
/// and the pager. Everything is a plain GET link, so the grid works without script; the grid
/// container boosts them with htmx (hx-select swaps just the grid).
/// </summary>
public sealed class Grid(HttpRequest request, ProTableRequest paging)
{
    public ProTableRequest Paging { get; } = paging;

    public string Path => request.Path;

    /// <summary>The current URL with some query values replaced (null removes). Filters reset to page 1.</summary>
    public string Href(params (string Key, string? Value)[] changes)
    {
        var query = request.Query.ToDictionary(q => q.Key, q => q.Value.ToString(), StringComparer.OrdinalIgnoreCase);
        if (!changes.Any(c => c.Key.Equals("current", StringComparison.OrdinalIgnoreCase))) query.Remove("current");
        foreach (var (key, value) in changes)
        {
            if (string.IsNullOrEmpty(value)) query.Remove(key);
            else query[key] = value;
        }
        return QueryHelpers.AddQueryString(request.Path, query.Where(q => !string.IsNullOrEmpty(q.Value)).Select(q => new KeyValuePair<string, string?>(q.Key, q.Value)));
    }

    /// <summary>The current URL (for refreshing the grid after a change).</summary>
    public string Current => request.Path + request.QueryString;

    /// <summary>The column searches in the URL (<c>s.{field}</c>).</summary>
    public IReadOnlyDictionary<string, string?> Searches { get; } = request.Query.ColumnSearches() ?? new Dictionary<string, string?>();

    /// <summary>
    /// A column header with Ant Design's sorter (a click cycles ascending → descending → none) and, when searchable, a column search box
    /// (<c>s.{field}</c>), as every Modulus table renders it (<see cref="ColumnHeader"/>).
    /// </summary>
    /// <param name="col">Column-picker key (<c>data-col</c>) when the column is optional.</param>
    public IHtmlContent SortTh(string field, string title, string? style = null, string? col = null, bool searchable = false, ColumnKind kind = ColumnKind.Text)
    {
        var current = string.Equals(Paging.SortField, field, StringComparison.OrdinalIgnoreCase) ? Paging.SortOrder : SortDirection.None;
        var next = ColumnSearch.NextSort(current);
        var href = next == SortDirection.None
            ? Href(("sortField", null), ("sortOrder", null))
            : Href(("sortField", field), ("sortOrder", next.ToString()));
        var content = ColumnHeader.Content(field, title, kind, sort: current, sortHref: href, searchable: searchable,
            search: Searches.GetValueOrDefault(field), searchHref: Href((ColumnSearch.Prefix + field, null)));
        return new HtmlString(
            $"""<th{(col is null ? "" : $" data-col=\"{E(col)}\"")} class="ant-table-cell{ColumnHeader.ThClass(true, current)}"{ColumnHeader.AriaSort(true, current)}{(style is null ? "" : $" style=\"{style}\"")}>{content}</th>""");
    }

    /// <summary>Whether any of the given query values (search, filters) or a column search is set.</summary>
    public bool Filtered(params string[] filters) =>
        Searches.Count > 0 || filters.Any(f => !string.IsNullOrWhiteSpace(request.Query[f]));

    /// <summary>The Clear link for a toolbar: the URL without the given filters and column searches, or null when none is set.</summary>
    public string? ClearLink(params string[] filters) => Filtered(filters) ? ClearHref(filters) : null;

    /// <summary>The current URL without the given filters and every column search (Clear filters).</summary>
    public string ClearHref(params string[] filters) =>
        Href([.. filters.Select(f => (f, (string?)null)), .. Searches.Keys.Select(k => (ColumnSearch.Prefix + k, (string?)null))]);

    /// <summary>" ant-table-column-sort" for the body cells of the sorted column.</summary>
    public string SortClass(string field) =>
        Paging.SortOrder != SortDirection.None && string.Equals(Paging.SortField, field, StringComparison.OrdinalIgnoreCase) ? " ant-table-column-sort" : "";

    /// <summary>Ant Design pagination (mini, like a Middle-size table) plus a page-size switcher.</summary>
    public IHtmlContent Pager(int total)
    {
        var size = Paging.PageSize;
        var pages = Math.Max(1, (int)Math.Ceiling(total / (double)size));
        var current = Math.Min(Paging.Current, pages);
        var sb = new StringBuilder("""<ul class="ant-table-pagination ant-table-pagination-right ant-pagination ant-pagination-mini" aria-label="Pagination">""");
        var from = total == 0 ? 0 : (current - 1) * size + 1;
        sb.Append($"""<li class="ant-pagination-total-text">{from}–{Math.Min(total, current * size)} of {total}</li>""");
        sb.Append(Arrow("prev", current - 1, current == 1, "Previous page", "M724 218.3V141c0-6.7-7.7-10.4-12.9-6.3L260.3 486.8a31.86 31.86 0 0 0 0 50.3l450.8 352.1c5.3 4.1 12.9.4 12.9-6.3v-77.3c0-4.9-2.3-9.6-6.1-12.6l-360-281 360-281.1c3.8-3 6.1-7.7 6.1-12.6z"));
        int? last = null;
        foreach (var page in PageWindow(current, pages))
        {
            if (last is { } l && page - l > 1) sb.Append(Jump(page > current, page > current ? Math.Min(pages, current + 5) : Math.Max(1, current - 5)));
            var active = page == current;
            sb.Append($"""<li class="ant-pagination-item ant-pagination-item-{page}{(active ? " ant-pagination-item-active" : "")}"><a href="{E(Href(("current", page.ToString())))}"{(active ? " aria-current=\"page\"" : "")}>{page}</a></li>""");
            last = page;
        }
        sb.Append(Arrow("next", current + 1, current == pages, "Next page", "M765.7 486.8L314.9 134.7A7.97 7.97 0 0 0 302 141v77.3c0 4.9 2.3 9.6 6.1 12.6l360 281.1-360 281.1c-3.9 3-6.1 7.7-6.1 12.6V883c0 6.7 7.7 10.4 12.9 6.3l450.8-352.1a31.96 31.96 0 0 0 0-50.4z"));
        sb.Append("""<li class="ant-pagination-options"><select class="m-select m-select-sm" aria-label="Rows per page" data-m-nav-select>""");
        foreach (var option in new[] { 10, 20, 50, 100 })
            sb.Append($"""<option value="{E(Href(("pageSize", option.ToString())))}"{(option == size ? " selected" : "")}>{option} / page</option>""");
        sb.Append("""</select></li></ul>""");
        return new HtmlString(sb.ToString());
    }

    // AntD's jump item: ••• that turns into a double arrow on hover and moves five pages.
    private string Jump(bool next, int page)
    {
        var kind = next ? "jump-next" : "jump-prev";
        var icon = next
            ? "M533.2 492.3L277.9 166.1c-3-3.9-7.7-6.1-12.6-6.1H188c-6.7 0-10.4 7.7-6.3 12.9L447.1 512 181.7 851.1A7.98 7.98 0 0 0 188 864h77.3c4.9 0 9.6-2.3 12.6-6.1l255.3-326.1c9.1-11.7 9.1-27.9 0-39.5zm304 0L581.9 166.1c-3-3.9-7.7-6.1-12.6-6.1H492c-6.7 0-10.4 7.7-6.3 12.9L751.1 512 485.7 851.1A7.98 7.98 0 0 0 492 864h77.3c4.9 0 9.6-2.3 12.6-6.1l255.3-326.1c9.1-11.7 9.1-27.9 0-39.5z"
            : "M272.9 512l265.4-339.1c4.1-5.2.4-12.9-6.3-12.9h-77.3c-4.9 0-9.6 2.3-12.6 6.1L186.8 492.3a31.99 31.99 0 0 0 0 39.5l255.3 326.1c3 3.9 7.7 6.1 12.6 6.1H532c6.7 0 10.4-7.7 6.3-12.9L272.9 512zm304 0l265.4-339.1c4.1-5.2.4-12.9-6.3-12.9h-77.3c-4.9 0-9.6 2.3-12.6 6.1L490.8 492.3a31.99 31.99 0 0 0 0 39.5l255.3 326.1c3 3.9 7.7 6.1 12.6 6.1H836c6.7 0 10.4-7.7 6.3-12.9L576.9 512z";
        return $"""<li class="ant-pagination-{kind} ant-pagination-{kind}-custom-icon"><a class="ant-pagination-item-link" href="{E(Href(("current", page.ToString())))}" aria-label="{(next ? "Next 5 pages" : "Previous 5 pages")}"><div class="ant-pagination-item-container">{Icon(icon, "ant-pagination-item-link-icon")}<span class="ant-pagination-item-ellipsis">•••</span></div></a></li>""";
    }

    private string Arrow(string kind, int page, bool disabled, string label, string path) =>
        disabled
            ? $"""<li class="ant-pagination-{kind} ant-pagination-disabled" aria-disabled="true"><span class="ant-pagination-item-link" aria-label="{label}">{Icon(path)}</span></li>"""
            : $"""<li class="ant-pagination-{kind}"><a class="ant-pagination-item-link" href="{E(Href(("current", page.ToString())))}" aria-label="{label}">{Icon(path)}</a></li>""";

    // AntD (rc-pagination) window: two pages either side of the current one, five at the ends, plus the first and last page.
    private static IEnumerable<int> PageWindow(int current, int pages)
    {
        if (pages <= 7) return Enumerable.Range(1, pages);
        var left = Math.Max(1, current - 2);
        var right = Math.Min(pages, current + 2);
        if (current - 1 <= 2) right = 5;
        if (pages - current <= 2) left = pages - 4;
        var window = Enumerable.Range(left, right - left + 1);
        if (left != 1) window = window.Prepend(1);
        if (right != pages) window = window.Append(pages);
        return window;
    }

    private static string Icon(string path, string? cssClass = null) =>
        $"""<span class="anticon{(cssClass is null ? "" : " " + cssClass)}" role="img" aria-hidden="true"><svg viewBox="0 0 1024 1024" width="1em" height="1em" fill="currentColor" focusable="false"><path d="{path}"/></svg></span>""";

    private static string E(string value) => System.Net.WebUtility.HtmlEncode(value);
}
