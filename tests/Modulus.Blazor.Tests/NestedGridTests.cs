using AntDesign;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>NestedGridSettings, NestedGridQuery and the Blazor NestedGrid.</summary>
public class NestedGridTests : BunitContext
{
    private static readonly GridColumn Name = new() { Key = "name", Title = "Name", Required = true };
    private static readonly GridColumn Qty = new() { Key = "qty", Title = "Qty", Type = GridCellType.Number };

    private static NestedGridSettings Settings() => new()
    {
        Levels =
        [
            new() { Title = "Groups", Columns = [Name] },
            new()
            {
                Title = "Items",
                Columns = [Name],
                ColumnSetField = "kind",
                ColumnSets = new Dictionary<string, IReadOnlyList<GridColumn>> { ["counted"] = [Name, Qty] },
                RowExpandable = r => r["name"] != "leaf",
            },
            new() { Title = "Parts", Columns = [Name] },
        ],
    };

    public NestedGridTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddAntDesign();
    }

    [Fact]
    public void ColumnsFor_PicksTheParentsColumnSet_AndClones()
    {
        var s = Settings();
        Assert.Equal(["name", "qty"], s.ColumnsFor(1, new GridRecord("g1", new Dictionary<string, string?> { ["kind"] = "counted" })).Select(c => c.Key));
        Assert.Equal(["name"], s.ColumnsFor(1, new GridRecord("g2", new Dictionary<string, string?> { ["kind"] = "other" })).Select(c => c.Key));
        Assert.Empty(s.ColumnsFor(5, null));

        s.ColumnsFor(0, null)[0].Title = "Changed";
        Assert.Equal("Name", Name.Title);
    }

    [Fact]
    public void CanExpand_FollowsTheNumberOfLevelsAndRowExpandable()
    {
        var s = Settings();
        var row = new GridRecord("r", new Dictionary<string, string?> { ["name"] = "x" });
        Assert.True(s.CanExpand(0, row));
        Assert.True(s.CanExpand(1, row));
        Assert.False(s.CanExpand(1, new GridRecord("l", new Dictionary<string, string?> { ["name"] = "leaf" })));
        Assert.False(s.CanExpand(2, row)); // last level

        s.Levels = [.. s.Levels.Take(2)];
        Assert.False(s.CanExpand(1, row)); // two levels: items are the last
    }

    [Fact]
    public void Query_SearchesSortsAndPages_EmptyCellsLast()
    {
        GridColumn[] columns = [Name, Qty];
        var rows = new[] { ("a", "Alpha", "5"), ("b", "Beta", null), ("c", "Gamma", "12"), ("d", "Delta", "1") }
            .Select(t => new GridRecord(t.Item1, new Dictionary<string, string?> { ["name"] = t.Item2, ["qty"] = t.Item3 })).ToList();

        var desc = NestedGridQuery.Apply(rows, columns, new NestedGridRequest(0, null, SortField: "qty", Descending: true, PageSize: 10));
        Assert.Equal(["c", "a", "d", "b"], desc.Rows.Select(r => r.Id));
        var asc = NestedGridQuery.Apply(rows, columns, new NestedGridRequest(0, null, SortField: "qty", PageSize: 10));
        Assert.Equal(["d", "a", "c", "b"], asc.Rows.Select(r => r.Id));

        var search = NestedGridQuery.Apply(rows, columns, new NestedGridRequest(0, null, Keyword: "ta", PageSize: 10));
        Assert.Equal(["b", "d"], search.Rows.Select(r => r.Id)); // Beta, Delta

        var page = NestedGridQuery.Apply(rows, columns, new NestedGridRequest(0, null, Page: 9, PageSize: 3));
        Assert.Equal(4, page.Total);
        Assert.Equal(["d"], page.Rows.Select(r => r.Id));
    }

    [Fact]
    public void NestedGrid_LoadsTheTopLevelAndShowsItsTools()
    {
        var requests = new List<NestedGridRequest>();
        var cut = Render<NestedGrid>(p => p
            .Add(g => g.Settings, Settings())
            .Add(g => g.HeaderTitle, "All groups")
            .Add(g => g.Load, r =>
            {
                requests.Add(r);
                return Task.FromResult(new NestedGridPage([new GridRecord("g1", new Dictionary<string, string?> { ["name"] = "First" })], 7));
            }));

        Assert.Equal(new NestedGridRequest(0, null, null, null, false, 1, 5), Assert.Single(requests));
        Assert.Contains("All groups", cut.Find(".m-table-toolbar-title").TextContent);
        Assert.Equal("7", cut.Find(".m-table-toolbar-title .m-badge").TextContent);
        Assert.NotNull(cut.Find(".m-nested-level-0 .m-nested-search"));
        Assert.Equal(3, cut.FindAll("select.m-nested-sort option").Count); // default + Name ↑ ↓
        Assert.Contains("First", cut.Markup);
        Assert.NotEmpty(cut.FindAll(".ant-pagination")); // 7 rows, 5 per page
    }
}
