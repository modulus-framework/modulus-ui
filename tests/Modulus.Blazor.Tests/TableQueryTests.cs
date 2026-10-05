using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Data;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>TableQuery: allow-listed sorting and searching, numbered pages, keyset cursors and their fallbacks.</summary>
public class TableQueryTests
{
    private sealed record Row(int Id, string Name, int Score, DateOnly Date, int? Rank);

    // Many ties on Score and Date, so the id tie-break matters.
    private static readonly Row[] Rows = [.. Enumerable.Range(1, 237).Select(i =>
        new Row(i, $"Name {i % 17:D2}", i % 7, new DateOnly(2026, 1, 1).AddDays(i % 11), i % 5 == 0 ? null : i % 9))];

    private static readonly TableQuery<Row> Query = TableQuery<Row>.By(r => r.Id)
        .Sort("name", r => r.Name)
        .Sort("score", r => r.Score)
        .Sort("date", r => r.Date)
        .Sort("rank", r => r.Rank)
        .SearchText("name", r => r.Name)
        .DefaultSort("date", descending: true);

    private static IQueryable<Row> Source => Rows.AsQueryable();

    private static ProTableRequest Request(string? sort = null, SortDirection order = SortDirection.Ascending, int current = 1, int size = 20,
        string? cursor = null, Dictionary<string, string?>? searches = null) =>
        new() { SortField = sort, SortOrder = sort is null ? SortDirection.None : order, Current = current, PageSize = size, Cursor = cursor, Searches = searches };

    private static List<int> Expected(Func<IEnumerable<Row>, IOrderedEnumerable<Row>> order) => [.. order(Rows).Select(r => r.Id)];

    // Every row, page after page, following NextCursor.
    private static List<int> Walk(ProTableRequest first)
    {
        var ids = new List<int>();
        var request = first;
        for (var guard = 0; guard < 100; guard++)
        {
            var page = Query.Page(Source, request, CountMode.None);
            ids.AddRange(page.Items.Select(r => r.Id));
            if (page.NextCursor is null) return ids;
            request = Request(first.SortField, first.SortOrder, size: first.PageSize, cursor: page.NextCursor);
        }
        throw new InvalidOperationException("The cursor never reached the end.");
    }

    [Fact]
    public void NumberedPage_OrdersBySortThenId_AndCounts()
    {
        var page = Query.Page(Source, Request("score", SortDirection.Descending, current: 3, size: 10));

        var expected = Expected(rows => rows.OrderByDescending(r => r.Score).ThenByDescending(r => r.Id)).Skip(20).Take(10);
        Assert.Equal(expected, page.Items.Select(r => r.Id));
        Assert.Equal(237, page.Total);
        Assert.Equal(20, page.Offset);
        Assert.True(page.HasMore);
        Assert.NotNull(page.NextCursor);
    }

    [Fact]
    public void UnknownSortField_UsesTheDefaultSort()
    {
        var page = Query.Page(Source, Request("Id; DROP TABLE rows", size: 5));

        Assert.Equal(Expected(rows => rows.OrderByDescending(r => r.Date).ThenByDescending(r => r.Id)).Take(5), page.Items.Select(r => r.Id));
    }

    [Theory]
    [InlineData("name", SortDirection.Ascending)]
    [InlineData("name", SortDirection.Descending)]
    [InlineData("score", SortDirection.Ascending)]
    [InlineData("date", SortDirection.Descending)]
    [InlineData(null, SortDirection.None)]
    public void Cursor_WalksEveryRowOnce_InOrder(string? sort, SortDirection order)
    {
        var ids = Walk(Request(sort, order, size: 25));

        var all = Query.Apply(Source, Request(sort, order)).Select(r => r.Id).ToList();
        Assert.Equal(all, ids);
        Assert.Equal(Rows.Length, ids.Distinct().Count());
    }

    [Fact]
    public void NullableKey_FallsBackToOffset_AndStillWalksEveryRow()
    {
        // Rank is null for every fifth row: a keyset condition would skip them, so the cursor uses the offset.
        var ids = Walk(Request("rank", size: 30));

        Assert.Equal(Query.Apply(Source, Request("rank")).Select(r => r.Id), ids);
    }

    [Fact]
    public void LastPage_HasNoCursor()
    {
        var page = Query.Page(Source, Request(current: 12, size: 20));

        Assert.Equal(17, page.Items.Count);
        Assert.False(page.HasMore);
        Assert.Null(page.NextCursor);
    }

    [Fact]
    public void CountModeNone_LeavesTotalOut()
    {
        var page = Query.Page(Source, Request(size: 10), CountMode.None);

        Assert.Null(page.Total);
        Assert.True(page.HasMore);
    }

    [Theory]
    [InlineData("not-base64!!")]
    [InlineData("WzEsMiwzXQ")] // [1,2,3]: base64 of the wrong shape
    public void InvalidCursor_StartsAtTheNumberedPage(string cursor)
    {
        var page = Query.Page(Source, Request("score", current: 2, size: 10, cursor: cursor));

        Assert.Equal(10, page.Offset);
        Assert.Equal(Query.Page(Source, Request("score", current: 2, size: 10)).Items, page.Items);
    }

    [Fact]
    public void CursorFromAnotherSort_IsIgnored()
    {
        var byName = Query.Page(Source, Request("name", size: 10));

        var page = Query.Page(Source, Request("score", size: 10, cursor: byName.NextCursor));

        Assert.Equal(0, page.Offset);
        Assert.Equal(Query.Page(Source, Request("score", size: 10)).Items, page.Items);
    }

    [Fact]
    public void Search_OnlyRegisteredFields_AndCutsLongText()
    {
        var page = Query.Page(Source, Request(searches: new() { ["name"] = "Name 03", ["score"] = "6" }));

        Assert.All(page.Items, r => Assert.Contains("Name 03", r.Name));
        Assert.Equal(Rows.Count(r => r.Name.Contains("Name 03")), page.Total);

        var longText = new string('x', TableQuery<Row>.MaxSearchLength + 50);
        Assert.Equal(0, Query.Page(Source, Request(searches: new() { ["name"] = longText })).Total);
    }

    [Fact]
    public void SearchText_IgnoreCase_MatchesAnyCase()
    {
        var query = TableQuery<Row>.By(r => r.Id).SearchText("name", r => r.Name, ignoreCase: true);
        var rows = Rows.Append(new Row(999, null!, 0, default, null)).AsQueryable(); // a null name is skipped, not an error

        var page = query.Page(rows, Request(size: 300, searches: new() { ["name"] = "nAME 03" }));

        Assert.Equal(Rows.Count(r => r.Name.Contains("Name 03")), page.Total);
        Assert.Equal(0, Query.Page(Source, Request(searches: new() { ["name"] = "name 03" })).Total); // case-sensitive by default
    }

    [Fact]
    public void SearchText_ValueList_KeepsTheListedValues_AndTheLongList()
    {
        var rows = Rows.Append(new Row(998, "", 0, default, null)).Append(new Row(999, null!, 0, default, null)).AsQueryable();
        int Count(string search, TableQuery<Row>? query = null) =>
            (query ?? Query).Page(rows, Request(size: 300, searches: new() { ["name"] = search })).Total ?? -1;
        int Named(params string[] names) => Rows.Count(r => names.Contains(r.Name));

        Assert.Equal(Named("Name 03", "Name 05"), Count("=Name 03|Name 05"));
        Assert.Equal(Rows.Length + 2 - Named("Name 03"), Count("!=Name 03"));        // blanks stay
        Assert.Equal(Named("Name 03") + 2, Count("=Name 03|"));                      // "" is the null and the empty name
        Assert.Equal(Rows.Length, Count("!=|"));
        Assert.Equal(0, Count("=name 03|name 05"));                                   // exact, case-sensitive by default
        Assert.Equal(Named("Name 03", "Name 05"),
            Count("=name 03|name 05", TableQuery<Row>.By(r => r.Id).SearchText("name", r => r.Name, ignoreCase: true)));

        // Longer than MaxSearchLength: kept whole (up to ColumnSearch.MaxListLength), not cut mid-value.
        var all = Enumerable.Range(0, 17).Select(i => $"Name {i:D2}").ToList();
        var list = "=" + string.Join('|', all.Prepend(new string('x', TableQuery<Row>.MaxSearchLength)));
        Assert.Equal(Rows.Length, Count(list));
    }

    [Fact]
    public async Task PageAsync_MatchesPage_AndUsesTheCounter()
    {
        var counted = false;
        var page = await Query.PageAsync(Source, Request("name", size: 15), CountMode.Exact, (q, _) =>
        {
            counted = true;
            return Task.FromResult(q.Count());
        }, TestContext.Current.CancellationToken);

        Assert.True(counted);
        Assert.Equal(Query.Page(Source, Request("name", size: 15)).Items, page.Items);
        Assert.Equal(237, page.Total);
    }

    [Fact]
    public void DefaultSort_MustBeRegistered() =>
        Assert.Throws<ArgumentException>(() => TableQuery<Row>.By(r => r.Id).DefaultSort("name"));
}
