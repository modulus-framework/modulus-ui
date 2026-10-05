using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Razor.TagHelpers;
using Microsoft.Extensions.Primitives;
using Modulus.AspNetCore.Mvc.TagHelpers;
using Modulus.Ui.Abstractions;
using Xunit;

namespace Modulus.AspNetCore.Mvc.Tests;

public class EditableGridFormTests
{
    private static List<GridColumn> Columns() =>
    [
        new() { Key = "name", Title = "Name", Required = true },
        new() { Key = "budget", Title = "Budget", Type = GridCellType.Currency, Min = 0 },
        new() { Key = "teams", Title = "Teams", Type = GridCellType.MultiSelect, Options = [new("web", "Web"), new("data", "Data")] },
        new() { Key = "billable", Title = "Billable", Type = GridCellType.Switch },
    ];

    private static FormCollection Form(params (string Key, string[] Values)[] fields) =>
        new(fields.ToDictionary(f => f.Key, f => new StringValues(f.Values)));

    private static FormCollection GridForm(string? op = null, params (string Key, string[] Values)[] extra) => Form(
    [
        ("grid.schema", [GridSchema.Serialize(Columns())]),
        ("grid.index", ["p1", "p2", "../x", "p1"]),
        ("grid[p1].name", [" Portal "]),
        ("grid[p1].budget", ["1250.50"]),
        ("grid[p1].teams", ["web", "", "data", "bogus"]),
        ("grid[p1].billable", ["false", "true"]),
        ("grid[p2].name", ["Mobile"]),
        ("grid[p2].billable", ["false"]),
        ("grid[p2].unknown", ["dropped"]),
        .. op is null ? Array.Empty<(string, string[])>() : [("grid.op", [op])],
        .. extra,
    ]);

    [Fact]
    public void Read_ParsesRowsInOrder_NormalisesValues_AndSkipsBadOrDuplicateIds()
    {
        var post = EditableGridForm.Read(GridForm("save"), Columns());

        Assert.Equal(["p1", "p2"], post.Rows.Select(r => r.Id));
        Assert.Equal("Portal", post.Rows[0]["name"]);
        Assert.Equal("1250.5", post.Rows[0]["budget"]);
        Assert.Equal("web,data", post.Rows[0]["teams"]);
        Assert.Equal(("true", "false"), (post.Rows[0]["billable"], post.Rows[1]["billable"]));
        Assert.False(post.Rows[1].Values.ContainsKey("unknown"));
        Assert.Equal(("save", "save"), (post.Operation, post.Verb));
    }

    [Fact]
    public void Read_FallsBackToServerColumns_WhenTheSchemaIsTampered()
    {
        var form = Form(("grid.schema", ["""[{"key":"no good"}]"""]), ("grid.index", ["p1"]), ("grid[p1].name", ["A"]));
        var fallback = Columns();

        var post = EditableGridForm.Read(form, fallback);

        Assert.Equal(fallback.Select(c => c.Key), post.Columns.Select(c => c.Key));
        Assert.NotSame(fallback[0], post.Columns[0]);
        Assert.Equal("", post.Verb);
    }

    [Fact]
    public void Apply_RowOperations()
    {
        var post = EditableGridForm.Read(GridForm("add-row"), Columns());
        Assert.True(EditableGridForm.Apply(post, GridForm()));
        Assert.Equal(["n3", "p1", "p2"], post.Rows.Select(r => r.Id));
        Assert.Equal("false", post.Rows[0]["billable"]);

        post = EditableGridForm.Read(GridForm("delete-row:p1"), Columns());
        Assert.True(EditableGridForm.Apply(post, GridForm()));
        Assert.Equal(["p2"], post.Rows.Select(r => r.Id));

        // A row the server holds as locked: its posted values are put back, and it can't be deleted.
        post = EditableGridForm.Read(GridForm("delete-row:p1"), Columns());
        GridSchema.Protect(post.Columns, post.Rows, [new("p1", new Dictionary<string, string?> { ["name"] = "Stored", ["billable"] = "false" }) { State = GridRowState.Locked }]);
        Assert.True(EditableGridForm.Apply(post, GridForm()));
        Assert.Equal(["p1", "p2"], post.Rows.Select(r => r.Id));
        Assert.Equal(("Stored", "false"), (post.Rows[0]["name"], post.Rows[0]["billable"]));
    }

    [Theory]
    [InlineData("pin:budget:left", "budget", GridFixed.Left, false)]
    [InlineData("pin:budget:right", "budget", GridFixed.Right, false)]
    [InlineData("pin:budget:none", "budget", GridFixed.None, false)]
    [InlineData("hide:teams", "teams", GridFixed.None, true)]
    [InlineData("toggle:teams", "teams", GridFixed.None, true)]
    public void Apply_ColumnPinAndVisibility(string op, string key, GridFixed expectedFixed, bool expectedHidden)
    {
        var post = EditableGridForm.Read(GridForm(op), Columns());

        Assert.True(EditableGridForm.Apply(post, GridForm()));

        var column = GridSchema.Find(post.Columns, key)!;
        Assert.Equal((expectedFixed, expectedHidden), (column.Fixed, column.Hidden));
    }

    [Fact]
    public void Apply_MoveAndRemoveColumns()
    {
        var post = EditableGridForm.Read(GridForm("move:budget:-1"), Columns());
        EditableGridForm.Apply(post, GridForm());
        Assert.Equal(["budget", "name", "teams", "billable"], post.Columns.Select(c => c.Key));

        post = EditableGridForm.Read(GridForm("remove:teams"), Columns());
        EditableGridForm.Apply(post, GridForm());
        Assert.Equal(["name", "budget", "billable"], post.Columns.Select(c => c.Key));
        Assert.All(post.Rows, r => Assert.False(r.Values.ContainsKey("teams")));
    }

    [Fact]
    public void Apply_AddColumn_ReadsTheNewColumnForm()
    {
        var form = GridForm("add-column",
            ("grid.new.title", ["Region"]), ("grid.new.type", ["Select"]), ("grid.new.options", ["EMEA, APAC"]),
            ("grid.new.fixed", ["Right"]), ("grid.new.required", ["true"]));
        var post = EditableGridForm.Read(form, Columns());

        Assert.True(EditableGridForm.Apply(post, form));

        var region = post.Columns[^1];
        Assert.Equal(("region", GridCellType.Select, GridFixed.Right, true), (region.Key, region.Type, region.Fixed, region.Required));
        Assert.Equal(["EMEA", "APAC"], region.Options.Select(o => o.Label));
        Assert.All(post.Rows, r => Assert.True(r.Values.ContainsKey("region")));
    }

    [Fact]
    public void Apply_AddOption_AddsToTheColumnAndPicksItForTheCell()
    {
        var form = GridForm("add-option:teams:p1", ("grid.option[p1].teams", [" Mobile "]), ("grid.color[p1].teams", ["lime"]));
        var post = EditableGridForm.Read(form, Columns());

        Assert.True(EditableGridForm.Apply(post, form));

        var teams = post.Columns.Single(c => c.Key == "teams");
        Assert.Equal(new GridOption("mobile", "Mobile", "lime"), teams.Options[^1]);
        Assert.Equal("web,data,mobile", post.Rows[0]["teams"]);
        Assert.Null(post.Error);
    }

    [Fact]
    public void Apply_AddOption_ReportsADuplicate_AndIgnoresOtherColumnTypes()
    {
        var form = GridForm("add-option:teams:p2", ("grid.option[p2].teams", ["web"]));
        var post = EditableGridForm.Read(form, Columns());
        EditableGridForm.Apply(post, form);
        Assert.Equal("“web” is already in the list.", post.Error);
        Assert.Equal(2, post.Columns.Single(c => c.Key == "teams").Options.Count);

        var budget = GridForm("add-option:budget:p1", ("grid.option[p1].budget", ["x"]));
        post = EditableGridForm.Read(budget, Columns());
        EditableGridForm.Apply(post, budget);
        Assert.Null(post.Error);
        Assert.Equal("1250.5", post.Rows[0]["budget"]);
    }

    private static readonly GridBatchAction[] Approvals =
    [
        new() { Key = "approve", Label = "Approve", When = r => r["name"] != "Mobile" },
        GridBatchAction.Printing(),
    ];

    [Fact]
    public void Batch_ActsOnTheSelectedRowsItAppliesTo_AndNeverOnALockedRow()
    {
        var post = EditableGridForm.Read(GridForm("batch:approve", ("grid.selected", ["p1", "p2", "../x", "p9"])), Columns());
        Assert.Equal(["p1", "p2", "p9"], post.Selected);

        // p2 fails When; p9 isn't in the grid.
        var request = EditableGridForm.Batch(post, Approvals);
        Assert.NotNull(request);
        Assert.Equal("approve", request.Action.Key);
        Assert.Equal(["p1"], request.Rows.Select(r => r.Id));

        // A locked row is skipped (its state comes from Protect, not the browser).
        post = EditableGridForm.Read(GridForm("batch:approve", ("grid.selected", ["p1"])), Columns());
        GridSchema.Protect(post.Columns, post.Rows, [new("p1", new Dictionary<string, string?> { ["name"] = "Portal" }) { State = GridRowState.Locked }]);
        Assert.Empty(EditableGridForm.Batch(post, Approvals)!.Rows);
    }

    [Theory]
    [InlineData("save")]
    [InlineData("batch")]
    [InlineData("batch:")]
    [InlineData("batch:print")]
    [InlineData("batch:delete")]
    [InlineData("batch:<x>")]
    public void Batch_IsNull_ForOtherOperations_PrintOrAnUnknownKey(string op)
    {
        var post = EditableGridForm.Read(GridForm(op, ("grid.selected", ["p1"])), Columns());
        Assert.Null(EditableGridForm.Batch(post, Approvals));
    }

    [Theory]
    [InlineData("save")]
    [InlineData("discard")]
    [InlineData("reset")]
    [InlineData("launch-rockets")]
    public void Apply_LeavesAppOperationsToTheCaller(string op)
    {
        var post = EditableGridForm.Read(GridForm(op), Columns());
        Assert.False(EditableGridForm.Apply(post, GridForm()));
    }
}

public class EditableGridTagHelperTests
{
    private static async Task<string> RenderAsync(EditableGridTagHelper helper, params (TagHelper Helper, string Tag, string Content)[] slots)
    {
        var items = new Dictionary<object, object>();
        var output = new TagHelperOutput("app-editable-grid", [], async (_, _) =>
        {
            foreach (var (slot, tag, content) in slots)
            {
                var slotOutput = new TagHelperOutput(tag, [], (_, _) => Task.FromResult<TagHelperContent>(new DefaultTagHelperContent().SetHtmlContent(content)));
                await slot.ProcessAsync(new TagHelperContext(tag, [], new Dictionary<object, object>(items), "s"), slotOutput);
            }
            return new DefaultTagHelperContent();
        });
        await helper.ProcessAsync(new TagHelperContext("app-editable-grid", [], items, "g"), output);
        using var writer = new StringWriter();
        output.WriteTo(writer, HtmlEncoder.Default);
        return writer.ToString();
    }

    private static EditableGridTagHelper Grid() => new()
    {
        Name = "projects",
        HeaderTitle = "Projects",
        Columns =
        [
            new() { Key = "name", Title = "Name", Required = true, Width = 200, Fixed = GridFixed.Left },
            new() { Key = "status", Title = "Status", Type = GridCellType.Select, Options = [new("a", "Active", "blue"), new("d", "Done")] },
            new() { Key = "budget", Title = "Budget", Type = GridCellType.Currency, Width = 140 },
            new() { Key = "notes", Title = "Notes", Hidden = true },
            new() { Key = "owner", Title = "Owner", Fixed = GridFixed.Right, Width = 160 },
        ],
        Rows =
        [
            new("p1", new Dictionary<string, string?> { ["name"] = "<Portal>", ["status"] = "a", ["budget"] = "1200", ["owner"] = "Ann" }),
            new("p2", new Dictionary<string, string?> { ["name"] = null, ["status"] = "d", ["budget"] = "-1", ["owner"] = "Bo" }),
        ],
    };

    [Fact]
    public async Task Renders_SchemaRowsAndEditors_ForAFullPost()
    {
        var html = await RenderAsync(Grid());

        Assert.Contains("class=\"m-resizable m-table-card m-grid ant-card\"", html);
        Assert.Contains("<span class=\"m-grid-dirty\" x-show=\"dirty\" style=\"display: none;\">", html);
        Assert.Contains("name=\"projects.schema\"", html);
        Assert.Contains("name=\"projects.index\" value=\"p1\"", html);
        Assert.Contains("name=\"projects.index\" value=\"p2\"", html);
        Assert.Contains("name=\"projects[p1].name\" value=\"&lt;Portal&gt;\"", html);
        Assert.Contains("name=\"projects[p1].budget\"", html);
        // Option cells edit with the creatable select (as Blazor's AppCreatableSelect): a hidden input posts the value.
        Assert.Contains("""<span class="m-cselect-inputs" x-html="inputs()"><input type="hidden" name="projects[p1].status" value="a" /></span>""", html);
        Assert.Contains("""<span class="ant-select-selection-item" title="Active"><span class="m-cselect-dot ant-tag-blue" aria-hidden="true"></span>Active</span>""", html);
        Assert.Contains("""@m-cselect-close="commit($el)" @m-cselect-add="submitOp($event.detail)">""", html);
        Assert.DoesNotContain("<select class=\"m-select\" name=\"projects[", html);
        Assert.Contains("Edit Status: Active", html);
        Assert.DoesNotContain("<Portal>", html);
    }

    [Fact]
    public async Task Renders_TheDataTableToolbar_SearchColumnsExportAndAFilterRow()
    {
        var grid = Grid();
        grid.Columns[1].Filterable = true;
        var html = await RenderAsync(grid);

        // Search, filters and selection stay in the browser: none of their inputs has a name.
        Assert.Contains("""<input class="ant-input" type="text" placeholder="Search projects…" aria-label="Search Projects" autocomplete="off" x-ref="search" x-model="keyword" @input.debounce.150ms="applyFilter()" @keydown.enter.prevent """, html);
        // The toolbar is icons with tooltips: the search folds into its icon, then the filter row, columns and add row.
        Assert.Contains("""title="Search (Ctrl+F)" data-m-search-toggle x-show="!searchShown()" @click="openSearch()">""", html);
        Assert.Contains("""aria-label="Filter row" title="Show the filter row" aria-pressed="false" data-m-filter-row>""", html);
        Assert.Contains("""aria-label="Add row" title="Add row (Insert)" data-m-grid-add @click.prevent="addRow()">""", html);
        Assert.Contains("""x-text="changesText()">Unsaved changes</span>""", html);
        Assert.Contains("""title="Save (Ctrl+S)" data-m-grid-save>""", html);
        // Each searchable header also has its box for the filter row (shown while the row is on).
        Assert.Contains("""class="ant-input ant-input-sm m-th-filter" type="text" value="" placeholder="Filter&#x2026;" aria-label="Filter Name" """.TrimEnd(), html);
        Assert.Contains("""placeholder="e.g. &gt;100 or 10..50" aria-label="Filter Budget" """.TrimEnd(), html);
        Assert.Contains("""<select class="m-select" aria-label="Status" data-filter="status" @change="setFilter('status', $el.value)"><option value="">Any status</option><option value="a">Active</option><option value="d">Done</option></select>""", html);
        // Columns and Export CSV are labelled buttons in the toolbar.
        Assert.Contains("""class="ant-btn ant-btn-default m-table-columns-toggle" aria-label="Columns" title="Show, hide, pin and order columns">""", html);
        Assert.Contains("<span>Columns</span></button>", html);
        Assert.Contains("""class="ant-btn ant-btn-default m-grid-export" title="Export the rows (after search and filters) to a CSV file" @click="exportCsv(false)">""", html);
        // An option column's box in the filter row is a select.
        Assert.Contains("""<select class="m-select m-th-filter" aria-label="Filter Status" x-ref="inline" @click.stop @change="applyInline()"><option value="">Any status</option><option value="=Active">Active</option><option value="=Done">Done</option></select>""", html);
        Assert.Contains("\"file\":\"Projects.csv\"", html.Replace("&quot;", "\""));
        Assert.Contains("""@m-col-resize="resized($event.detail)" """.TrimEnd(), html);
        Assert.Contains("x-ref=\"schema\"", html);
    }

    [Fact]
    public async Task Renders_SelectionAndTheBatchBar()
    {
        var html = await RenderAsync(Grid());

        // The selection column follows the drag handle column (40px).
        Assert.Contains("""<th class="ant-table-cell ant-table-selection-column ant-table-cell-fix-left" style="position: sticky; left: 40px;"><label class="ant-checkbox-wrapper"><span class="ant-checkbox"><input type="checkbox" class="ant-checkbox-input" aria-label="Select all rows" data-select-all @change="selectAll($el.checked)" />""", html);
        Assert.Contains("""aria-label="Select row" data-select-row @change="selectRow($el)" />""", html);
        // The first pinned column starts after the handle and selection columns.
        Assert.Contains("position: sticky; left: 88px;", html);
        Assert.Contains("""<div class="ant-alert ant-alert-info ant-alert-no-icon m-table-alert m-grid-batch" role="status" x-show="selected > 0" style="display: none;">""", html);
        Assert.Contains("""<option value="status=a">Active</option>""", html);
        Assert.Contains("""@click="deleteSelected()">""", html);
        Assert.Contains("""@click="exportCsv(true)">""", html);
    }

    [Fact]
    public async Task Renders_ADragHandlePerRow_AndLocksItWhileTheServerSorts()
    {
        var html = await RenderAsync(Grid());

        Assert.Contains("""data-m-sort-rows="grid-g" data-m-sort-root="" data-m-sort-title="Projects" """.TrimEnd(), html);
        Assert.Contains("""<span class="m-sort-live" id="grid-g-drag-help">""", html);
        Assert.Contains("""<col style="width: 40px;">""", html);
        // The row's first cell holds its index input and the handle, named after the first column.
        Assert.Contains("""<td class="ant-table-cell m-row-drag-cell ant-table-cell-fix-left" style="position: sticky; left: 0px;"><input type="hidden" name="projects.index" value="p1" />""", html);
        Assert.Contains("""data-m-sort-row="p1" data-m-sort-label="&lt;Portal&gt;" tabindex="0" title="Drag to reorder" role="button" aria-label="Move &lt;Portal&gt;" """.TrimEnd(), html);
        Assert.Contains("\"dragLocked\":false", html.Replace("&quot;", "\""));

        var sorted = Grid();
        sorted.SortHref = key => $"/level?sort={key}";
        sorted.SortField = "budget";
        var locked = await RenderAsync(sorted);
        Assert.Contains("data-m-sort-disabled", locked);
        Assert.Contains("""data-m-sort-row="p1" data-m-sort-label="&lt;Portal&gt;" tabindex="-1" aria-disabled="true" """.TrimEnd(), locked);

        var off = Grid();
        off.RowDraggable = false;
        var plain = await RenderAsync(off);
        Assert.DoesNotContain("data-m-sort-row", plain);
        Assert.DoesNotContain("m-row-drag-cell", plain);
    }

    [Fact]
    public async Task Renders_BatchButtons_AndEachRowsBatchKeys()
    {
        var grid = Grid();
        grid.Rows[1].State = GridRowState.Locked;
        grid.Batch =
        [
            new() { Key = "approve", Label = "Approve", Icon = "check", Primary = true, Confirm = true, When = r => r["status"] == "a" },
            new() { Key = "reject", Label = "Reject", Danger = true },
            GridBatchAction.Printing(),
        ];
        var html = await RenderAsync(grid);

        // Rows list the actions they take (none for a locked row).
        Assert.Contains("""data-m-batch="approve reject" """.TrimEnd(), html);
        Assert.Contains("""data-m-batch="" """.TrimEnd(), html);
        Assert.Contains("""class="ant-btn ant-btn-primary ant-btn-sm m-grid-batch-action" data-m-grid-batch="approve" """.TrimEnd(), html);
        Assert.Contains("""class="ant-btn ant-btn-default ant-btn-dangerous ant-btn-sm m-grid-batch-action" data-m-grid-batch="reject" """.TrimEnd(), html);
        Assert.Contains("x-text=\"batchCount('approve', selected)\"", html);
        Assert.Contains("@click=\"runBatch('print')\"", html);
        Assert.DoesNotContain("batchCount('print'", html);
        var config = html.Replace("&quot;", "\"");
        Assert.Contains("\"title\":\"Projects\"", config);
        Assert.Contains("\"approve\":{\"label\":\"Approve\",\"confirm\":true,\"danger\":false,\"print\":false}", config);
        Assert.DoesNotContain("data-m-batch=", await RenderAsync(Grid()));
    }

    [Fact]
    public async Task Renders_RowStates_AndRowErrors()
    {
        var grid = Grid();
        grid.Rows[0].State = GridRowState.Locked;
        grid.Rows[0].StateReason = "Invoiced";
        grid.Rows[1].State = GridRowState.ReadOnly;
        grid.Errors = [GridError.ForRow("p2", "Due is before the start.")];
        var html = await RenderAsync(grid);

        // Rows say their state; the cells are plain (no editors), and still post their values.
        Assert.Contains("""<tr data-row-key="p1" class="ant-table-row ant-table-row-level-0 m-grid-row-locked" data-m-row-state="locked" title="Locked: Invoiced">""", html);
        Assert.Contains("""<tr data-row-key="p2" class="ant-table-row ant-table-row-level-0 m-grid-row-readonly m-grid-row-error" data-m-row-state="readonly" title="Read-only">""", html);
        // (The new-row <template> stays editable, so only the rendered rows are checked.)
        var rows = html.Split("<template")[0];
        Assert.DoesNotContain("Edit Name: &lt;Portal&gt;", rows);
        Assert.DoesNotContain("m-grid-cell-editing", rows);
        Assert.Contains("""<input type="hidden" name="projects[p1].budget" value="1200" />""", html);
        // The first cell carries the mark (read out) and the row's error; empty cells say "—", not "Add".
        Assert.Contains("""<span class="m-grid-row-mark">""", html);
        Assert.Contains("""<span class="m-sr-only">Locked: Invoiced</span></span><span>&lt;Portal&gt;</span></div>""", html);
        Assert.Contains("""<div class="m-grid-cell-message m-grid-row-message" role="alert">Due is before the start.</div>""", html);
        Assert.Contains("""<span class="m-grid-empty">—</span>""", html);
        Assert.Contains("1 row needs attention before saving.", html);
        // A locked row's handle and delete button are disabled; the read-only row keeps both.
        Assert.Contains("""data-m-sort-row="p1" data-m-sort-label="&lt;Portal&gt;" tabindex="-1" aria-disabled="true" title="Locked: Invoiced" """.TrimEnd(), html);
        Assert.Contains("""disabled aria-label="Delete row" title="Locked: Invoiced">""", html);
        Assert.Contains("""value="delete-row:p2" """.TrimEnd(), html);
        Assert.DoesNotContain("""value="delete-row:p1" """.TrimEnd(), html);
    }

    [Fact]
    public async Task Renders_AvatarsSubtitlesAndOptionIcons_AndCanTurnTheExtrasOff()
    {
        var grid = Grid();
        grid.Columns[0].SubtitleKey = "notes";
        grid.Columns[1] = new() { Key = "status", Title = "Status", Type = GridCellType.Select, Avatar = true, Options = [new("a", "Active Team", null, "fire"), new("d", "Done")] };
        grid.Rows[0]["notes"] = "First & best";
        var html = await RenderAsync(grid);

        Assert.Contains($"""<span class="m-avatar m-grid-avatar m-av-{GridSchema.Tint("Active Team")}" aria-hidden="true">AT</span>""", html);
        Assert.Contains("anticon-fire", html);
        Assert.Contains("""<span class="m-grid-entity-desc">First &amp; best</span>""", html);

        grid.Searchable = grid.Selectable = grid.Exportable = grid.Resizable = grid.AllowColumnHide = false;
        grid.AllowColumnEdit = false;
        var plain = await RenderAsync(grid);
        Assert.DoesNotContain("m-grid-search", plain);
        Assert.DoesNotContain("ant-table-selection-column", plain);
        Assert.DoesNotContain("m-grid-batch", plain);
        Assert.DoesNotContain("exportCsv", plain);
        Assert.DoesNotContain("m-resizable", plain);
        Assert.DoesNotContain("""aria-label="Columns" """.TrimEnd(), plain);
    }

    [Fact]
    public async Task Renders_AnAddOptionRowUnderSelectEditors()
    {
        var html = await RenderAsync(Grid());

        // "Create new status" posts the name and colour with add-option:{key}:{row}, so the option joins the schema.
        Assert.Contains("<span>Create new status</span>", html);
        Assert.Contains("""type="text" name="projects.option[p1].status" x-ref="add" x-model="newLabel" placeholder="New option" """.TrimEnd(), html);
        Assert.Contains("""<input type="hidden" name="projects.color[p1].status" :value="color()" />""", html);
        Assert.Contains("gridOp\":\"add-option:status:p2\"", html.Replace("&quot;", "\""));
        Assert.DoesNotContain("projects.option[p1].budget", html);
    }

    [Fact]
    public async Task Renders_DateCells_WithTheAntDesignPicker()
    {
        var grid = Grid();
        grid.Columns[2] = new() { Key = "due", Title = "Due", Type = GridCellType.Date };
        grid.Rows[0]["due"] = "2026-09-28";
        var html = await RenderAsync(grid);

        Assert.Contains("""<div class="ant-picker m-picker" style="width: 100%;" x-data="modulusPicker(""", html);
        Assert.Contains("""<input type="hidden" name="projects[p1].due" value="2026-09-28" data-m-picker-value="0" />""", html);
        Assert.Contains("""@m-picker-pick="commit($el)">""", html);
        Assert.DoesNotContain("type=\"date\"", html);
    }

    [Fact]
    public async Task Renders_PinnedColumnsSticky_AndSkipsHiddenOnes()
    {
        var html = await RenderAsync(Grid());

        Assert.Contains("data-key=\"name\"", html);
        Assert.Contains("ant-table-cell-fix-left", html);
        Assert.Contains("ant-table-cell-fix-right ant-table-cell-fix-right-first", html);
        Assert.Contains("position: sticky; right: 64px", html);
        Assert.DoesNotContain("th class=\"m-grid-th ant-table-cell\" data-key=\"notes\"", html);
        Assert.DoesNotContain("data-key=\"notes\"", html.Split("<table")[1]);
        // Hidden columns still post their values so nothing is lost.
        Assert.Contains("name=\"projects[p1].notes\"", html);
    }

    [Fact]
    public async Task Renders_ErrorsDirtyStateAndSlots()
    {
        var grid = Grid();
        grid.Dirty = true;
        grid.Columns[2].Min = 0;
        grid.Errors = GridSchema.Validate(grid.Columns, grid.Rows);
        grid.RowDetail = r => new HtmlString($"<p class=\"detail\">{r.Id}</p>");

        var html = await RenderAsync(grid,
            (new GridTitleExtraTagHelper(), "grid-title-extra", "<span class=\"count\">2</span>"),
            (new GridSummaryTagHelper(), "grid-summary", "<span class=\"total\">Total</span>"));

        Assert.Contains("2 cells need attention before saving.", html);
        Assert.Contains("Name is required.", html);
        Assert.Contains("Budget must be at least 0.", html);
        Assert.Contains("m-grid-cell-error", html);
        Assert.Contains("<span class=\"m-grid-dirty\" x-show=\"dirty\">", html);
        Assert.Contains("<span class=\"count\">2</span>", html);
        Assert.Contains("<span class=\"total\">Total</span>", html);
        Assert.Contains("data-detail-for=\"p1\"", html);
        Assert.Contains("<p class=\"detail\">p2</p>", html);
        Assert.Contains("ant-table-row-expand-icon", html);
    }

    [Fact]
    public async Task Renders_HeaderBands_OverTheShownColumns()
    {
        var grid = Grid();
        // Notes is hidden, so Plan covers Status and Budget only.
        grid.HeaderGroups = [new HeaderGroup("Plan", "status", "budget", "notes")];
        var html = await RenderAsync(grid);

        // Drag handle + checkbox + Name, Plan, Owner + delete.
        Assert.Contains("""<thead class="ant-table-thead"><tr class="m-header-bands"><td class="ant-table-cell m-header-band m-header-band-empty" colspan="3" data-m-span="3"></td>"""
            + """<th class="ant-table-cell m-header-band" scope="colgroup" colspan="2" data-m-span="2">Plan</th>"""
            + """<td class="ant-table-cell m-header-band m-header-band-empty" colspan="2" data-m-span="2"></td></tr><tr>""", html);
        Assert.DoesNotContain("m-header-bands", await RenderAsync(Grid()));
    }
}
