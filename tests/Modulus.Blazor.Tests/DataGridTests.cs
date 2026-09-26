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
}
