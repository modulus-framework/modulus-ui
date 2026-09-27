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
        Assert.Contains("<option value=\"a\" selected", html);
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
        Assert.Contains("""<input class="ant-input" type="text" placeholder="Search projects…" aria-label="Search Projects" autocomplete="off" x-model="keyword" @input.debounce.150ms="applyFilter()" @keydown.enter.prevent />""", html);
        Assert.Contains("""<select class="m-select" aria-label="Status" data-filter="status" @change="setFilter('status', $el.value)"><option value="">Any status</option><option value="a">Active</option><option value="d">Done</option></select>""", html);
        Assert.Contains("<span>Columns</span>", html);
        Assert.Contains("""@click="exportCsv(false)">""", html);
        Assert.Contains("\"file\":\"Projects.csv\"", html.Replace("&quot;", "\""));
        Assert.Contains("""@m-col-resize="resized($event.detail)" """.TrimEnd(), html);
        Assert.Contains("x-ref=\"schema\"", html);
    }

    [Fact]
    public async Task Renders_SelectionAndTheBatchBar()
    {
        var html = await RenderAsync(Grid());

        Assert.Contains("""<th class="ant-table-cell ant-table-selection-column ant-table-cell-fix-left" style="position: sticky; left: 0px;"><label class="ant-checkbox-wrapper"><span class="ant-checkbox"><input type="checkbox" class="ant-checkbox-input" aria-label="Select all rows" data-select-all @change="selectAll($el.checked)" />""", html);
        Assert.Contains("""aria-label="Select row" data-select-row @change="selectRow($el)" />""", html);
        // The first pinned column starts after the selection column.
        Assert.Contains("position: sticky; left: 48px;", html);
        Assert.Contains("""<div class="ant-alert ant-alert-info ant-alert-no-icon m-table-alert m-grid-batch" role="status" x-show="selected > 0" style="display: none;">""", html);
        Assert.Contains("""<option value="status=a">Active</option>""", html);
        Assert.Contains("""@click="deleteSelected()">""", html);
        Assert.Contains("""@click="exportCsv(true)">""", html);
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
        Assert.DoesNotContain("<span>Columns</span>", plain);
    }

    [Fact]
    public async Task Renders_AnAddOptionRowUnderSelectEditors()
    {
        var html = await RenderAsync(Grid());

        Assert.Contains("""<input type="text" class="ant-input ant-input-sm" name="projects.option[p1].status" maxlength="60" placeholder="New option" aria-label="New status option" @keydown.enter.stop.prevent="submitOp('add-option:status:p1')" />""", html);
        Assert.Contains("""@mousedown.prevent @click="submitOp('add-option:status:p2')">""", html);
        // The next colour in turn is picked: two options, so the third colour.
        Assert.Contains("""<input type="radio" class="m-sr-only" name="projects.color[p1].status" value="orange" checked />""", html);
        Assert.DoesNotContain("projects.option[p1].budget", html);
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
}
