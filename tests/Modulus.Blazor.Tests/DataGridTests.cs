using System.Globalization;
using AngleSharp.Dom;
using AntDesign;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions;
using Xunit;

namespace Modulus.Blazor.Tests;

public class GridSchemaTests
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    private static List<GridColumn> Columns() =>
    [
        new() { Key = "name", Title = "Name", Required = true, Width = 200, Fixed = GridFixed.Left },
        new() { Key = "budget", Title = "Budget", Type = GridCellType.Currency, Min = 0, Width = 140 },
        new() { Key = "status", Title = "Status", Type = GridCellType.Select, Options = [new("a", "Active", "blue"), new("d", "Done", "green")] },
        new() { Key = "teams", Title = "Teams", Type = GridCellType.MultiSelect, Options = [new("web", "Web"), new("data", "Data")] },
        new() { Key = "due", Title = "Due", Type = GridCellType.Date },
        new() { Key = "billable", Title = "Billable", Type = GridCellType.Switch, Required = true },
        new() { Key = "owner", Title = "Owner", Fixed = GridFixed.Right, Width = 160 },
    ];

    [Fact]
    public void Matches_SearchesDisplayedTextAndSubtitles_AndAppliesEveryFilter()
    {
        var columns = Columns();
        columns[0].SubtitleKey = "email";
        var row = new GridRecord("r", new Dictionary<string, string?>
        {
            ["name"] = "Portal", ["email"] = "ann@example.com", ["status"] = "a", ["teams"] = "web,data", ["billable"] = "true", ["budget"] = "1250",
        });

        Assert.True(GridSchema.Matches(columns, row, "active", culture: Us));   // the option label, not the value
        Assert.True(GridSchema.Matches(columns, row, "$1,250", culture: Us));  // formatted money
        Assert.True(GridSchema.Matches(columns, row, "EXAMPLE", culture: Us)); // the subtitle
        Assert.False(GridSchema.Matches(columns, row, "done", culture: Us));

        Assert.True(GridSchema.Matches(columns, row, null, new Dictionary<string, string?> { ["teams"] = "data", ["billable"] = "true", ["status"] = "" }));
        Assert.False(GridSchema.Matches(columns, row, null, new Dictionary<string, string?> { ["billable"] = "false" }));
        Assert.False(GridSchema.Matches(columns, row, null, new Dictionary<string, string?> { ["status"] = "d" }));
        Assert.True(GridSchema.Matches(columns, row, null, new Dictionary<string, string?> { ["unknown"] = "x" }));

        columns[2].Hidden = true; // hidden columns are not searched
        Assert.False(GridSchema.Matches(columns, row, "active", culture: Us));
    }

    [Fact]
    public void FilterColumns_KeepFilterableOptionAndSwitchColumns()
    {
        var columns = Columns();
        foreach (var c in columns) c.Filterable = true;

        Assert.Equal(["status", "teams", "billable"], GridSchema.FilterColumns(columns).Select(c => c.Key));
        Assert.Equal(["Yes", "No"], GridSchema.FilterOptions(columns[5]).Select(o => o.Label));
        Assert.Equal(["a", "d"], GridSchema.FilterOptions(columns[2]).Select(o => o.Value));
    }

    [Fact]
    public void InitialsAndTint_AreStable()
    {
        Assert.Equal("OB", GridSchema.Initials("Olivia  Bennett Jr"));
        Assert.Equal("A", GridSchema.Initials("Adatum"));
        Assert.Equal("", GridSchema.Initials(null));
        Assert.Equal(GridSchema.Tint("Olivia Bennett"), GridSchema.Tint("Olivia Bennett"));
        Assert.All(new[] { "", "a", "Olivia Bennett", new string('z', 500) }, t => Assert.InRange(GridSchema.Tint(t), 1, 8));
    }

    [Fact]
    public void Csv_KeepsNumbersAndDatesInvariant_LabelsOptions_AndQuotes()
    {
        var columns = Columns();
        var rows = new[]
        {
            new GridRecord("r1", new Dictionary<string, string?> { ["name"] = "Portal, \"v2\"", ["budget"] = "1250.5", ["status"] = "a", ["teams"] = "web,data", ["due"] = "2026-03-01", ["billable"] = "true" }),
            new GridRecord("r2", new Dictionary<string, string?> { ["name"] = "Line\nbreak", ["status"] = "gone" }),
        };

        var lines = GridSchema.Csv(columns, rows).Split("\r\n");
        Assert.Equal("Name,Budget,Status,Teams,Due,Billable,Owner", lines[0]);
        Assert.Equal("\"Portal, \"\"v2\"\"\",1250.5,Active,Web; Data,2026-03-01,Yes,", lines[1]);
        Assert.Equal("\"Line\nbreak\",,gone,,,No,", lines[2]);
    }

    [Fact]
    public void Deserialize_DropsAMalformedSubtitleKey()
    {
        var columns = Columns();
        columns[0].SubtitleKey = "email";
        columns[1].SubtitleKey = "../x";
        var back = GridSchema.Deserialize(GridSchema.Serialize(columns))!;
        Assert.Equal(("email", null), (back[0].SubtitleKey, back[1].SubtitleKey));
    }

    [Fact]
    public void Visible_PutsLeftPinnedFirst_RightPinnedLast_AndSkipsHidden()
    {
        var columns = Columns();
        columns[2].Hidden = true;
        GridSchema.Pin(columns, "due", GridFixed.Left);

        Assert.Equal(["name", "due", "budget", "teams", "billable", "owner"], GridSchema.Visible(columns).Select(c => c.Key));
    }

    [Fact]
    public void StickyOffsets_AccumulateFromEachEdge()
    {
        var columns = Columns();
        GridSchema.Pin(columns, "budget", GridFixed.Left);
        GridSchema.Pin(columns, "billable", GridFixed.Right);

        var offsets = GridSchema.StickyOffsets(GridSchema.Visible(columns), leadingWidth: 48);

        Assert.Equal(48, offsets["name"]);
        Assert.Equal(248, offsets["budget"]);
        Assert.Equal(0, offsets["owner"]);
        Assert.Equal(160, offsets["billable"]);
        Assert.False(offsets.ContainsKey("teams"));
    }

    [Fact]
    public void SetHidden_KeepsOneColumnVisible_AndMoveRemoveStayInBounds()
    {
        var columns = Columns().Take(2).ToList();
        GridSchema.SetHidden(columns, "name", true);
        GridSchema.SetHidden(columns, "budget", true);
        Assert.Equal([true, false], columns.Select(c => c.Hidden));

        GridSchema.Move(columns, "name", -1);
        Assert.Equal("name", columns[0].Key);
        GridSchema.Move(columns, "name", 1);
        Assert.Equal(["budget", "name"], columns.Select(c => c.Key));

        var rows = new List<GridRecord> { new("r1", new Dictionary<string, string?> { ["name"] = "A", ["budget"] = "1" }) };
        GridSchema.Remove(columns, rows, "budget");
        Assert.Equal(["name"], columns.Select(c => c.Key));
        Assert.False(rows[0].Values.ContainsKey("budget"));
        GridSchema.Remove(columns, rows, "name");
        Assert.Single(columns);
    }

    [Fact]
    public void Add_DerivesUniqueCamelCaseKey_ParsesOptions_AndRejectsBlankTitles()
    {
        var columns = Columns();

        var region = GridSchema.Add(columns, "  Sales region ", GridCellType.Select, "EMEA, Americas\nAPAC, emea", GridFixed.Right, required: true);
        var again = GridSchema.Add(columns, "Sales Region!", GridCellType.Text);
        var numeric = GridSchema.Add(columns, "2026", GridCellType.Number);
        var tags = GridSchema.Add(columns, "Labels", GridCellType.Tag);

        Assert.NotNull(region);
        Assert.Equal("salesRegion", region.Key);
        Assert.Equal(["EMEA", "Americas", "APAC"], region.Options.Select(o => o.Label));
        Assert.Equal(["emea", "americas", "apac"], region.Options.Select(o => o.Value));
        Assert.All(region.Options, o => Assert.NotNull(o.Color));
        Assert.Equal((GridFixed.Right, true, 180), (region.Fixed, region.Required, region.Width));
        Assert.Equal("salesRegion2", again!.Key);
        Assert.Equal("col2026", numeric!.Key);
        Assert.Equal(2, tags!.Options.Count);
        Assert.Null(GridSchema.Add(columns, "   ", GridCellType.Text));
    }

    [Fact]
    public void Add_StopsAtMaxColumns()
    {
        var columns = Enumerable.Range(0, GridSchema.MaxColumns).Select(i => new GridColumn { Key = "c" + i, Title = "C" + i }).ToList();
        Assert.Null(GridSchema.Add(columns, "One more", GridCellType.Text));
    }

    [Fact]
    public void Format_UsesCultureForNumbersAndDates_AndLabelsForOptions()
    {
        var c = Columns();
        Assert.Equal("$125,000", GridSchema.Format(c[1], "125000", Us));
        Assert.Equal("$1,250.50", GridSchema.Format(c[1], "1250.5", Us));
        Assert.Equal("125.000 €", GridSchema.Format(c[1], "125000", CultureInfo.GetCultureInfo("de-DE")).Replace((char)0xA0, ' '));
        Assert.Equal("Done", GridSchema.Format(c[2], "d", Us));
        Assert.Equal("Web, Data", GridSchema.Format(c[3], "web,data", Us));
        Assert.Equal("12/31/2026", GridSchema.Format(c[4], "2026-12-31", Us));
        Assert.Equal("31.12.2026", GridSchema.Format(c[4], "2026-12-31", CultureInfo.GetCultureInfo("de-DE")));
        Assert.Equal("Yes", GridSchema.Format(c[5], "true", Us));
        Assert.Equal("No", GridSchema.Format(c[5], null, Us));
        Assert.Equal("", GridSchema.Format(c[0], null, Us));
        Assert.Equal("3 / 5", GridSchema.Format(new GridColumn { Type = GridCellType.Rating }, "3", Us));
        Assert.Equal("40%", GridSchema.Format(new GridColumn { Type = GridCellType.Percent }, "40", Us));
    }

    [Fact]
    public void Normalize_WritesInvariantValues_AndKeepsBadInputForValidation()
    {
        var c = Columns();
        Assert.Equal("1250.5", GridSchema.Normalize(c[1], " 1250.50 "));
        Assert.Equal("abc", GridSchema.Normalize(c[1], "abc"));
        Assert.Null(GridSchema.Normalize(c[0], "   "));
        Assert.Equal("web", GridSchema.Normalize(c[3], "web,bogus,web"));
        Assert.Equal("true", GridSchema.Normalize(c[5], "on"));
        Assert.Equal("false", GridSchema.Normalize(c[5], null));
    }

    [Fact]
    public void Validate_ReportsRequiredNumberRangeDateAndOptionErrors()
    {
        var rows = new List<GridRecord>
        {
            new("ok", new Dictionary<string, string?> { ["name"] = "A", ["budget"] = "5", ["status"] = "a", ["teams"] = "web", ["due"] = "2026-01-02", ["billable"] = "false" }),
            new("bad", new Dictionary<string, string?> { ["name"] = null, ["budget"] = "-1", ["status"] = "x", ["teams"] = "zzz", ["due"] = "31/12/2026" }),
            new("nan", new Dictionary<string, string?> { ["name"] = "B", ["budget"] = "ten" }),
        };

        var errors = GridSchema.Validate(Columns(), rows);

        Assert.DoesNotContain(errors, e => e.RowId == "ok");
        Assert.Equal(
        [
            ("bad", "name", "Name is required."),
            ("bad", "budget", "Budget must be at least 0."),
            ("bad", "status", "Choose a valid status."),
            ("bad", "teams", "Choose valid teams."),
            ("bad", "due", "Due must be a date."),
            ("nan", "budget", "Budget must be a number."),
        ], errors.Select(e => (e.RowId, e.Key, e.Message)));
    }

    [Fact]
    public void SerializeDeserialize_RoundTrips_AndRejectsTamperedSchemas()
    {
        var columns = Columns();
        var back = GridSchema.Deserialize(GridSchema.Serialize(columns));

        Assert.NotNull(back);
        Assert.Equal(columns.Select(c => (c.Key, c.Type, c.Fixed, c.Required)), back.Select(c => (c.Key, c.Type, c.Fixed, c.Required)));
        Assert.Equal(["a", "d"], back[2].Options.Select(o => o.Value));
        Assert.Contains("\"currency\"", GridSchema.Serialize(columns), StringComparison.OrdinalIgnoreCase);

        Assert.Null(GridSchema.Deserialize(null));
        Assert.Null(GridSchema.Deserialize("{not json"));
        Assert.Null(GridSchema.Deserialize("[]"));
        Assert.Null(GridSchema.Deserialize("""[{"key":"a b"}]"""));
        Assert.Null(GridSchema.Deserialize("""[{"key":"a"},{"key":"a"}]"""));
        var clamped = GridSchema.Deserialize("""[{"key":"a","width":5000}]""");
        Assert.Equal((600, "a"), (clamped![0].Width!.Value, clamped[0].Title));
    }

    [Fact]
    public void NewRow_PicksAFreeId_AndDefaultsSwitchesToFalse()
    {
        var rows = new List<GridRecord> { new("n1"), new("n3") };
        var row = GridSchema.NewRow(Columns(), rows);

        Assert.Equal("n4", row.Id);
        Assert.Equal("false", row["billable"]);
        Assert.Null(row["name"]);
    }
}

public class EditableGridTests : BunitContext
{
    public EditableGridTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        // Alert measures itself after render; loose interop would hand it a null element.
        JSInterop.Setup<AntDesign.JsInterop.HtmlElement>(_ => true).SetResult(new AntDesign.JsInterop.HtmlElement());
        // Select measures its trigger to size the dropdown.
        JSInterop.Setup<AntDesign.JsInterop.DomRect>(_ => true).SetResult(new AntDesign.JsInterop.DomRect());
        Services.AddAntDesign();
    }

    private static List<GridColumn> Columns() =>
    [
        new() { Key = "name", Title = "Name", Required = true, Width = 200, Fixed = GridFixed.Left },
        new() { Key = "budget", Title = "Budget", Type = GridCellType.Currency, Min = 0 },
        new() { Key = "billable", Title = "Billable", Type = GridCellType.Switch },
        new() { Key = "owner", Title = "Owner", Fixed = GridFixed.Right, Width = 160 },
    ];

    private static List<GridRecord> Rows() =>
    [
        new("r1", new Dictionary<string, string?> { ["name"] = "Portal", ["budget"] = "1200", ["billable"] = "true", ["owner"] = "Ann" }),
        new("r2", new Dictionary<string, string?> { ["name"] = "Mobile", ["budget"] = "800", ["billable"] = "false", ["owner"] = "Bo" }),
    ];

    [Fact]
    public void RendersColumnsFromSchema_WithPinnedCellsAndRequiredMarks()
    {
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, Columns()).Add(g => g.Rows, Rows()).Add(g => g.HeaderTitle, "Projects"));

        Assert.Equal(2, cut.FindAll("tbody tr.ant-table-row").Count);
        var name = cut.Find("[aria-label^='Edit Name:']").Closest("td")!;
        Assert.Contains("ant-table-cell-fix-left", name.ClassList);
        Assert.Contains("ant-table-cell-fix-right", cut.Find("[aria-label^='Edit Owner:']").Closest("td")!.ClassList);
        Assert.Contains("Edit Budget: $1,200", cut.Find("[aria-label^='Edit Budget:']").GetAttribute("aria-label"));
        Assert.Single(cut.FindAll("thead .m-grid-required"));
        Assert.Empty(cut.FindAll(".m-grid-dirty"));
    }

    [Fact]
    public async Task AddRow_InsertsAnEditableRowAtTheTop_AndMarksDirty()
    {
        var rows = Rows();
        var changes = 0;
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, Columns()).Add(g => g.Rows, rows).Add(g => g.OnChange, () => changes++));

        // AntD's Button runs OnClick after an await, so the click has to be awaited.
        await cut.FindAll("button").First(b => b.TextContent.Contains("Add row")).ClickAsync(new());

        Assert.Equal(3, rows.Count);
        Assert.Equal("n3", rows[0].Id);
        Assert.Equal(1, changes);
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("tbody tr.ant-table-row").Count));
        Assert.NotEmpty(cut.FindAll(".m-grid-dirty"));
        Assert.NotEmpty(cut.FindAll(".m-grid-cell-editing input"));
    }

    [Fact]
    public async Task Save_BlocksOnValidationErrors_ThenCallsOnSaveWhenValid()
    {
        var rows = Rows();
        rows[1]["name"] = null;
        rows[1]["budget"] = "-3";
        var saved = 0;
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, Columns()).Add(g => g.Rows, rows).Add(g => g.OnSave, () => saved++));
        IElement Save() => cut.FindAll("button").First(b => b.TextContent.Trim() == "Save");

        await Save().ClickAsync(new());

        Assert.Equal(0, saved);
        Assert.Contains("2 cells need attention", cut.Find(".ant-alert").TextContent);
        Assert.Equal(["Name is required.", "Budget must be at least 0."], cut.FindAll(".m-grid-cell-message").Select(e => e.TextContent));

        rows[1]["name"] = "Mobile";
        rows[1]["budget"] = "3";
        await Save().ClickAsync(new());

        Assert.Equal(1, saved);
        Assert.Empty(cut.FindAll(".ant-alert"));
    }

    private static List<GridColumn> RichColumns() =>
    [
        new() { Key = "name", Title = "Name", SubtitleKey = "email" },
        new() { Key = "owner", Title = "Owner", Type = GridCellType.Select, Avatar = true, Filterable = true, Options = [new("ann", "Ann Lee"), new("bo", "Bo Park")] },
        new() { Key = "priority", Title = "Priority", Type = GridCellType.Select, Filterable = true, Options = [new("low", "Low", null, "arrow-down"), new("high", "High", null, "fire")] },
        new() { Key = "email", Title = "Email", Hidden = true },
    ];

    private static List<GridRecord> RichRows() =>
    [
        new("r1", new Dictionary<string, string?> { ["name"] = "Portal", ["owner"] = "ann", ["priority"] = "high", ["email"] = "portal@example.com" }),
        new("r2", new Dictionary<string, string?> { ["name"] = "Mobile", ["owner"] = "bo", ["priority"] = "low" }),
    ];

    [Fact]
    public void Toolbar_LooksLikeTheDataTable_SearchColumnsExportFiltersAndSelection()
    {
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, RichColumns()).Add(g => g.Rows, RichRows()).Add(g => g.HeaderTitle, "Projects"));

        Assert.Contains("m-resizable", cut.Find(".m-grid").ClassList);
        Assert.Equal("Search projects…", cut.Find(".m-grid-search input").GetAttribute("placeholder"));
        Assert.Contains(cut.FindAll(".m-table-toolbar-actions button"), b => b.TextContent.Trim() == "Columns");
        Assert.Contains(cut.FindAll(".m-table-toolbar-actions button"), b => b.TextContent.Trim() == "Export CSV");
        Assert.Equal(2, cut.FindAll(".m-table-filters .ant-select").Count); // owner, priority
        Assert.NotNull(cut.Find("thead th.ant-table-selection-column"));
        Assert.Equal(2, cut.FindAll("tbody td.ant-table-selection-column").Count);
        Assert.Equal("owner", cut.FindAll("thead .m-grid-th")[1].GetAttribute("data-key"));
    }

    [Fact]
    public void Cells_ShowAvatarsSubtitlesAndOptionIcons()
    {
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, RichColumns()).Add(g => g.Rows, RichRows()));

        var owner = cut.Find("[aria-label='Edit Owner: Ann Lee'] .m-grid-entity");
        Assert.Equal("AL", owner.QuerySelector(".m-avatar")!.TextContent);
        Assert.Contains($"m-av-{GridSchema.Tint("Ann Lee")}", owner.QuerySelector(".m-avatar")!.ClassName);
        Assert.Equal("portal@example.com", cut.Find("[aria-label='Edit Name: Portal'] .m-grid-entity-desc").TextContent);
        Assert.NotNull(cut.Find("[aria-label='Edit Priority: High'] .m-grid-icon-text .anticon-fire"));
    }

    [Fact]
    public void ProTable_RowDetailAddsAnExpandColumn_AndIsResizable()
    {
        var cut = Render<ProTable<GridRecord>>(p => p
            .Add(t => t.Request, _ => Task.FromResult(new ProTableResponse<GridRecord> { Data = RichRows(), Total = 2 }))
            .Add(t => t.RowKey, r => r.Id)
            .Add(t => t.RowDetail, r => b => b.AddMarkupContent(0, $"<p class=\"detail\">{r.Id}</p>"))
            .Add(t => t.Columns, r => b =>
            {
                b.OpenComponent<PropertyColumn<GridRecord, string>>(0);
                b.AddAttribute(1, "Property", (System.Linq.Expressions.Expression<Func<GridRecord, string>>)(x => x.Id));
                b.CloseComponent();
            }));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tbody .ant-table-row-expand-icon").Count));
        Assert.Contains("m-resizable", cut.Find(".m-table-card").ClassList);
    }

    [Fact]
    public void SelectingRows_ShowsTheBatchBar_AndSetValueUpdatesEverySelectedRow()
    {
        var rows = RichRows();
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, RichColumns()).Add(g => g.Rows, rows));
        Assert.Empty(cut.FindAll(".m-grid-batch"));

        // Re-find the boxes each time: every change renders the rows again.
        for (var i = 0; i < 2; i++) cut.FindAll("tbody td.ant-table-selection-column input")[i].Change(true);

        cut.WaitForAssertion(() => Assert.Contains("Selected 2 rows", cut.Find(".m-grid-batch").TextContent));
        Assert.Equal(["Owner", "Priority"], cut.FindAll(".m-grid-batch-set optgroup").Select(g => g.GetAttribute("label")));
        cut.Find(".m-grid-batch-set").Change("priority=high");

        cut.WaitForAssertion(() => Assert.Equal(["high", "high"], rows.Select(r => $"{r["priority"]}")));
        Assert.NotEmpty(cut.FindAll(".m-grid-dirty"));
    }
}
