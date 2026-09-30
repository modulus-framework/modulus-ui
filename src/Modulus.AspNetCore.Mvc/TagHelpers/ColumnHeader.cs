using AntDesign;
using Modulus.Ui.Abstractions;
using SortDirection = Modulus.Ui.Abstractions.SortDirection;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// The header of a table column as every Modulus table renders it (data table, editable grid, nested grid; the Blazor
/// twin is <c>ColumnHeader.razor</c>): the title, Ant Design's sorter carets and a search icon that opens a column
/// search box (<see cref="ColumnSearch"/>). Put <see cref="ThClass"/> and <see cref="AriaSort"/> on the <c>&lt;th&gt;</c>.
/// </summary>
public static class ColumnHeader
{
    /// <summary>Renders the header content.</summary>
    /// <param name="key">Column key, also the <c>s.{key}</c> of its search.</param>
    /// <param name="sortHref">Server sorting: the URL of the next sort. Without it the sorter is a button (<paramref name="sortAttributes"/> handles it).</param>
    /// <param name="sortAttributes">Extra attributes of the sorter (<c>hx-get</c>…, or an Alpine <c>@click</c>).</param>
    /// <param name="searchHref">Server search: the URL without this column's search; the box appends <c>s.{key}=…</c>. Without it the box raises <c>m-col-search</c>.</param>
    /// <param name="searchAttributes">Extra attributes of the search box's host (<c>hx-target</c>, <c>hx-swap</c>, used by its request).</param>
    /// <param name="extra">Markup after the search icon (a column menu button).</param>
    /// <param name="choices">Choices of the column's box in the filter row (a select instead of a text box); see <see cref="GridSchema.SearchChoices"/>.</param>
    public static string Content(string key, string title, ColumnKind kind = ColumnKind.Text, bool required = false,
        bool sortable = true, SortDirection sort = SortDirection.None, string? sortHref = null, string? sortAttributes = null,
        bool searchable = true, string? search = null, string? searchHref = null, string? searchAttributes = null, string? extra = null,
        IReadOnlyList<ColumnChoice>? choices = null)
    {
        var label = Html.E(title) + (required ? """<span class="m-grid-required" aria-hidden="true">*</span>""" : "");
        string head;
        if (sortable)
        {
            var state = sort switch { SortDirection.Ascending => " (ascending)", SortDirection.Descending => " (descending)", _ => "" };
            var carets = $"""<span class="ant-table-column-sorter ant-table-column-sorter-full"><span class="ant-table-column-sorter-inner">{AntIcons.Span("caret-up", IconThemeType.Outline, Html.Cx("ant-table-column-sorter-up", sort == SortDirection.Ascending ? "active" : null))}{AntIcons.Span("caret-down", IconThemeType.Outline, Html.Cx("ant-table-column-sorter-down", sort == SortDirection.Descending ? "active" : null))}</span></span>""";
            var body = $"""<span class="ant-table-column-title">{label}</span>{carets}""";
            var aria = $""" title="Sort by {Html.E(title)}" aria-label="Sort by {Html.E(title)}{state}" """.TrimEnd();
            head = sortHref is not null
                ? $"""<a class="ant-table-column-sorters m-th-sort" href="{Html.E(sortHref)}"{aria}{sortAttributes}>{body}</a>"""
                : $"""<span class="ant-table-column-sorters m-th-sort" role="button" tabindex="0"{aria}{sortAttributes}>{body}</span>""";
        }
        else head = $"""<span class="ant-table-column-title m-th-title">{label}</span>""";

        return $"""<span class="m-th" data-key="{Html.E(key)}">{head}{(searchable ? Search(key, title, kind, search, searchHref, searchAttributes, choices) : "")}{extra}</span>""";
    }

    /// <summary>Classes for the <c>&lt;th&gt;</c> of a sortable column (with a leading space).</summary>
    public static string ThClass(bool sortable, SortDirection sort) =>
        !sortable ? "" : sort == SortDirection.None ? " ant-table-column-has-sorters" : " ant-table-column-has-sorters ant-table-column-sort";

    /// <summary>The <c>aria-sort</c> attribute of the <c>&lt;th&gt;</c> (with a leading space), empty when unsortable.</summary>
    public static string AriaSort(bool sortable, SortDirection sort) =>
        !sortable ? "" : $" aria-sort=\"{sort switch { SortDirection.Ascending => "ascending", SortDirection.Descending => "descending", _ => "none" }}\"";

    // The search icon and its box (Alpine columnSearch in modulus-widgets.js places it under the icon, position: fixed).
    private static string Search(string key, string title, ColumnKind kind, string? search, string? searchHref, string? attributes, IReadOnlyList<ColumnChoice>? choices)
    {
        var active = !string.IsNullOrWhiteSpace(search);
        var t = Html.E(title);
        var href = searchHref is null ? "" : $" data-search-href=\"{Html.E(searchHref)}\"";
        return $"""<span class="m-th-search-host" x-data="columnSearch" data-key="{Html.E(key)}"{href}{attributes} @click.outside="close()" @keydown.escape.stop="close()">""" +
               $"""<span class="{Html.Cx("ant-table-filter-trigger", "m-th-search", active ? "active" : null)}" role="button" tabindex="0" title="Search {t}" aria-label="Search {t}{(active ? $": {Html.E(search)}" : "")}" aria-haspopup="dialog" :aria-expanded="open" x-ref="trigger" @click.stop.prevent="toggle()" @keydown.enter.prevent="toggle()" @keydown.space.prevent="toggle()">{AntIcons.Span("search")}</span>""" +
               $"""<div class="ant-dropdown m-col-search-drop" role="dialog" aria-label="Search {t}" x-show="open" x-cloak :style="style" @click.stop>""" +
               $"""<div class="ant-table-filter-dropdown m-col-search"><div class="m-col-search-body">""" +
               $"""<input class="ant-input ant-input-sm" type="text" value="{Html.E(search)}" placeholder="Search {Html.E(title.ToLowerInvariant())}" aria-label="Search {t}" autocomplete="off" maxlength="{ColumnSearch.MaxLength}" x-ref="input" @keydown.enter.prevent.stop="apply()" />""" +
               $"""<div class="m-col-search-hint">{Html.E(ColumnSearch.Hint(kind))}</div></div>""" +
               """<div class="ant-table-filter-dropdown-btns"><button type="button" class="ant-btn ant-btn-link ant-btn-sm" @click="reset()"><span>Reset</span></button><button type="button" class="ant-btn ant-btn-primary ant-btn-sm" @click="apply()"><span>Search</span></button></div>""" +
               "</div></div>" +
               // The filter row under the headers (shown when the table's filter row is on): the same search, typed in
               // place, or picked from a select for option columns.
               (choices is null
                   ? $"""<input class="ant-input ant-input-sm m-th-filter{(active ? " m-th-filter-active" : "")}" type="text" value="{Html.E(search)}" placeholder="{Html.E(ColumnSearch.FilterPlaceholder(kind))}" aria-label="Filter {t}" title="{Html.E(ColumnSearch.Hint(kind))}" autocomplete="off" maxlength="{ColumnSearch.MaxLength}" x-ref="inline" @click.stop @keydown.enter.prevent.stop="applyInline()" @keydown.escape.stop="$el.blur()" @change="applyInline()" />"""
                   : FilterSelect(t, title, search, active, choices)) +
               "</span>";
    }

    private static string FilterSelect(string encodedTitle, string title, string? search, bool active, IReadOnlyList<ColumnChoice> choices)
    {
        var current = search?.Trim();
        var options = string.Concat(ColumnSearch.WithCurrent(choices, current).Select(c =>
            $"""<option value="{Html.E(c.Query)}"{(c.Query == current ? " selected" : "")}>{Html.E(c.Label)}</option>"""));
        return $"""<select class="m-select m-th-filter{(active ? " m-th-filter-active" : "")}" aria-label="Filter {encodedTitle}" x-ref="inline" @click.stop @change="applyInline()"><option value="">{Html.E(ColumnSearch.AnyText(title))}</option>{options}</select>""";
    }
}
