using System.Globalization;
using AngleSharp.Dom;
using AntDesign;
using Bunit;
using Microsoft.AspNetCore.Components;
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
    public void RowStates_ValidateSkipsThem_TheRowRuleRunsOnValidRows_AndProtectKeepsAPostToTheStore()
    {
        var columns = Columns()[..2];
        GridRecord Row(string id, string? name, string budget, GridRowState state = GridRowState.Editable) =>
            new(id, new Dictionary<string, string?> { ["name"] = name, ["budget"] = budget }) { State = state, StateReason = state == GridRowState.Editable ? null : "Posted" };
        string? Rule(GridRecord r) => GridSchema.Number(r["budget"]) > 100 ? "Over budget." : null;

        var errors = GridSchema.Validate(columns, [Row("a", null, "500"), Row("b", "B", "500"), Row("c", null, "500", GridRowState.Locked), Row("d", "D", "5")], Rule);
        // a: its cell first (the rule waits for valid cells); b: the rule; c: locked, skipped; d: fine.
        Assert.Equal([("a", "name"), ("b", "")], errors.Select(e => (e.RowId, e.Key)));
        Assert.True(errors[1].IsRow);
        Assert.Equal("1 cell needs attention before saving.", GridSchema.ErrorSummary(errors.Take(1)));
        Assert.Equal("2 rows need attention before saving.", GridSchema.ErrorSummary([GridError.ForRow("a", "x"), GridError.ForRow("b", "y")]));
        Assert.Equal("2 items need attention before saving.", GridSchema.ErrorSummary(errors));

        Assert.Equal("Locked: Posted", GridSchema.StateText(Row("x", "X", "1", GridRowState.Locked)));
        Assert.Equal("Read-only", GridSchema.StateText(new GridRecord("y") { State = GridRowState.ReadOnly }));
        Assert.Null(GridSchema.StateText(Row("z", "Z", "1")));
        Assert.False(GridSchema.CanEdit(columns[0], Row("x", "X", "1", GridRowState.ReadOnly)));

        // The browser posts edits to a read-only row, drops a locked and a read-only one, and has no states.
        List<GridRecord> stored = [Row("e", "E", "1"), Row("r", "R", "2", GridRowState.ReadOnly), Row("l", "L", "3", GridRowState.Locked), Row("g", "G", "4", GridRowState.ReadOnly)];
        List<GridRecord> posted = [Row("e", "E2", "10"), Row("r", "Hacked", "999")];
        GridSchema.Protect(columns, posted, stored);

        Assert.Equal(["e", "r", "l"], posted.Select(r => r.Id)); // the locked row is back in its place; the read-only one stays deleted
        Assert.Equal(("E2", "10"), (posted[0]["name"], posted[0]["budget"]));
        Assert.Equal(("R", "2", GridRowState.ReadOnly), (posted[1]["name"], posted[1]["budget"], posted[1].State));
        Assert.Equal(("L", GridRowState.Locked, "Posted"), (posted[2]["name"], posted[2].State, posted[2].StateReason));
    }

    [Fact]
    public void Matches_SearchesDisplayedTextAndSubtitles_AndAppliesEveryFilter()
    {
        var columns = Columns();
        columns[0].SubtitleKey = "email";
        var row = new GridRecord("r", new Dictionary<string, string?>
        {
            ["name"] = "Portal",
            ["email"] = "ann@example.com",
            ["status"] = "a",
            ["teams"] = "web,data",
            ["billable"] = "true",
            ["budget"] = "1250",
        });

        Assert.True(GridSchema.Matches(columns, row, "active", culture: Us));   // the option label, not the value
        Assert.True(GridSchema.Matches(columns, row, "$1,250", culture: Us));  // formatted money
        Assert.True(GridSchema.Matches(columns, row, "EXAMPLE", culture: Us)); // the subtitle
        Assert.False(GridSchema.Matches(columns, row, "done", culture: Us));

        Assert.True(GridSchema.Matches(columns, row, null, new Dictionary<string, string?> { ["teams"] = "data", ["billable"] = "true", ["status"] = "" }));
        Assert.False(GridSchema.Matches(columns, row, null, new Dictionary<string, string?> { ["billable"] = "false" }));
        Assert.False(GridSchema.Matches(columns, row, null, new Dictionary<string, string?> { ["status"] = "d" }));
        Assert.True(GridSchema.Matches(columns, row, null, new Dictionary<string, string?> { ["unknown"] = "x" }));

        // Column searches on a column with a subtitle: =exact (a filter-row choice) matches the cell itself,
        // contains also finds the subtitle, and !exclude looks at both.
        var searches = (string q) => new Dictionary<string, string?> { ["name"] = q };
        Assert.True(GridSchema.Matches(columns, row, null, searches: searches("=Portal")));
        Assert.True(GridSchema.Matches(columns, row, null, searches: searches("example")));
        Assert.False(GridSchema.Matches(columns, row, null, searches: searches("!example")));
        Assert.False(GridSchema.Matches(columns, row, null, searches: searches("=Port")));

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

public class EditableGridTests : AsyncBunitContext
{
    // Cells format with the current culture; CI runs under the invariant culture, which renders ¤1,200 instead of $1,200.
    private readonly CultureInfo culture = CultureInfo.CurrentCulture;
    private readonly CultureInfo uiCulture = CultureInfo.CurrentUICulture;

    public EditableGridTests()
    {
        CultureInfo.CurrentCulture = CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
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
    public async Task DraggedRow_ReordersRows_MarksDirty_AndReportsThePageOrder()
    {
        var rows = Rows();
        Modulus.Ui.Abstractions.Plugins.RowMove? moved = null;
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, Columns()).Add(g => g.Rows, rows).Add(g => g.HeaderTitle, "Projects")
            .Add(g => g.OnRowMove, m => moved = m));

        Assert.Equal(2, cut.FindAll("[data-m-sort-row]").Count);
        Assert.Equal("Move Portal", cut.Find("[data-m-sort-row='r1']").GetAttribute("aria-label"));
        Assert.NotNull(cut.Find("[data-m-sort-rows][data-m-sort-mode='event']"));

        // What modulus-plugins.js writes after r2 is dropped on top.
        await cut.Find("input[data-m-sort-result]").ChangeAsync("""{"id":"r2","from":"x","to":"x","index":0}""");

        Assert.Equal(["r2", "r1"], rows.Select(r => r.Id));
        Assert.Equal(["r2", "r1"], moved!.Order);
        Assert.NotEmpty(cut.FindAll(".m-grid-dirty"));
    }

    [Fact]
    public void DragHandles_AreLocked_WhileAColumnSortIsOn()
    {
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, Columns()).Add(g => g.Rows, Rows()).Add(g => g.SortField, "budget")
            .Add(g => g.OnSort, (string _) => { }));

        Assert.All(cut.FindAll("[data-m-sort-row]"), h => Assert.Equal("true", h.GetAttribute("aria-disabled")));
        Assert.NotNull(cut.Find("[data-m-sort-rows][data-m-sort-disabled]"));
    }

    [Fact]
    public async Task AddRow_InsertsAnEditableRowAtTheTop_AndMarksDirty()
    {
        var rows = Rows();
        var changes = 0;
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, Columns()).Add(g => g.Rows, rows).Add(g => g.OnChange, () => changes++));

        // The toolbar's add button is an icon with a tooltip (Insert does the same).
        await cut.Find("[data-m-grid-add]").ClickAsync(new());

        Assert.Equal(3, rows.Count);
        Assert.Equal("n3", rows[0].Id);
        Assert.Equal(1, changes);
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("tbody tr.ant-table-row").Count));
        Assert.Equal("1 unsaved change", cut.Find(".m-grid-dirty").TextContent.Trim());
        Assert.NotEmpty(cut.FindAll(".m-grid-cell-editing input"));
    }

    [Fact]
    public async Task EditStates_TintTheChangedRowAndCell_TheAddedRow_UntilSaved()
    {
        var rows = Rows();
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, Columns()).Add(g => g.Rows, rows).Add(g => g.OnSave, () => { }));
        // AntD Blazor's rows carry no key: r1 and r2 by their names, the added row on top.
        IElement Row(string id) => id == "n3" ? cut.FindAll("tbody tr.ant-table-row")[0]
            : cut.Find($"[aria-label^='Edit Name: {(id == "r1" ? "Portal" : "Mobile")}']").Closest("tr")!;
        IElement Cell(string id, string title) => Row(id).QuerySelector($"[aria-label^='Edit {title}:']")!.Closest(".m-grid-cell")!;

        Assert.Empty(cut.FindAll(".m-grid-row-dirty, .m-grid-cell-dirty"));

        // Billable is a switch, changed in place: its row turns amber and the cell itself is marked.
        await Row("r1").QuerySelector(".ant-switch")!.ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("m-grid-row-dirty", Row("r1").ClassList));
        Assert.DoesNotContain("m-grid-row-dirty", Row("r2").ClassList);
        Assert.Single(cut.FindAll(".m-grid-cell-dirty"));
        Assert.Contains("m-grid-cell-dirty", Row("r1").QuerySelector(".ant-switch")!.Closest(".m-grid-cell")!.ClassList);
        Assert.DoesNotContain("m-grid-cell-dirty", Cell("r1", "Name").ClassList);

        // An added row is new (blue), not changed, and the row being edited is marked too.
        await cut.Find("[data-m-grid-add]").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Contains("m-grid-row-new", Row("n3").ClassList));
        Assert.Contains("m-grid-row-editing", Row("n3").ClassList);
        Assert.DoesNotContain("m-grid-row-dirty", Row("n3").ClassList);

        // Saving clears every mark.
        rows[0]["name"] = "Portal";
        rows.Single(r => r.Id == "n3")["name"] = "New";
        await cut.FindAll("button").First(b => b.TextContent.Trim() == "Save").ClickAsync(new());
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".m-grid-row-dirty, .m-grid-row-new, .m-grid-cell-dirty")));
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

    [Fact]
    public async Task RowStates_ReadOnlyAndLockedRows_DontEdit_LockedRowsDontDeleteOrMove()
    {
        var rows = Rows();
        rows.Add(new("r3", new Dictionary<string, string?> { ["name"] = "Archive", ["budget"] = "50", ["billable"] = "false", ["owner"] = "Cy" }));
        (rows[1].State, rows[1].StateReason) = (GridRowState.ReadOnly, "Approved");
        (rows[2].State, rows[2].StateReason) = (GridRowState.Locked, "Invoiced");
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, Columns()).Add(g => g.Rows, rows));
        IElement Row(string name) => cut.FindAll("tbody tr.ant-table-row").Single(tr => tr.TextContent.Contains(name));

        // Only the editable row's cells are buttons; the others say why in their first cell.
        Assert.Equal(3, cut.FindAll("[aria-label^='Edit ']").Count); // name, budget and owner of Portal
        Assert.Contains("m-grid-row-readonly", Row("Mobile").ClassList);
        Assert.Contains("m-grid-row-locked", Row("Archive").ClassList);
        Assert.Equal("Read-only: Approved", Row("Mobile").QuerySelector(".m-grid-row-mark")!.GetAttribute("title"));
        Assert.Equal("Locked: Invoiced", Row("Archive").QuerySelector(".m-grid-row-mark .m-sr-only")!.TextContent);
        Assert.NotNull(Row("Mobile").QuerySelector(".ant-switch-disabled"));

        // A locked row can't be dragged or deleted; a read-only one can.
        Assert.Equal("true", Row("Archive").QuerySelector("[data-m-sort-row]")!.GetAttribute("aria-disabled"));
        Assert.Equal("Locked: Invoiced", Row("Archive").QuerySelector("[data-m-sort-row]")!.GetAttribute("title"));
        Assert.Null(Row("Mobile").QuerySelector("[data-m-sort-row]")!.GetAttribute("aria-disabled"));
        Assert.True(Row("Archive").QuerySelector("td.m-grid-actions button")!.HasAttribute("disabled"));
        Assert.False(Row("Mobile").QuerySelector("td.m-grid-actions button")!.HasAttribute("disabled"));

        // A locked row dropped elsewhere stays put (the handle is disabled, so this is a forged move).
        await cut.Find("input[data-m-sort-result]").ChangeAsync("""{"id":"r3","from":"x","to":"x","index":0}""");
        Assert.Equal(["r1", "r2", "r3"], rows.Select(r => r.Id));
        Assert.Empty(cut.FindAll(".m-grid-dirty"));
    }

    [Fact]
    public async Task RowRule_FlagsTheRow_BlocksSave_AndClearsOnceFixed_AndTheAppCanRefuseRows()
    {
        var rows = Rows();
        rows[1]["budget"] = "9000";
        var saved = 0;
        EditableGrid grid = null!;
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, Columns()).Add(g => g.Rows, rows)
            .Add(g => g.ValidateRow, r => GridSchema.Number(r["budget"]) > 5000 ? "Over the limit." : null)
            .Add(g => g.OnSave, () =>
            {
                saved++;
                // The server refuses Portal: the changes stay unsaved.
                if (saved == 1) grid.ShowErrors([GridError.ForRow("r1", "Portal is checked out by Ann.")]);
            }));
        grid = cut.Instance;
        IElement Save() => cut.FindAll("button").First(b => b.TextContent.Trim() == "Save");
        IElement Row(string name) => cut.FindAll("tbody tr.ant-table-row").Single(tr => tr.TextContent.Contains(name));

        await cut.InvokeAsync(() => Save().Click());
        Assert.Equal(0, saved);
        // The grid shows the refusal in a later render: wait for each part before reading the rows.
        cut.WaitForAssertion(() => Assert.Contains("1 row needs attention", cut.Find(".ant-alert").TextContent));
        cut.WaitForAssertion(() => Assert.Contains("m-grid-row-error", Row("Mobile").ClassList));
        cut.WaitForAssertion(() => Assert.Equal("Over the limit.", Row("Mobile").QuerySelector(".m-grid-row-message")!.TextContent));

        // Fixing the cell clears the row's error at once.
        await cut.InvokeAsync(() => Row("Mobile").QuerySelector("[aria-label^='Edit Budget:']")!.ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.NotNull(Row("Mobile").QuerySelector("input[type=number]")));
        await cut.InvokeAsync(() => Row("Mobile").QuerySelector("input[type=number]")!.InputAsync(new ChangeEventArgs { Value = "800" }));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".m-grid-row-error")));
        // Let the grid finish its pending render before the next click looks up the Save handler.
        await cut.InvokeAsync(() => { });

        await cut.InvokeAsync(() => Save().Click());
        Assert.Equal(1, saved);
        cut.WaitForAssertion(() => Assert.Equal("Portal is checked out by Ann.", Row("Portal").QuerySelector(".m-grid-row-message")!.TextContent));
        Assert.NotEmpty(cut.FindAll(".m-grid-dirty"));

        await cut.InvokeAsync(() => Save().Click());
        Assert.Equal(2, saved);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".m-grid-row-error, .m-grid-dirty")));
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
    public async Task Toolbar_LooksLikeTheDataTable_SearchColumnsExportFiltersAndSelection()
    {
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, RichColumns()).Add(g => g.Rows, RichRows()).Add(g => g.HeaderTitle, "Projects"));

        Assert.Contains("m-resizable", cut.Find(".m-grid").ClassList);
        // The search box opens from its icon; Columns and Export CSV are labelled buttons; density sits in the More menu.
        Assert.Empty(cut.FindAll(".m-grid-search"));
        await cut.Find("[data-m-search-toggle]").ClickAsync(new());
        Assert.Equal("Search projects…", cut.Find(".m-grid-search input").GetAttribute("placeholder"));
        Assert.Equal("Columns", cut.Find(".m-table-columns-toggle").GetAttribute("aria-label"));
        Assert.Equal("Columns", cut.Find(".m-table-columns-toggle").TextContent.Trim());
        Assert.Equal("Export CSV", cut.Find(".m-grid-export").TextContent.Trim());
        Assert.Equal("Add row (Insert)", cut.Find("[data-m-grid-add]").GetAttribute("title"));
        Assert.NotNull(cut.Find(".m-table-toolbar-settings [aria-label='More actions']"));
        Assert.Equal(2, cut.FindAll(".m-table-filters .ant-select").Count); // owner, priority
        Assert.NotNull(cut.Find("thead th.ant-table-selection-column"));
        Assert.Equal(2, cut.FindAll("tbody td.ant-table-selection-column").Count);
        Assert.Equal("owner", cut.FindAll("thead .m-th")[1].GetAttribute("data-key"));
    }

    [Fact]
    public async Task FilterRow_ShowsTheColumnSearchesUnderTheHeaders_AndFilters()
    {
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, RichColumns()).Add(g => g.Rows, RichRows()));
        Assert.Empty(cut.FindAll(".m-th-filter"));
        Assert.Equal(3, cut.FindAll(".m-th-search").Count);

        await cut.Find(".m-table-filter-toggle").ClickAsync(new());

        Assert.Equal("true", cut.Find(".m-table-filter-toggle").GetAttribute("aria-pressed"));
        Assert.Equal(3, cut.FindAll(".m-th-filter").Count); // name, owner, priority (email is hidden)
        // Only the checkbox-list columns keep the icon: the filter row's select picks one value, the list several.
        Assert.Equal(["owner", "priority"], cut.FindAll(".m-th-search").Select(s => s.Closest(".m-th")!.GetAttribute("data-key")));
        Assert.All(cut.FindAll(".m-th-search-host"), h => Assert.True(h.HasAttribute("data-values")));
        await cut.FindAll(".m-th-filter")[1].ChangeAsync("Ann");

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("tbody tr.ant-table-row")));
        Assert.Contains("m-th-filter-active", cut.FindAll(".m-th-filter")[1].ClassName);
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
    public void ProTable_TitleExtraFollowsTheTitle_AndRowSelectableDisablesTheOtherRowsBoxes()
    {
        var cut = Render<ProTable<GridRecord>>(p => p
            .Add(t => t.Request, _ => Task.FromResult(new ProTableResponse<GridRecord> { Data = RichRows(), Total = 2 }))
            .Add(t => t.RowKey, r => r.Id)
            .Add(t => t.HeaderTitle, "Projects")
            .Add(t => t.TitleExtra, b => b.AddMarkupContent(0, "<span class=\"m-badge\">2</span>"))
            .Add(t => t.Selectable, true)
            .Add(t => t.RowSelectable, r => r.Id != "r2")
            .Add(t => t.Columns, r => b =>
            {
                b.OpenComponent<PropertyColumn<GridRecord, string>>(0);
                b.AddAttribute(1, "Property", (System.Linq.Expressions.Expression<Func<GridRecord, string>>)(x => x.Id));
                b.CloseComponent();
            }));

        var title = cut.Find(".m-table-toolbar-title");
        Assert.StartsWith("Projects", title.TextContent.Trim());
        Assert.Equal("2", title.QuerySelector(".m-badge")!.TextContent);

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tbody td.ant-table-selection-column input").Count));
        var boxes = cut.FindAll("tbody td.ant-table-selection-column input");
        Assert.False(boxes[0].HasAttribute("disabled"));
        Assert.True(boxes[1].HasAttribute("disabled"));
    }

    [Fact]
    public async Task SelectingRows_ShowsTheBatchBar_AndSetValueUpdatesEverySelectedRow()
    {
        var rows = RichRows();
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, RichColumns()).Add(g => g.Rows, rows));
        Assert.Empty(cut.FindAll(".m-grid-batch"));

        // Re-find the boxes each time: every change renders the rows again.
        for (var i = 0; i < 2; i++) await cut.FindAll("tbody td.ant-table-selection-column input")[i].ChangeAsync(true);

        cut.WaitForAssertion(() => Assert.Contains("Selected 2 rows", cut.Find(".m-grid-batch").TextContent));
        Assert.Equal(["Owner", "Priority"], cut.FindAll(".m-grid-batch-set optgroup").Select(g => g.GetAttribute("label")));
        await cut.Find(".m-grid-batch-set").ChangeAsync("priority=high");

        cut.WaitForAssertion(() => Assert.Equal(["high", "high"], rows.Select(r => $"{r["priority"]}")));
        Assert.NotEmpty(cut.FindAll(".m-grid-dirty"));
    }

    [Fact]
    public async Task BatchActions_CountTheRowsTheyApplyTo_RaiseOnBatchWithThoseRows_AndPrint()
    {
        var rows = RichRows();
        GridBatchRequest? request = null;
        GridBatchAction[] actions =
        [
            new() { Key = "approve", Label = "Approve", Primary = true, When = r => r["owner"] == "ann" },
            new() { Key = "close", Label = "Close", When = r => r["owner"] == "nobody" },
            new() { Key = "reject", Label = "Reject", Danger = true, Confirm = true },
            GridBatchAction.Printing(),
        ];
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, RichColumns()).Add(g => g.Rows, rows).Add(g => g.HeaderTitle, "Projects")
            .Add(g => g.Batch, actions).Add(g => g.OnBatch, (GridBatchRequest r) => request = r));
        for (var i = 0; i < 2; i++) await cut.FindAll("tbody td.ant-table-selection-column input")[i].ChangeAsync(true);

        cut.WaitForAssertion(() => Assert.Equal("1", cut.Find("[data-m-grid-batch=approve] .m-grid-batch-count").TextContent));
        Assert.Equal("2", cut.Find("[data-m-grid-batch=reject] .m-grid-batch-count").TextContent);
        Assert.True(cut.Find("[data-m-grid-batch=close]").HasAttribute("disabled"));
        Assert.Empty(cut.FindAll("[data-m-grid-batch=print] .m-grid-batch-count"));

        // Only the selected rows the action applies to; a batch isn't an unsaved edit.
        await cut.Find("[data-m-grid-batch=approve]").ClickAsync(new());
        Assert.NotNull(request);
        Assert.Equal(["r1"], request.Rows.Select(r => r.Id));
        Assert.Empty(cut.FindAll(".m-grid-dirty"));

        await cut.Find("[data-m-grid-batch=print]").ClickAsync(new());
        var print = Assert.Single(JSInterop.Invocations, i => i.Identifier == "modulusShell.printTable");
        Assert.Equal("Projects", print.Arguments[0]);
    }
}
