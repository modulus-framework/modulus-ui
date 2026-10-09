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
        // Select measures its trigger to size the dropdown.
        JSInterop.Setup<AntDesign.JsInterop.HtmlElement>(_ => true).SetResult(new AntDesign.JsInterop.HtmlElement());
        JSInterop.Setup<AntDesign.JsInterop.DomRect>(_ => true).SetResult(new AntDesign.JsInterop.DomRect());
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
    public void TreeMode_RepeatsTheFirstLevel_AndExpandsOnlyRowsWithChildren()
    {
        var s = new NestedGridSettings
        {
            Levels = [new() { Title = "Parts", Columns = [Name, Qty] }],
            Tree = new NestedGridTree { MaxDepth = 3, ChildCountField = "children" },
        };
        var parent = new GridRecord("p", new Dictionary<string, string?> { ["name"] = "Shirt", ["children"] = "2" });
        var leaf = new GridRecord("l", new Dictionary<string, string?> { ["name"] = "Button", ["children"] = "0" });

        Assert.Equal("Parts", s.Level(0)!.Title);
        Assert.Equal("Parts", s.Level(2)!.Title);
        Assert.Null(s.Level(3)); // MaxDepth
        Assert.True(s.CanExpand(0, parent));
        Assert.True(s.CanExpand(1, parent));
        Assert.False(s.CanExpand(0, leaf));
        Assert.False(s.CanExpand(2, parent)); // the last depth never expands

        // Without a child count every row expands (the first open loads an empty level).
        s.Tree.ChildCountField = null;
        Assert.True(s.CanExpand(0, leaf));
    }

    [Fact]
    public void Apply_TotalsTheRollupColumns_OverEveryMatchingRow()
    {
        GridColumn[] columns = [Name, Qty];
        var rows = new[] { ("a", "Alpha", "5"), ("b", "Beta", null), ("c", "Gamma", "12"), ("d", "Delta", "1.5") }
            .Select(t => new GridRecord(t.Item1, new Dictionary<string, string?> { ["name"] = t.Item2, ["qty"] = t.Item3 })).ToList();

        // Page 1 holds two rows, but the totals cover all four; the text column is not summed.
        var page = NestedGridQuery.Apply(rows, columns, new NestedGridRequest(0, null, PageSize: 2), ["name", "qty", "missing"]);
        Assert.Equal(2, page.Rows.Count);
        var total = Assert.Single(page.Totals);
        Assert.Equal(("qty", 18.5m, 1), (total.Field, total.Value, total.Decimals));

        // A filter narrows the totals too; a column without values totals to none.
        var filtered = NestedGridQuery.Apply(rows, columns, new NestedGridRequest(0, null, Keyword: "beta"), ["qty"]);
        Assert.Null(filtered.Totals.Single().Value);

        Assert.Empty(NestedGridQuery.Apply(rows, columns, new NestedGridRequest(0, null)).Totals);
    }

    [Fact]
    public void RowOrder_MovesWithinThePage_AndKeepsOtherRowsInPlace()
    {
        var move = new Modulus.Ui.Abstractions.Plugins.SortMove("c", "rows", "rows", 0);
        Assert.Equal(["c", "a", "b"], move.Within(["a", "b", "c"]));
        Assert.Equal(["a", "b"], move.Within(["a", "b"])); // not on the page: unchanged

        // a, c and e are the page (b and d filtered out): the page's new order takes their slots.
        var all = new List<string> { "a", "b", "c", "d", "e" };
        GridSchema.Reorder(all, ["e", "a", "c"], id => id);
        Assert.Equal(["e", "b", "a", "d", "c"], all);

        GridSchema.Reorder(all, ["zz", "c", "e"], id => id); // unknown ids are skipped
        Assert.Equal(["c", "b", "a", "d", "e"], all);
    }

    [Fact]
    public void StoreOrder_PlacesAddedRowsWhereTheyWereDropped()
    {
        static GridRecord R(string id) => new(id);
        // Page 1 is a, b; c is on page 2; n was added on page 1 and stored at the end.
        var all = new List<GridRecord> { R("a"), R("b"), R("c"), R("n") };
        NestedGridQuery.StoreOrder(all, ["b", "n", "a"], new HashSet<string> { "n" });
        Assert.Equal(["b", "n", "a", "c"], all.Select(r => r.Id));
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
    public void Query_AppliesFilters_AndWithFilterSetsOrClearsOne()
    {
        var kind = new GridColumn { Key = "kind", Title = "Kind", Type = GridCellType.Select, Filterable = true, Options = [new("a", "A"), new("b", "B")] };
        var rows = new[] { ("1", "a"), ("2", "b"), ("3", "a") }.Select(t => new GridRecord(t.Item1, new Dictionary<string, string?> { ["name"] = "x" + t.Item1, ["kind"] = t.Item2 })).ToList();
        var request = new NestedGridRequest(0, null, PageSize: 10, Page: 3).WithFilter("kind", "a");

        Assert.Equal(1, request.Page);
        Assert.Equal(["1", "3"], NestedGridQuery.Apply(rows, [Name, kind], request).Rows.Select(r => r.Id));
        Assert.Null(request.WithFilter("kind", "").Filters);
        Assert.Equal(3, NestedGridQuery.Apply(rows, [Name, kind], request.WithFilter("kind", null)).Total);
    }

    [Fact]
    public async Task NestedGrid_FilterRowSearchesOnTheServer()
    {
        var settings = Settings();
        settings.Levels[0].Columns = [Name, new GridColumn { Key = "kind", Title = "Kind", Type = GridCellType.Select, Filterable = true, Options = [new("a", "A")] }];
        var cut = Render<NestedGrid>(p => p.Add(g => g.Settings, settings)
            .Add(g => g.Load, _ => Task.FromResult(new NestedGridPage([], 0))));

        // The filters sit under the column headers from the start (a select for the option column, a box for the text
        // one), searching on the server; there is no filter bar above the table and no second search box.
        Assert.Empty(cut.FindAll(".m-table-filters"));
        Assert.Equal(["Any kind", "A"], cut.FindAll("select.m-th-filter option").Select(o => o.TextContent));
        Assert.Equal("=A", cut.FindAll("select.m-th-filter option")[1].GetAttribute("value"));
        Assert.Single(cut.FindAll("input.m-th-filter"));
        Assert.Empty(cut.FindAll(".m-grid-search"));
        // The level's own search folds into one icon while it is empty; a click opens the box.
        await cut.Find(".m-table-search-toggle").ClickAsync(new());
        Assert.Single(cut.FindAll(".m-nested-search"));
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
        Assert.NotNull(cut.Find(".m-nested-level-0 .m-table-search-toggle"));
        Assert.Single(cut.FindAll("thead .m-th-sort")); // the Name header sorts the level
        Assert.Contains("First", cut.Markup);
        Assert.NotEmpty(cut.FindAll(".ant-pagination")); // 7 rows, 5 per page
    }
}
