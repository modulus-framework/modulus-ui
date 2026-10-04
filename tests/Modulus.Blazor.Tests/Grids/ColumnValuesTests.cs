using System.Globalization;
using AntDesign;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The checkbox list of a column header (the Excel-style filter menu): value lists, their values and the Blazor menu.</summary>
public class ColumnValuesTests : BunitContext
{
    private static readonly CultureInfo Us = CultureInfo.GetCultureInfo("en-US");

    public ColumnValuesTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<AntDesign.JsInterop.HtmlElement>(_ => true).SetResult(new AntDesign.JsInterop.HtmlElement());
        JSInterop.Setup<AntDesign.JsInterop.DomRect>(_ => true).SetResult(new AntDesign.JsInterop.DomRect());
        Services.AddAntDesign();
    }

    [Fact]
    public void ValueQuery_IsTheShorterList_AndNullForAllOrNone()
    {
        string[] values = ["Active", "Pending", "Closed", ""];
        Assert.Equal("=Active", ColumnSearch.ValueQuery(["Active"], values));
        Assert.Equal("=Active|Pending", ColumnSearch.ValueQuery(["Active", "Pending"], ["Active", "Pending", "Closed", "Archived", "Draft"]));
        Assert.Equal("!=Closed", ColumnSearch.ValueQuery(["Active", "Pending", ""], values));
        Assert.Equal("=|", ColumnSearch.ValueQuery([""], values));
        Assert.Null(ColumnSearch.ValueQuery(values, values));
        Assert.Null(ColumnSearch.ValueQuery(["active", "PENDING", "closed", ""], values)); // case doesn't matter
        Assert.Null(ColumnSearch.ValueQuery([], values));
    }

    [Fact]
    public void ValueLists_Match_Exactly_IncludingBlanks()
    {
        Assert.True(ColumnSearch.Matches("=Active|Pending", "pending"));
        Assert.False(ColumnSearch.Matches("=Active|Pending", "Active member")); // exact, not contains
        Assert.False(ColumnSearch.Matches("!=Active|Pending", "Active"));
        Assert.True(ColumnSearch.Matches("!=Active|Pending", "Closed"));
        Assert.True(ColumnSearch.Matches("=|", ""));
        Assert.True(ColumnSearch.Matches("=|", null));
        Assert.False(ColumnSearch.Matches("=|", "Active"));
        Assert.True(ColumnSearch.Matches("=Active|", " "));
        Assert.False(ColumnSearch.Matches("!=|", ""));
        // A list on a number column still compares the text.
        Assert.True(ColumnSearch.Matches("=10|20", "20", ColumnKind.Number, 20));
        // "!=a" alone stays "not exactly a"; "=a" alone stays exact.
        Assert.True(ColumnSearch.IsValueList("!=Active"));
        Assert.False(ColumnSearch.Matches("!=Active", "active"));
        Assert.True(ColumnSearch.Matches("!=Active", "Active member"));
        Assert.True(ColumnSearch.Matches("=Active", "active"));
        Assert.False(ColumnSearch.IsValueList("=Active"));
        Assert.True(ColumnSearch.IsValueList("=Active|Closed"));
    }

    [Fact]
    public void Picked_ChecksTheValuesASearchKeeps()
    {
        string[] values = ["Active", "Pending", "Closed", ""];
        Assert.Equal(4, ColumnSearch.Picked(null, values).Count);
        Assert.Equal(4, ColumnSearch.Picked("act", values).Count); // a typed search: all checked
        Assert.Equal(["Active"], ColumnSearch.Picked("=active", values));
        Assert.Equal(["Active", "Pending"], ColumnSearch.Picked("=Active|Pending", values).Order());
        Assert.Equal(["", "Active", "Pending"], ColumnSearch.Picked("!=Closed|Gone", values).Order());
        Assert.Equal([""], ColumnSearch.Picked("=|", values));
    }

    [Fact]
    public void Describe_ReadsAListAsItsValues()
    {
        Assert.Equal("Active, Pending", ColumnSearch.Describe("=Active|Pending"));
        Assert.Equal("Not Closed, (Blanks)", ColumnSearch.Describe("!=Closed|"));
        Assert.Equal(">10", ColumnSearch.Describe(" >10 "));
        Assert.Equal("", ColumnSearch.Describe(null));
    }

    [Fact]
    public void FromQuery_KeepsLongLists_ButCutsTypedSearches()
    {
        var list = "=" + string.Join("|", Enumerable.Range(0, 300).Select(i => $"Value {i}"));
        var typed = new string('x', 300);
        var searches = ColumnSearch.FromQuery([new("s.status", list), new("s.name", typed)])!;
        Assert.Equal(ColumnSearch.MaxListLength, searches["status"]!.Length);
        Assert.Equal(ColumnSearch.MaxLength, searches["name"]!.Length);
        Assert.Equal(list[..200], ColumnSearch.FromQuery([new("s.status", list[..200])])!["status"]);
    }

    [Fact]
    public void ValuesOf_CountsAndSortsWithBlanksLast_AndNarrowFilters()
    {
        var values = ColumnSearch.ValuesOf(["Paris", "london", null, "London", "  ", "a|b", "Berlin"]);
        Assert.Equal([new("Berlin", 1), new("london", 2), new("Paris", 1), new("", 2)], values);

        Assert.Equal(["london"], ColumnSearch.Narrow(values, "LON").Select(v => v.Text));
        Assert.Equal([""], ColumnSearch.Narrow(values, "blank").Select(v => v.Text));
        Assert.Equal(4, ColumnSearch.Narrow(values, " ").Count());
    }

    [Fact]
    public void SearchValues_AreTheOptionsThenOtherCells_WithCounts()
    {
        var status = new GridColumn { Key = "status", Title = "Status", Type = GridCellType.Select, Options = [new("a", "Active"), new("d", "Done")] };
        GridRecord Row(string? s) => new("r", new Dictionary<string, string?> { ["status"] = s });
        var rows = new[] { Row("a"), Row("a"), Row(null), Row("gone") };

        Assert.Equal([new("Active"), new("Done")], GridSchema.SearchValues(status));
        Assert.Equal([new("Active", 2), new("Done", 0), new("gone", 1), new("", 1)], GridSchema.SearchValues(status, rows, Us));
        Assert.Equal(["Yes", "No"], GridSchema.SearchValues(new GridColumn { Key = "b", Type = GridCellType.Switch })!.Select(v => v.Text));
        Assert.Null(GridSchema.SearchValues(new GridColumn { Key = "name" }));
        Assert.Null(GridSchema.SearchValues(new GridColumn { Key = "t", Type = GridCellType.Select }));
    }

    [Fact]
    public void ColumnMatches_AValueListComparesTheCellAlone_NotItsSubtitle()
    {
        var name = new GridColumn { Key = "name", Title = "Name", SubtitleKey = "email" };
        var row = new GridRecord("r", new Dictionary<string, string?> { ["name"] = "Ann", ["email"] = "ann@x.dev" });
        Assert.True(GridSchema.ColumnMatches(name, row, "=Ann|Bo", Us));
        Assert.False(GridSchema.ColumnMatches(name, row, "=ann@x.dev|Bo", Us));
        Assert.True(GridSchema.ColumnMatches(name, row, "ann@", Us)); // a typed search still reads the subtitle
    }

    // ---- The Blazor menu ----

    private static List<GridColumn> Columns() =>
    [
        new() { Key = "name", Title = "Name" },
        new() { Key = "status", Title = "Status", Type = GridCellType.Select, Options = [new("a", "Active"), new("p", "Pending"), new("c", "Closed")] },
    ];

    private static List<GridRecord> Rows() =>
    [
        new("r1", new Dictionary<string, string?> { ["name"] = "One", ["status"] = "a" }),
        new("r2", new Dictionary<string, string?> { ["name"] = "Two", ["status"] = "p" }),
        new("r3", new Dictionary<string, string?> { ["name"] = "Three", ["status"] = "c" }),
        new("r4", new Dictionary<string, string?> { ["name"] = "Four", ["status"] = "a" }),
    ];

    // AntD's Dropdown opens through JS in a browser; here its VisibleChanged opens it, as a click on the icon does.
    private static Task OpenAsync(IRenderedComponent<EditableGrid> cut, string key)
    {
        var header = cut.FindComponents<ColumnHeader>().Single(h => h.Instance.Key == key);
        var drop = header.FindComponent<Dropdown>();
        return header.InvokeAsync(() => drop.Instance.VisibleChanged.InvokeAsync(true));
    }

    [Fact]
    public async Task EditableGrid_OptionColumnsOpenACheckboxList_ThatFilters()
    {
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, Columns()).Add(g => g.Rows, Rows()));
        await OpenAsync(cut, "status");

        var labels = cut.WaitForElements(".m-col-value .m-col-value-text");
        Assert.Equal(["Active", "Pending", "Closed"], labels.Select(e => e.TextContent));
        Assert.Equal(["2", "1", "1"], cut.FindAll(".m-col-value-count").Select(e => e.TextContent));
        Assert.Equal(3, cut.FindAll(".m-col-value.ant-checkbox-wrapper-checked").Count);
        Assert.Empty(cut.FindAll(".m-col-search-hint"));

        // Narrow to "pen", Select all off: OK is off with nothing picked.
        await cut.Find(".m-col-search input[type=search]").InputAsync(new() { Value = "pen" });
        cut.WaitForAssertion(() => Assert.Equal(["Pending"], cut.FindAll(".m-col-value .m-col-value-text").Select(e => e.TextContent)), TimeSpan.FromSeconds(2));
        await cut.Find(".m-col-value-all input").ChangeAsync(new() { Value = false });
        Assert.True(cut.Find(".m-col-search .ant-btn-primary").HasAttribute("disabled"));

        // Clear the box, untick Closed: the shown and checked values apply.
        await cut.Find(".m-col-search input[type=search]").InputAsync(new() { Value = "" });
        Assert.Equal("mixed", cut.Find(".m-col-value-all input").GetAttribute("aria-checked"));
        await cut.FindAll(".m-col-value input")[2].ChangeAsync(new() { Value = false });
        await cut.Find(".m-col-search .ant-btn-primary").ClickAsync(new());

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tbody tr.ant-table-row").Count)); // the two Active rows
        Assert.Contains("Search Status: =Active", cut.Find(".m-th[data-key='status'] .m-th-search").GetAttribute("aria-label"));
    }

    [Fact]
    public async Task EditableGrid_TextColumnsKeepTheSearchBox()
    {
        var cut = Render<EditableGrid>(p => p.Add(g => g.Columns, Columns()).Add(g => g.Rows, Rows()));
        await OpenAsync(cut, "name");
        cut.WaitForElement(".m-col-search-hint");
        Assert.Empty(cut.FindAll(".m-col-values"));
    }
}
