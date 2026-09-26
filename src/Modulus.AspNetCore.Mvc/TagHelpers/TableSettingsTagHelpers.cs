using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Shell;

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

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "div";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.AddClasses("m-table-toolbar-settings");
        var reload = ReloadHref is null ? "" :
            $"""<a class="ant-btn ant-btn-text ant-btn-icon-only" href="{Html.E(ReloadHref)}" aria-label="Reload" title="Reload">{AntIcons.Span("reload")}</a>""";
        var density = !Density ? "" : DropdownTagHelper.Render(
            $"""<button type="button" class="ant-btn ant-btn-text ant-btn-icon-only" aria-label="Table density" title="Density">{AntIcons.Span("column-height")}</button>""",
            Level("Comfortable", "") + Level("Standard", "ant-table-middle") + Level("Compact", "ant-table-small"),
            "bottomRight");
        output.Content.SetHtmlContent(reload + density);
    }

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
                $"""<span class="m-column-toggle">{ShellRenderer.Icon(Hidden.Contains(c.Key) ? "square" : "square-check")}{Html.E(c.Title)}</span>""",
                Href(TableColumns.ToggleHidden(Hidden, c.Key, columns)))));
        output.TagName = null;
        output.Content.SetHtmlContent(DropdownTagHelper.Render(
            $"""<button type="button" class="ant-btn ant-btn-default">{ShellRenderer.Icon("columns")}<span>{Html.E(Label)}</span></button>""",
            items, "bottomRight"));
    }
}
