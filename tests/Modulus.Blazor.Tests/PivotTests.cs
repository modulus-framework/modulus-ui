using System.Globalization;
using System.IO.Compression;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Modulus.Blazor.Components.Pro;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions.Pivot;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The pivot table: its state in the URL, the grouped query and its totals, the markup and the xlsx download.</summary>
public class PivotTests
{
    private static readonly PivotQuery<Shipment> Pivot = PivotTablePage.Pivot;

    private static PivotState Query(string query) => Pivot.State(PivotState.ReadQuery(query));

    private static PivotState State(string rows, string cols, string measures = "count,amount") =>
        Pivot.Normalize(new PivotState { Rows = Split(rows), Columns = Split(cols), Measures = Split(measures) }).ExpandAll();

    private static string[] Split(string s) => s.Split(',', StringSplitOptions.RemoveEmptyEntries);

    // State ----------------------------------------------------------------------------------------------------

    [Fact]
    public void State_RoundTripsThroughTheQueryString()
    {
        var state = new PivotState
        {
            Rows = ["city", "customer"], Columns = ["date:year"], Measures = ["amount", "count"],
            RowsAllDepth = 1, RowPaths = ["*/Berlin", "*/Paris/Acme Labs"], ColumnPaths = ["*"],
            SortMeasure = "amount", SortColumn = "*/2024", SortDescending = true,
        };

        var back = PivotState.Parse(PivotState.ReadQuery("?" + state.ToQueryString()))!;

        Assert.Equal(state.ToQueryString(), back.ToQueryString());
        Assert.Equal(["*/Berlin", "*/Paris/Acme Labs"], back.RowPaths);
        Assert.True(back.SortDescending);
    }

    [Fact]
    public void State_WithoutPivotKeys_IsTheDefault_AndUnknownFieldsAreDropped()
    {
        Assert.Same(Pivot.DefaultState, Query("status=Packed&city=Oslo"));

        var state = Query("rows=city,nope,city&cols=date,status:month&measures=bogus&sort=bogus");

        Assert.Equal(["city"], state.Rows);
        Assert.Equal(["date:month"], state.Columns); // a date without an interval groups by month
        Assert.Equal(["count", "amount"], state.Measures); // no known measure: the default ones
        Assert.Null(state.SortMeasure);
    }

    [Fact]
    public void ExpandBy_AddsTheGroupByBelowTheHeader_AndCollapseForgetsIt()
    {
        var state = Pivot.Normalize(new PivotState { Measures = ["count"] });

        state = state.ExpandBy(PivotAxis.Rows, PivotPath.Root, "city");
        Assert.Equal(["city"], state.Rows);
        Assert.True(state.IsExpanded(PivotAxis.Rows, PivotPath.Root));

        var berlin = PivotPath.Child(PivotPath.Root, "Berlin");
        state = state.ExpandBy(PivotAxis.Rows, berlin, "status");
        Assert.Equal(["city", "status"], state.Rows);
        Assert.True(state.IsExpanded(PivotAxis.Rows, berlin));
        Assert.False(state.IsExpanded(PivotAxis.Rows, PivotPath.Child(PivotPath.Root, "Paris")));

        // Closing the only header that used "status" drops it, as in Odoo.
        var closed = state.Collapse(PivotAxis.Rows, berlin);
        Assert.Equal(["city"], closed.Rows);

        // Closing "Total" starts the axis over.
        Assert.Empty(state.Collapse(PivotAxis.Rows, PivotPath.Root).Rows);
    }

    [Fact]
    public void ExpandAll_OpensEveryHeader_ButALaterGroupByOpensOnlyItsHeader()
    {
        var state = State("city,customer", "");
        var berlin = PivotPath.Child(PivotPath.Root, "Berlin");
        var acme = PivotPath.Child(berlin, "Acme Labs");

        Assert.True(state.IsExpanded(PivotAxis.Rows, berlin));

        state = state.ExpandBy(PivotAxis.Rows, acme, "status");
        Assert.True(state.IsExpanded(PivotAxis.Rows, acme));
        Assert.False(state.IsExpanded(PivotAxis.Rows, PivotPath.Child(berlin, "Hooli Group")));
        Assert.True(state.IsExpanded(PivotAxis.Rows, PivotPath.Child(PivotPath.Root, "Paris"))); // still open from Expand all

        // Above the expand-all depth a collapse is listed, and the group-bys stay.
        var closed = state.Collapse(PivotAxis.Rows, berlin);
        Assert.False(closed.IsExpanded(PivotAxis.Rows, berlin));
        Assert.Equal(3, closed.Rows.Count);
    }

    [Fact]
    public void Flip_SwapsTheAxes_AndToggleMeasureKeepsTheLastOne()
    {
        var state = State("city", "status").SortBy("amount", PivotPath.Root);

        var flipped = state.Flip();
        Assert.Equal(["status"], flipped.Rows);
        Assert.Equal(["city"], flipped.Columns);
        Assert.Null(flipped.SortMeasure);

        var one = state.ToggleMeasure("count").ToggleMeasure("amount");
        Assert.Equal(["amount"], one.Measures); // the last measure stays
        Assert.Equal(["amount", "average"], one.ToggleMeasure("average").Measures);
        Assert.Null(state.ToggleMeasure("amount").SortMeasure); // its sort goes with it
    }

    [Fact]
    public void SortBy_IsAscendingFirst_ThenToggles()
    {
        var state = State("city", "");

        var asc = state.SortBy("amount", PivotPath.Root);
        var desc = asc.SortBy("amount", PivotPath.Root);

        Assert.False(asc.SortDescending);
        Assert.True(desc.SortDescending);
    }

    // Query and totals -----------------------------------------------------------------------------------------

    [Fact]
    public void Run_TotalsEqualTheRawSums_AtEveryLevel()
    {
        var data = Shipments.Query.ToList();
        var result = Pivot.Run(Shipments.Query, State("city", "status"));

        Assert.Null(result.Error);
        Assert.Equal(data.Count, result.Value(PivotPath.Root, PivotPath.Root, "count"));
        Assert.Equal(data.Sum(s => s.Amount), result.Value(PivotPath.Root, PivotPath.Root, "amount"));
        foreach (var city in data.GroupBy(s => s.City))
        {
            var row = PivotPath.Child(PivotPath.Root, city.Key);
            Assert.Equal(city.Sum(s => s.Amount), result.Value(row, PivotPath.Root, "amount"));
            foreach (var status in city.GroupBy(s => s.Status))
            {
                var col = PivotPath.Child(PivotPath.Root, ((long)status.Key).ToString(CultureInfo.InvariantCulture));
                Assert.Equal(status.Count(), result.Value(row, col, "count"));
                Assert.Equal(status.Sum(s => s.Amount), result.Value(row, col, "amount"));
            }
        }
    }

    [Fact]
    public void Run_AveragesAndMaxima_RollUpCorrectly()
    {
        var data = Shipments.Query.ToList();
        var result = Pivot.Run(Shipments.Query, State("city", "", "average,largest"));

        Assert.Equal(data.Average(s => s.Amount), result.Value(PivotPath.Root, PivotPath.Root, "average"));
        Assert.Equal(data.Max(s => s.Amount), result.Value(PivotPath.Root, PivotPath.Root, "largest"));
        var oslo = data.Where(s => s.City == "Oslo").ToList();
        Assert.Equal(oslo.Average(s => s.Amount), result.Value(PivotPath.Child(PivotPath.Root, "Oslo"), PivotPath.Root, "average"));
    }

    [Fact]
    public void Run_BeyondMaxGroups_IsAnError_NotAHugeTable()
    {
        var small = PivotQuery<Shipment>.Create().Dimension("city", "City", s => s.City).Limit(3);

        var state = small.Normalize(new PivotState { Rows = ["city"], Measures = ["count"] });

        Assert.Null(small.Run(Shipments.Query, state).Error); // closed: one "Total" group
        var result = small.Run(Shipments.Query, state.ExpandAll());
        Assert.NotNull(result.Error);
        Assert.Contains("more than 3 groups", result.Error);
    }

    [Fact]
    public void Run_ReadsOnlyTheOpenHeaders_AndTheirTotalsStayExact()
    {
        var data = Shipments.Query.ToList();
        var oslo = PivotPath.Child(PivotPath.Root, "Oslo");
        // Rows by city then customer, only Oslo open: other cities' customers are not read.
        var state = Pivot.Normalize(new PivotState { Rows = ["city", "customer"], Columns = ["status"], Measures = ["count", "amount"], RowPaths = [PivotPath.Root, oslo], ColumnPaths = [PivotPath.Root] });

        var levels = Pivot.Queries(Shipments.Query, state);
        Assert.Equal(6, levels.Count); // row depths 0-2 × column depths 0-1
        var customers = levels.Single(l => l.RowDepth == 2 && l.ColumnDepth == 0).Groups.ToList();
        Assert.Equal(data.Where(s => s.City == "Oslo").Select(s => s.Customer).Distinct().Count(), customers.Count);

        var result = Pivot.Run(Shipments.Query, state);
        Assert.Null(result.Error);
        Assert.Equal(data.Sum(s => s.Amount), result.Value(PivotPath.Root, PivotPath.Root, "amount"));
        Assert.All(result.Rows.Children.Where(h => h.Key != "Oslo"), h => Assert.Empty(h.Children));
        foreach (var customer in data.Where(s => s.City == "Oslo").GroupBy(s => s.Customer))
            Assert.Equal(customer.Sum(s => s.Amount), result.Value(PivotPath.Child(oslo, customer.Key), PivotPath.Root, "amount"));
        var berlin = data.Where(s => s.City == "Berlin").ToList();
        Assert.Equal(berlin.Count, result.Value(PivotPath.Child(PivotPath.Root, "Berlin"), PivotPath.Root, "count"));

        // Expand all with Berlin closed: every city's customers but Berlin's.
        var all = Pivot.Normalize(state with { RowsAllDepth = 2, RowPaths = [PivotPath.Child(PivotPath.Root, "Berlin")] });
        var allCustomers = Pivot.Queries(Shipments.Query, all).Single(l => l.RowDepth == 2 && l.ColumnDepth == 0).Groups.ToList();
        Assert.Equal(data.Where(s => s.City != "Berlin").Select(s => (s.City, s.Customer)).Distinct().Count(), allCustomers.Count);
    }

    [Fact]
    public void Run_GroupsDatesByInterval_InOrder_WithTheirLabels()
    {
        var culture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        try
        {
            Shipment At(int id, DateOnly date) => new(id, "R" + id, "C", "Oslo", ShipmentStatus.Packed, 10, date);
            var data = new[] { At(1, new(2025, 2, 3)), At(2, new(2024, 11, 30)), At(3, new(2024, 12, 1)), At(4, new(2025, 2, 9)) }.AsQueryable();

            Labels("date:year", ["2024", "2025"]);
            Labels("date:quarter", ["Q4 2024", "Q1 2025"]);
            Labels("date:month", ["November 2024", "December 2024", "February 2025"]);
            Labels("date:week", ["W48 2024", "W06 2025"]);
            Labels("date:day", ["30 Nov 2024", "01 Dec 2024", "03 Feb 2025", "09 Feb 2025"]);

            void Labels(string groupBy, string[] expected)
            {
                var result = Pivot.Run(data, State(groupBy, "", "count"));
                Assert.Equal(expected, result.Rows.Children.Select(h => h.Label));
            }
        }
        finally
        {
            CultureInfo.CurrentCulture = culture;
        }
    }

    [Fact]
    public void Run_SortedByAMeasure_OrdersTheRowsWithinEachGroup()
    {
        var result = Pivot.Run(Shipments.Query, State("city", "").SortBy("amount", PivotPath.Root).SortBy("amount", PivotPath.Root));

        var values = result.Rows.Children.Select(h => result.Value(h.Path, PivotPath.Root, "amount")!.Value).ToList();
        Assert.Equal(values.OrderDescending(), values);
    }

    [Fact]
    public void Queries_TranslateToGroupBysInSql_WithTheOpenHeadersAsParameters()
    {
        using var db = new ShipmentDb();
        var state = Pivot.Normalize(new PivotState
        {
            Rows = ["city", "date:month"], Columns = ["status"], Measures = ["count", "amount", "average", "largest"],
            RowPaths = [PivotPath.Root, PivotPath.Child(PivotPath.Root, "Oslo")], ColumnPaths = [PivotPath.Root],
        });

        var levels = Pivot.Queries(db.Shipments, state);

        Assert.Equal(6, levels.Count);
        foreach (var level in levels)
        {
            var sql = level.Groups.ToQueryString();
            Assert.Contains("COUNT(", sql);
            Assert.Contains("SUM(", sql);
            Assert.Contains("MAX(", sql);
            if (level.RowDepth + level.ColumnDepth > 0) Assert.Contains("GROUP BY", sql);
        }
        var deepest = levels.Single(l => l.RowDepth == 2 && l.ColumnDepth == 1).Groups.ToQueryString();
        Assert.DoesNotContain("'Oslo'", deepest); // a parameter, not SQL text
        Assert.Contains("@", deepest);
    }

    private sealed class ShipmentDb : DbContext
    {
        public DbSet<Shipment> Shipments => Set<Shipment>();

        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseSqlServer("Server=.;Database=Pivot;Trusted_Connection=True"); // never opened: ToQueryString only

        protected override void OnModelCreating(ModelBuilder model) => model.Entity<Shipment>().HasKey(s => s.Id);
    }

    // Markup ---------------------------------------------------------------------------------------------------

    private static string Href(PivotState s) => "/p?" + s.ToQueryString();

    [Fact]
    public void Html_DrawsTheToolbar_HeadersAndValues_AsLinks()
    {
        var result = Pivot.Run(Shipments.Query, State("city", "status"));

        var html = PivotHtml.Render(result, Href, downloadHref: "/p/download", id: "pv");

        Assert.StartsWith("<div class=\"m-pivot\" id=\"pv\"><div class=\"m-pivot-toolbar\">", html);
        Assert.Contains("<span>Measures</span>", html);
        Assert.Contains("title=\"Flip axis\"", html);
        Assert.Contains("href=\"/p/download\" download hx-boost=\"false\" data-enhance-nav=\"false\"", html);
        // "Total" spans the status columns and its own; each value column has one cell per measure.
        var statuses = result.Columns.Children.Count;
        Assert.Contains($"colspan=\"{(statuses + 1) * 2}\" rowspan=\"1\"", html);
        Assert.Contains("class=\"m-pivot-header m-pivot-open\"", html);
        Assert.Contains("<th class=\"ant-table-cell m-pivot-row\" scope=\"row\" style=\"--m-depth:1\">", html);
        Assert.Contains(result.Format(result.Value(PivotPath.Root, PivotPath.Root, "amount"), result.Measures[1]), html);
    }

    [Fact]
    public void Html_LastLevelHeaders_OfferTheUnusedGroupBys_WithDateIntervals()
    {
        var result = Pivot.Run(Shipments.Query, State("status", ""));

        var html = PivotHtml.Render(result, Href);

        Assert.Contains("<details class=\"m-dropdown m-pivot-groupby\">", html);
        Assert.Contains(">Customer</a>", html);
        Assert.DoesNotContain(">Status</a>", html); // already grouped
        Assert.Contains(">Quarter</a>", html);
    }

    [Fact]
    public void Html_EmptyAndError_ShowAMessageInsteadOfTheTable()
    {
        var empty = Pivot.Run(Shipments.Query.Where(s => s.City == "Nowhere"), State("city", ""));

        Assert.Contains("No data to display", PivotHtml.Render(empty, Href));
        Assert.DoesNotContain("<table", PivotHtml.Render(empty, Href));
    }

    [Fact]
    public void Component_IsTheSharedMarkup()
    {
        using var ctx = new BunitContext();
        var result = Pivot.Run(Shipments.Query, State("city", "date:year"));
        Func<PivotState, string> href = Href;

        var cut = ctx.Render<AppPivotGrid>(p => p.Add(c => c.Result, result).Add(c => c.Href, href).Add(c => c.DownloadHref, "/d").Add(c => c.Id, "pv"));

        cut.MarkupMatches(PivotHtml.Render(result, href, null, "/d", "pv"));
    }

    [Fact]
    public void SamplePage_DrillsDownOnlyByStatusAndCity()
    {
        var page = PivotTablePage.Load(new ShipmentFilter(Keyword: "acme"), PivotState.ReadQuery("rows=city&cols=status&rx=*&cx=*"));
        var row = page.Result.Rows.Children.First(h => h.Key == "Oslo");
        var column = new PivotColumn(page.Result.Columns.Children.First(), false);

        Assert.Equal($"/tables/large?status={column.Header.Label}&city=Oslo&keyword=acme", page.CellHref(new PivotCell(row, column, "count")));

        var byCustomer = PivotTablePage.Load(new ShipmentFilter(), PivotState.ReadQuery("rows=customer&rx=*"));
        Assert.Null(byCustomer.CellHref(new PivotCell(byCustomer.Result.Rows.Children[0], new PivotColumn(byCustomer.Result.Columns, false), "count")));
    }

    // Download -------------------------------------------------------------------------------------------------

    [Fact]
    public void Xlsx_IsAWorkbook_WithMergedHeadersAndValues()
    {
        var result = Pivot.Run(Shipments.Query, State("city", "status"));

        using var zip = new ZipArchive(new MemoryStream(PivotXlsx.Write(result, "Ship/ments")));

        Assert.NotNull(zip.GetEntry("[Content_Types].xml"));
        Assert.NotNull(zip.GetEntry("xl/styles.xml"));
        var workbook = new StreamReader(zip.GetEntry("xl/workbook.xml")!.Open()).ReadToEnd();
        Assert.Contains("name=\"Shipments\"", workbook); // "/" is not allowed in a sheet name
        var sheet = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open()).ReadToEnd();
        Assert.Contains("<mergeCell ref=\"A1:A3\"/>", sheet); // the corner, over both header rows and the measure row
        Assert.Contains(">Total</t>", sheet);
        Assert.Contains($"<v>{Shipments.Count}</v>", sheet);
        System.Xml.Linq.XDocument.Parse(sheet); // well-formed
    }
}
