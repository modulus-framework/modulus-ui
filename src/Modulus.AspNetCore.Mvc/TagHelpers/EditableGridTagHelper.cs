using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AntDesign;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Plugins;
using SortDirection = Modulus.Ui.Abstractions.SortDirection;
using Modulus.Ui.Abstractions.Shell;

namespace Modulus.AspNetCore.Mvc.TagHelpers;

/// <summary>
/// Schema-driven editable grid, the MVC twin of Blazor's <c>EditableGrid</c> (same DOM, same model):
/// <code>
/// &lt;form method="post" hx-post hx-target="#projects" hx-select="#projects" hx-swap="outerHTML"&gt;
///     &lt;app-editable-grid id="projects" columns="Model.Columns" rows="Model.Rows" header-title="Projects"
///                        errors="Model.Errors" row-detail="detail" /&gt;
/// &lt;/form&gt;
/// </code>
/// Click a cell to edit it; switches and ratings change in place; rows are added and deleted in the browser.
/// Column operations (pin, hide, move, remove, add) and Save post the form with a <c>{name}.op</c> value; read the
/// post with <see cref="EditableGridForm.Read"/>, apply it with <see cref="EditableGridForm.Apply"/> and render the
/// grid again. Every post carries the schema and all rows, so nothing is lost between operations.
/// </summary>
[HtmlTargetElement("app-editable-grid")]
public partial class EditableGridTagHelper : TagHelper
{
    public IList<GridColumn> Columns { get; set; } = [];

    public IReadOnlyList<GridRecord> Rows { get; set; } = [];

    /// <summary>Prefix of the posted fields (<c>grid.schema</c>, <c>grid.index</c>, <c>grid[id].key</c>, <c>grid.op</c>).</summary>
    public string Name { get; set; } = "grid";

    public string? HeaderTitle { get; set; }

    /// <summary>Cell errors to show (from <see cref="GridSchema.Validate"/> after a Save).</summary>
    public IEnumerable<GridError>? Errors { get; set; }

    /// <summary>Show "Unsaved changes" (the posted grid differs from the saved one).</summary>
    public bool Dirty { get; set; }

    /// <summary>Content of an expanded row, e.g. a Razor template <c>@&lt;div&gt;…@item["notes"]…&lt;/div&gt;</c>.</summary>
    public Func<GridRecord, IHtmlContent>? RowDetail { get; set; }

    /// <summary>Which rows expand (the Blazor grid's RowExpandable): a row that doesn't gets no expander. Default: all.</summary>
    public Func<GridRecord, bool>? RowExpandable { get; set; }

    public bool AllowAddRow { get; set; } = true;

    /// <summary>Tooltip of the add-row button ("Add style", say).</summary>
    public string AddRowText { get; set; } = "Add row";

    public bool AllowDeleteRow { get; set; } = true;

    /// <summary>Column menus, the column settings panel and "Add column".</summary>
    public bool AllowColumnEdit { get; set; } = true;

    public bool ShowSave { get; set; } = true;

    public bool ShowDiscard { get; set; } = true;

    /// <summary>A search box that hides the rows whose visible text doesn't contain the keyword (in the browser).</summary>
    public bool Searchable { get; set; } = true;

    /// <summary>A filter row with a dropdown for each <see cref="GridColumn.Filterable"/> column (filters in the browser).</summary>
    public bool ClientFilters { get; set; } = true;

    /// <summary>A checkbox column and a batch bar for the selected rows: set a value, export, delete.</summary>
    public bool Selectable { get; set; } = true;

    /// <summary>"Export CSV" (the rows shown) in the toolbar and "Export selected" in the batch bar.</summary>
    public bool Exportable { get; set; } = true;

    /// <summary>File name of the CSV export (default: the title).</summary>
    public string? ExportFileName { get; set; }

    /// <summary>
    /// A pager under the grid ("1–10 of 42", pages, rows per page), paging the rows after search and filters in the
    /// browser (modulusGrid). Off for a grid whose rows are one page of a server list (the nested grid's levels).
    /// </summary>
    public bool Paged { get; set; } = true;

    /// <summary>Rows per page to start with; the pager also offers <see cref="Paging.Sizes"/>.</summary>
    [HtmlAttributeName("page-size")] public int PageSize { get; set; } = 10;

    /// <summary>The Columns button (show and hide columns) even when <see cref="AllowColumnEdit"/> is off.</summary>
    public bool AllowColumnHide { get; set; } = true;

    /// <summary>Drag a header's right edge to resize the column; the width goes into the posted schema.</summary>
    public bool Resizable { get; set; } = true;

    /// <summary>With <see cref="ReloadTarget"/>: a Reload button that fetches this URL with htmx.</summary>
    public string? ReloadHref { get; set; }

    public string? ReloadTarget { get; set; }

    /// <summary>Column headers sort the rows: ascending, descending, then unsorted (in the browser, or with <see cref="SortHref"/>).</summary>
    public bool Sortable { get; set; } = true;

    /// <summary>
    /// A drag handle on each row: drag it (or focus it and press Up, Down, Home, End) to reorder the rows. The rows
    /// post in their new order. Locked while a column sort is on.
    /// </summary>
    [HtmlAttributeName("row-draggable")] public bool RowDraggable { get; set; } = true;

    /// <summary>A search icon in each column header for a search of that column (see <see cref="ColumnSearch"/>).</summary>
    public bool ColumnSearchable { get; set; } = true;

    /// <summary>Starts with the filter row (a box or select under each column header) shown; the toolbar's filter button still toggles it.</summary>
    [HtmlAttributeName("filter-row-open")] public bool FilterRowOpen { get; set; }

    /// <summary>Server sorting (the nested grid): column key → URL of the next sort. Null sorts in the browser.</summary>
    public Func<string, string>? SortHref { get; set; }

    /// <summary>With <see cref="SortHref"/>: the column the server sorted by.</summary>
    public string? SortField { get; set; }

    public bool SortDescending { get; set; }

    /// <summary>Server column search: column key → URL without that column's search. Null searches in the browser.</summary>
    public Func<string, string>? SearchHref { get; set; }

    /// <summary>With <see cref="SearchHref"/>: the column searches the server applied.</summary>
    public IReadOnlyDictionary<string, string?>? Searches { get; set; }

    /// <summary>With <see cref="SortHref"/> or <see cref="SearchHref"/>: the element the htmx response replaces (outerHTML).</summary>
    public string? ServerTarget { get; set; }

    /// <summary>Bands over the column titles (a multi-level header): <see cref="HeaderGroup"/>s over <see cref="GridColumn.Key"/>s.</summary>
    public IReadOnlyList<HeaderGroup>? HeaderGroups { get; set; }

    /// <summary>
    /// Buttons in the batch bar that act on the selected rows (Approve, Reject, <see cref="GridBatchAction.Printing"/>).
    /// Each counts the rows it applies to, asks first when <see cref="GridBatchAction.Confirm"/> is set, then posts
    /// <c>{name}.op=batch:{key}</c> with the row ids in <c>{name}.selected</c>; read it with <see cref="EditableGridForm.Batch"/>.
    /// Print runs in the browser.
    /// </summary>
    public IReadOnlyList<GridBatchAction>? Batch { get; set; }

    private const int ExpandWidth = 48;
    private const int SelectionWidth = 48;
    private const int ActionsWidth = 64;

    public override async Task ProcessAsync(TagHelperContext context, TagHelperOutput output)
    {
        var slots = new Dictionary<string, string>();
        context.Items[typeof(EditableGridTagHelper)] = slots;
        await output.GetChildContentAsync();

        var culture = CultureInfo.CurrentCulture;
        var visible = GridSchema.Visible(Columns);
        var detail = RowDetail is not null;
        var lead = DragWidth + (Selectable ? SelectionWidth : 0) + (detail ? ExpandWidth : 0);
        var offsets = GridSchema.StickyOffsets(visible, lead);
        var errors = (Errors ?? []).GroupBy(e => (e.RowId, e.Key)).ToDictionary(g => g.Key, g => g.First().Message);
        var hasLeft = visible.Any(c => c.Fixed == GridFixed.Left);
        var hasRight = AllowDeleteRow || visible.Any(c => c.Fixed == GridFixed.Right);
        var width = GridSchema.ScrollWidth(Columns, lead + (AllowDeleteRow ? ActionsWidth : 0));
        var id = output.Attributes.TryGetAttribute("id", out var idAttr) ? idAttr.Value?.ToString() ?? "" : $"grid-{context.UniqueId}";
        var colspan = visible.Count + (RowDraggable ? 1 : 0) + (Selectable ? 1 : 0) + (detail ? 1 : 0) + (AllowDeleteRow ? 1 : 0);
        var frame = new GridFrame(id, culture, FilterRowOpen && ColumnSearchable, visible, offsets, errors, detail, hasLeft, hasRight, width, colspan);

        ConfigureTag(output, frame);

        var sb = new StringBuilder("""<div class="ant-card-body">""");
        if (RowDraggable) sb.Append(SortableHtml.RowsHelp(HelpId(id)));
        sb.Append($"""<input type="hidden" name="{Html.E(Name)}.schema" value="{Html.E(GridSchema.Serialize(Columns))}" x-ref="schema" />""");
        Toolbar(sb, slots);
        FilterRow(sb, slots);

        if (Selectable) sb.Append(BatchBar(slots.GetValueOrDefault("grid-batch")));

        if (errors.Count > 0)
            sb.Append(ErrorAlert(errors));

        Table(sb, frame);
        if (AllowAddRow) sb.Append(RowTemplate(frame));

        if (Paged) sb.Append(Pager());
        if (slots.TryGetValue("grid-summary", out var summary)) sb.Append($"""<div class="m-grid-summary">{summary}</div>""");
        sb.Append("</div>");

        if (AllowColumnEdit)
        {
            sb.Append(ColumnMenu());
            sb.Append(ColumnDialog(id));
        }
        output.Content.SetHtmlContent(sb.ToString());
    }
}
