using AntDesign;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// The reload and density buttons at the right of a table toolbar, as Ant Design Pro's ProTable renders them:
/// <code>&lt;app-table-settings reload-href="@grid.Current" /&gt;</code>
/// Density needs the card's <c>x-data="tableSelection"</c> scope and <c>&lt;app-table density&gt;</c> (or
/// <c>:class="density"</c> on <c>.ant-table</c>); the choice is remembered in localStorage.
/// </summary>
[HtmlTargetElement("app-table-settings", TagStructure = TagStructure.WithoutEndTag)]
public class TableSettingsTagHelper : TagHelper
{
    /// <summary>Link for the reload button (usually the current URL); no button when null.</summary>
    public string? ReloadHref { get; set; }

    /// <summary>Show the density menu.</summary>
    public bool Density { get; set; } = true;

    /// <summary>
    /// A filter button that shows each header's column search as a box under its title (a filter row), for tables whose
    /// headers use <see cref="ColumnHeader"/>. modulus-shell.js toggles it and keeps it open while a column is searched.
    /// </summary>
    [HtmlAttributeName("filter-row")] public bool FilterRow { get; set; }

    /// <summary>
    /// A Columns button before the settings that shows or hides the table's columns (modulus-shell.js,
    /// <c>[data-m-table-columns]</c>); the choice is remembered in the browser, per page and table. Turn it off for a
    /// table with a server-side picker (<c>app-table-columns</c>).
    /// </summary>
    public bool Columns { get; set; } = true;

    /// <summary>An Export CSV button that saves the rows shown, in the visible columns (<c>[data-m-table-export]</c>).</summary>
    public bool Export { get; set; } = true;

    /// <summary>File name of the export, without ".csv" (defaults to the table's title).</summary>
    [HtmlAttributeName("export-name")] public string? ExportName { get; set; }

    /// <summary>
    /// The URL without the search and filters: a Clear button before the others. Pass it only while a search or a
    /// filter is set; null or empty hides the button (Razor passes <c>clear-href="@null"</c> as an empty string).
    /// </summary>
    [HtmlAttributeName("clear-href")] public string? ClearHref { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-table-toolbar-settings");
        output.PreElement.SetHtmlContent(
            (string.IsNullOrEmpty(ClearHref) ? "" : ClearButton(ClearHref)) +
            (Columns ? ColumnsButton : "") +
            (Export ? ExportButton(ExportName) : ""));
        var reload = string.IsNullOrEmpty(ReloadHref) ? "" :
            $"""<a class="ant-btn ant-btn-text ant-btn-icon-only" href="{Html.E(ReloadHref)}" aria-label="Reload" title="Reload">{AntIcons.Span("reload")}</a>""";
        var density = !Density ? "" : DropdownTagHelper.Render(
            $"""<button type="button" class="ant-btn ant-btn-text ant-btn-icon-only" aria-label="Table density" title="Density">{AntIcons.Span("column-height")}</button>""",
            Level("Comfortable", "") + Level("Standard", "ant-table-middle") + Level("Compact", "ant-table-small"),
            "bottomRight");
        output.Content.SetHtmlContent((FilterRow ? FilterRowButton : "") + reload + density);
    }

    /// <summary>The toolbar's Clear button: a link to the list without its search and filters.</summary>
    public static string ClearButton(string href) =>
        $"""<a class="ant-btn ant-btn-text m-table-clear" href="{Html.E(href)}" title="Clear the search and filters">{AntIcons.Span("close-circle")}<span>Clear</span></a>""";

    /// <summary>The toolbar's Columns button (modulus-shell.js, [data-m-table-columns]), as the Blazor ProTable renders it.</summary>
    internal static string ColumnsButton =>
        $"""<button type="button" class="ant-btn ant-btn-default m-table-columns" data-m-table-columns aria-haspopup="dialog" aria-expanded="false" title="Show or hide columns">{AntIcons.Span("setting")}<span>Columns</span></button>""";

    /// <summary>The toolbar's Export CSV button (modulus-shell.js, [data-m-table-export]).</summary>
    internal static string ExportButton(string? name) =>
        $"""<button type="button" class="ant-btn ant-btn-default m-table-export" data-m-table-export="{Html.E(name ?? "")}" title="Export the rows shown to a CSV file">{AntIcons.Span("download")}<span>Export CSV</span></button>""";

    /// <summary>The toolbar button that shows and hides a table's filter row (modulus-shell.js, [data-m-filter-row]).</summary>
    internal static string FilterRowButton =>
        $"""<button type="button" class="ant-btn ant-btn-text ant-btn-icon-only m-table-filter-toggle" aria-label="Filter row" title="Show the filter row" aria-pressed="false" data-m-filter-row>{AntIcons.Span("filter")}</button>""";

    private static string Level(string label, string value) =>
        DropdownTagHelper.Item(label, attributes: $""" @click="setDensity('{value}')" :class="density === '{value}' && 'ant-dropdown-menu-item-selected'" """.TrimEnd());
}

/// <summary>
/// Column picker for a server-rendered table: a "Columns" button whose menu toggles optional columns.
/// Each item is a link carrying the new hidden set, so the picker works without script and the state
/// stays in the URL:
/// <code>&lt;app-table-columns columns="EmployeeDirectory.OptionalColumns" hidden="Model.Hidden"
///     href="@(hide =&gt; grid.Href(("hide", hide)))" /&gt;</code>
/// Pair it with <c>&lt;app-table hidden-columns="Model.Hidden"&gt;</c> and <c>data-col</c> on the cells.
/// </summary>
[HtmlTargetElement("app-table-columns", TagStructure = TagStructure.WithoutEndTag)]
public class TableColumnsTagHelper : TagHelper
{
    /// <summary>The optional columns, in display order.</summary>
    public IEnumerable<TableColumn> Columns { get; set; } = [];

    /// <summary>Keys of the columns that are hidden now.</summary>
    public IReadOnlySet<string> Hidden { get; set; } = new HashSet<string>();

    /// <summary>Builds the link for a new "hide" value (null when every column is shown).</summary>
    public Func<string?, string> Href { get; set; } = _ => "";

    public string Label { get; set; } = "Columns";

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        var columns = Columns.ToList();
        var items = string.Concat(columns.Select(c =>
            DropdownTagHelper.Item(
                $"""<span class="m-column-toggle">{(Hidden.Contains(c.Key) ? AntIcons.Span("border") : AntIcons.Span("check-square", IconThemeType.Fill))}{Html.E(c.Title)}</span>""",
                Href(TableColumns.ToggleHidden(Hidden, c.Key, columns)))));
        output.TagName = null;
        output.Content.SetHtmlContent(DropdownTagHelper.Render(
            $"""<button type="button" class="ant-btn ant-btn-default m-table-columns-toggle" title="Show or hide columns">{AntIcons.Span("setting")}<span>{Html.E(Label)}</span></button>""",
            items, "bottomRight"));
    }
}
