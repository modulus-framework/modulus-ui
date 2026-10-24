using AntDesign;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Data;
using Xunit;
using SortDirection = Modulus.Ui.Abstractions.SortDirection;

namespace Modulus.Blazor.Tests;

/// <summary>Sorting by several columns: SortKeys (clicks, text), the request and URL, TableQuery and ProTable's Shift+click.</summary>
public class MultiSortTests : AsyncBunitContext
{
    public MultiSortTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<AntDesign.JsInterop.HtmlElement>(_ => true).SetResult(new AntDesign.JsInterop.HtmlElement());
        JSInterop.Setup<AntDesign.JsInterop.DomRect>(_ => true).SetResult(new AntDesign.JsInterop.DomRect());
        Services.AddAntDesign();
    }

    private static SortKey Asc(string f) => new(f, SortDirection.Ascending);

    private static SortKey Desc(string f) => new(f, SortDirection.Descending);

    [Fact]
    public void Click_PlainSortsByOneColumn_ShiftAddsTurnsAndDrops()
    {
        IReadOnlyList<SortKey> sorts = [];
        sorts = SortKeys.Click(sorts, "dept", add: false);
        Assert.Equal([Asc("dept")], sorts);

        sorts = SortKeys.Click(sorts, "salary", add: true);   // Shift: joins after
        Assert.Equal([Asc("dept"), Asc("salary")], sorts);
        sorts = SortKeys.Click(sorts, "salary", add: true);   // Shift again: turns
        Assert.Equal([Asc("dept"), Desc("salary")], sorts);
        Assert.Equal(2, SortKeys.Rank(sorts, "Salary"));
        Assert.Equal(SortDirection.Descending, SortKeys.Of(sorts, "SALARY"));

        Assert.Equal([Asc("dept")], SortKeys.Click(sorts, "salary", add: true));   // and drops out
        Assert.Empty(SortKeys.Click(sorts, "salary", add: false));                  // a plain click on a descending column: unsorted
        Assert.Equal([Asc("name")], SortKeys.Click(sorts, "name", add: false));    // a plain click replaces them all
        Assert.Equal(0, SortKeys.Rank([Asc("dept")], "dept"));                     // one sort shows no number

        IReadOnlyList<SortKey> full = [Asc("a"), Asc("b"), Asc("c"), Asc("d")];
        Assert.Equal(full, SortKeys.Click(full, "e", add: true));                  // at most four
    }

    [Fact]
    public void Text_RoundTrips_AndDropsMalformedRepeatedAndExtraKeys()
    {
        Assert.Equal("-salary,name", SortKeys.Format([Desc("salary"), Asc("name")]));
        Assert.Null(SortKeys.Format([]));
        Assert.Equal([Desc("salary"), Asc("name")], SortKeys.Parse(" -salary , +name ,"));
        Assert.Equal([Asc("a")], SortKeys.Parse("a,A,x y,-;drop,b c"));
        Assert.Equal(4, SortKeys.Parse("a,b,c,d,e,f").Count);
        Assert.Empty(SortKeys.Parse(new string('a', 401)));
    }

    [Fact]
    public void Request_Sorts_ArePrimaryThenThenBy()
    {
        var request = new ProTableRequest { SortField = "dept", SortOrder = SortDirection.Descending, ThenBy = "salary,-dept,name" };
        Assert.Equal([Desc("dept"), Asc("salary"), Asc("name")], request.Sorts);

        request.Sorts = [Asc("office"), Desc("salary")];
        Assert.Equal(("office", SortDirection.Ascending, "-salary"), (request.SortField, request.SortOrder, request.ThenBy));
        Assert.Empty(new ProTableRequest { ThenBy = "name" }.Sorts); // no first sort, no sort

        var read = ProTableUrl.Read(SavedViewsParse("sortField=dept&sortOrder=Ascending&thenBy=-salary&keyword=x"));
        Assert.Equal([Asc("dept"), Desc("salary")], read.Sorts);
        Assert.Equal("sortField=dept&sortOrder=Ascending&thenBy=-salary", ProTableUrl.Query(ProTableUrl.Write(read)));
        Assert.True(ProTableUrl.IsTableKey("thenBy"));
    }

    private static IReadOnlyList<KeyValuePair<string, string?>> SavedViewsParse(string q) => Modulus.Ui.Abstractions.State.SavedViews.Parse(q);

    // ---- TableQuery ----

    private sealed record Row(int Id, string Dept, int Salary, string Name);

    private static readonly Row[] Rows = [.. Enumerable.Range(1, 97).Select(i => new Row(i, $"D{i % 4}", 1000 + i % 9 * 100, $"N{i % 13:D2}"))];

    private static readonly TableQuery<Row> Query = TableQuery<Row>.By(r => r.Id)
        .Sort("dept", r => r.Dept).Sort("salary", r => r.Salary).Sort("name", r => r.Name);

    private static ProTableRequest Sorted(int size, params SortKey[] sorts) => new() { PageSize = size, Sorts = sorts };

    [Fact]
    public void TableQuery_OrdersByEverySort_ThenById()
    {
        var page = Query.Page(Rows.AsQueryable(), Sorted(15, Asc("dept"), Desc("salary")));

        var expected = Rows.OrderBy(r => r.Dept).ThenByDescending(r => r.Salary).ThenBy(r => r.Id).Take(15).Select(r => r.Id);
        Assert.Equal(expected, page.Items.Select(r => r.Id));

        // Unknown fields drop out; the known ones keep their order.
        var mixed = Query.Apply(Rows.AsQueryable(), Sorted(10, Desc("nope"), Asc("name"), Asc("dept"))).Select(r => r.Id);
        Assert.Equal(Rows.OrderBy(r => r.Name).ThenBy(r => r.Dept).ThenBy(r => r.Id).Select(r => r.Id), mixed);
        Assert.Equal([Asc("name"), Asc("dept")], Query.SortsOf(Sorted(10, Desc("nope"), Asc("name"), Asc("dept"))));
    }

    [Fact]
    public void TableQuery_CursorOnSeveralSorts_PagesByOffset_AndWalksEveryRowOnce()
    {
        var first = Sorted(20, Desc("dept"), Asc("name"));
        var ids = new List<int>();
        var request = first;
        for (var guard = 0; guard < 20; guard++)
        {
            var page = Query.Page(Rows.AsQueryable(), request, CountMode.None);
            ids.AddRange(page.Items.Select(r => r.Id));
            if (page.NextCursor is null) break;
            request = Sorted(20, Desc("dept"), Asc("name"));
            request.Cursor = page.NextCursor;
        }

        Assert.Equal(Query.Apply(Rows.AsQueryable(), first).Select(r => r.Id), ids);

        // A cursor made for another sort list is ignored (numbered page 1).
        var cursor = Query.Page(Rows.AsQueryable(), first, CountMode.None).NextCursor;
        var other = Sorted(20, Desc("dept"), Desc("name"));
        other.Cursor = cursor;
        Assert.Equal(0, Query.Page(Rows.AsQueryable(), other).Offset);
    }

    [Fact]
    public void TableQuery_SeveralSorts_TranslateToOneOrderBy()
    {
        var sql = Query.Apply(new RowDb().Rows, Sorted(10, Asc("dept"), Desc("salary"))).ToQueryString();
        var orderBy = sql[sql.IndexOf("ORDER BY", StringComparison.Ordinal)..];
        Assert.Matches(@"ORDER BY \[r\]\.\[Dept\], \[r\]\.\[Salary\] DESC, \[r\]\.\[Id\]", orderBy);
    }

    private sealed class RowDb : DbContext
    {
        public DbSet<Row> Rows => Set<Row>();

        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseSqlServer("Server=.;Database=Sorts;Trusted_Connection=True"); // never opened: ToQueryString only
    }

    [Fact]
    public void InMemorySortBy_AppliesEveryKnownSort()
    {
        var keys = new Dictionary<string, Func<Row, object?>>(StringComparer.OrdinalIgnoreCase) { ["dept"] = r => r.Dept, ["salary"] = r => r.Salary };
        var sorted = Rows.SortBy(Sorted(10, Desc("Dept"), Asc("nope"), Asc("salary")), keys).Select(r => r.Id);
        Assert.Equal(Rows.OrderByDescending(r => r.Dept).ThenBy(r => r.Salary).Select(r => r.Id), sorted);
        Assert.Same(Rows, Rows.SortBy(new ProTableRequest(), keys));
    }

    // ---- ProTable ----

    private (IRenderedComponent<ProTable<GridRecord>> Cut, List<ProTableRequest> Requests) RenderTable(bool multiSort)
    {
        var requests = new List<ProTableRequest>();
        var rows = Enumerable.Range(1, 5).Select(i => new GridRecord($"r{i}", new Dictionary<string, string?>())).ToList();
        var cut = Render<ProTable<GridRecord>>(p => p
            .Add(t => t.Request, r =>
            {
                requests.Add(r);
                return Task.FromResult(new ProTableResponse<GridRecord> { Data = rows, Total = rows.Count });
            })
            .Add(t => t.RowKey, r => r.Id)
            .Add(t => t.MultiSort, multiSort)
            .Add(t => t.Columns, r => b =>
            {
                b.OpenComponent<PropertyColumn<GridRecord, string>>(0);
                b.AddAttribute(1, "Property", (System.Linq.Expressions.Expression<Func<GridRecord, string>>)(x => x.Id));
                b.AddAttribute(2, "TitleTemplate", SortableTitle("Dept"));
                b.CloseComponent();
                b.OpenComponent<PropertyColumn<GridRecord, string>>(3);
                b.AddAttribute(4, "Property", (System.Linq.Expressions.Expression<Func<GridRecord, string>>)(x => x.Id));
                b.AddAttribute(5, "TitleTemplate", SortableTitle("Salary"));
                b.CloseComponent();
            }));
        return (cut, requests);
    }

    private static RenderFragment SortableTitle(string key) => t =>
    {
        t.OpenComponent<ColumnHeader>(0);
        t.AddAttribute(1, "Key", key);
        t.AddAttribute(2, "Title", key);
        t.AddAttribute(3, "Searchable", false);
        t.CloseComponent();
    };

    [Fact]
    public async Task ProTable_ShiftClickAddsASort_AndTheHeadersShowTheirPlace()
    {
        var (cut, requests) = RenderTable(multiSort: true);
        cut.WaitForAssertion(() => Assert.NotEmpty(requests));
        AngleSharp.Dom.IElement Sorter(string key) => cut.Find($".m-th[data-key='{key}'] .m-th-sort");

        await Sorter("Dept").ClickAsync(new MouseEventArgs());
        await Sorter("Salary").ClickAsync(new MouseEventArgs { ShiftKey = true });
        await Sorter("Salary").ClickAsync(new MouseEventArgs { ShiftKey = true });

        cut.WaitForAssertion(() => Assert.Equal([Asc("Dept"), Desc("Salary")], requests[^1].Sorts));
        Assert.Equal("-Salary", requests[^1].ThenBy);
        cut.WaitForAssertion(() => Assert.Equal(["1", "2"], cut.FindAll(".m-th-sort-rank").Select(e => e.TextContent)));
        Assert.Contains("(descending, sort 2)", Sorter("Salary").GetAttribute("aria-label"));
        Assert.Contains("Shift+click to add", Sorter("Dept").GetAttribute("title"));

        // A plain click sorts by that column alone again.
        await Sorter("Salary").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.Empty(requests[^1].Sorts));
        await Sorter("Salary").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => Assert.Equal([Asc("Salary")], requests[^1].Sorts));
        Assert.Empty(cut.FindAll(".m-th-sort-rank"));
    }

    [Fact]
    public async Task ProTable_WithoutMultiSort_ShiftClickStillSortsByOneColumn()
    {
        var (cut, requests) = RenderTable(multiSort: false);
        cut.WaitForAssertion(() => Assert.NotEmpty(requests));

        await cut.Find(".m-th[data-key='Dept'] .m-th-sort").ClickAsync(new MouseEventArgs());
        await cut.Find(".m-th[data-key='Salary'] .m-th-sort").ClickAsync(new MouseEventArgs { ShiftKey = true });

        cut.WaitForAssertion(() => Assert.Equal([Asc("Salary")], requests[^1].Sorts));
        Assert.DoesNotContain("Shift", cut.Find(".m-th[data-key='Dept'] .m-th-sort").GetAttribute("title"));
    }
}
