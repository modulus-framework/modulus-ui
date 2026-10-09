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
    public void ExpandAll_BringsBackTheLevelsACollapseDropped_AsOdoosFullGroupBys()
    {
        var berlin = PivotPath.Child(PivotPath.Root, "Berlin");
        var state = Pivot.DefaultState.ExpandBy(PivotAxis.Rows, berlin, "customer");
        Assert.Equal(["city", "customer"], state.Rows);

        // Closing Berlin drops "customer" from the axis, but Expand all remembers it, also through the URL.
        var closed = state.Collapse(PivotAxis.Rows, berlin);
        Assert.Equal(["city"], closed.Rows);
        Assert.Contains("rf=city%2Ccustomer", closed.ToQueryString());
        closed = Query(closed.ToQueryString());
        Assert.True(closed.CanExpandAll());

        var all = closed.ExpandAll();
        Assert.Equal(["city", "customer"], all.Rows);
        Assert.True(all.IsExpanded(PivotAxis.Rows, berlin));
        Assert.True(all.IsExpanded(PivotAxis.Rows, PivotPath.Child(PivotPath.Root, "Paris")));
        Assert.DoesNotContain("rf=", all.ToQueryString());

        // Another group-by below the city replaces the remembered one.
        Assert.Equal(["city", "status"], closed.ExpandBy(PivotAxis.Rows, berlin, "status").ExpandAll().Rows);
        // Collapsing "Total" keeps the levels for Expand all too.
        Assert.Equal(["city", "customer"], state.Collapse(PivotAxis.Rows, PivotPath.Root).ExpandAll().Rows);
    }

    [Fact]
    public void ExpandAll_IsDisabled_WhenEveryHeaderIsOpen()
    {
        // The default (City × Year, both "Total"s open) has nothing more to open.
        Assert.False(Pivot.DefaultState.CanExpandAll());
        Assert.False(State("city,customer", "date:year").CanExpandAll());
        var berlin = PivotPath.Child(PivotPath.Root, "Berlin");
        Assert.True(Pivot.DefaultState.ExpandBy(PivotAxis.Rows, berlin, "customer").CanExpandAll());

        var html = PivotHtml.Render(Pivot.Run(Shipments.Query, Pivot.DefaultState), s => "?" + s.ToQueryString());
        Assert.Contains("<button type=\"button\" class=\"ant-btn ant-btn-default ant-btn-icon-only\" disabled title=\"Every header is expanded\" aria-label=\"Expand all\">", html);
        Assert.DoesNotContain("title=\"Expand all\"", html);
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

    // Derived measures -----------------------------------------------------------------------------------------

    private sealed record Sale(string Region, decimal Profit, decimal Sales);

    private static readonly Sale[] Sales =
    [
        new("A", 10m, 100m), new("A", 5m, 50m), new("B", 3m, 0m),
    ];

    private static PivotQuery<Sale> SalesPivot() => PivotQuery<Sale>.Create()
        .Dimension("region", "Region", s => s.Region)
        .Measure("profit", "Profit", s => s.Profit)
        .Measure("sales", "Sales", s => s.Sales)
        .Ratio("margin", "Margin", "profit", "sales", decimals: 1, percent: true)
        .Ratio("per_record", "Profit per record", "profit", "count");

    [Fact]
    public void Ratio_IsWorkedOutFromTheSums_NotAddedUpFromTheCells()
    {
        var pivot = SalesPivot();
        var state = pivot.Normalize(new PivotState { Rows = ["region"], Measures = ["margin", "sales"] }).ExpandAll();
        var result = pivot.Run(Sales.AsQueryable(), state);

        // Region A: 15 / 150 = 10%. Region B has no sales, so no margin. The total is 18 / 150 = 12%, not 10% + nothing.
        Assert.Equal(0.1m, result.Value(PivotChild("A"), PivotPath.Root, "margin"));
        Assert.Null(result.Value(PivotChild("B"), PivotPath.Root, "margin"));
        Assert.Equal(0.12m, result.Value(PivotPath.Root, PivotPath.Root, "margin"));
        Assert.Equal(150m, result.Value(PivotChild("A"), PivotPath.Root, "sales"));
    }

    [Fact]
    public void Ratio_WithCount_AndPercentFormatting()
    {
        var pivot = SalesPivot();
        var state = pivot.Normalize(new PivotState { Measures = ["per_record", "margin"] });
        var result = pivot.Run(Sales.AsQueryable(), state);

        Assert.Equal(6m, result.Value(PivotPath.Root, PivotPath.Root, "per_record")); // 18 profit over 3 records
        var margin = result.Measures.Single(m => m.Key == "margin");
        Assert.True(margin.Percent);
        Assert.EndsWith("%", result.Format(0.12m, margin));
        Assert.Contains("12", result.Format(0.12m, margin));
        Assert.Equal("", result.Format(null, margin));
    }

    [Fact]
    public void Ratio_StoresOnlyTheSumsItNeeds_AndIsDroppedWithAHiddenMeasure()
    {
        var pivot = SalesPivot();
        var state = pivot.Normalize(new PivotState { Measures = ["margin"] });
        Assert.Equal(["margin"], state.Measures);

        // The query sums profit and sales for the ratio; restricting sales hides the ratio and the state drops it.
        var restricted = pivot.Restrict(k => k != "sales");
        Assert.DoesNotContain(restricted.Measures, m => m.Key == "margin");
        Assert.DoesNotContain(restricted.Measures, m => m.Key == "sales");
        Assert.Equal(["count"], restricted.Normalize(new PivotState { Measures = ["margin"] }).Measures);
    }

    [Fact]
    public void Ratio_RejectsUnknownOrDuplicateMeasures()
    {
        var pivot = SalesPivot();
        Assert.Throws<ArgumentException>(() => pivot.Ratio("x", "X", "nope", "sales"));
        Assert.Throws<ArgumentException>(() => pivot.Ratio("x", "X", "profit", "margin")); // a ratio is not a measure to divide by
        Assert.Throws<ArgumentException>(() => pivot.Ratio("margin", "Again", "profit", "sales"));
    }

    [Fact]
    public void Ratio_ShowsAPercentInTheWorkbook()
    {
        var pivot = SalesPivot();
        var result = pivot.Run(Sales.AsQueryable(), pivot.Normalize(new PivotState { Rows = ["region"], Measures = ["margin"] }).ExpandAll());
        using var zip = new ZipArchive(new MemoryStream(PivotXlsx.Write(result, "Sales")));
        using var reader = new StreamReader(zip.GetEntry("xl/worksheets/sheet1.xml")!.Open());
        var sheet = reader.ReadToEnd();

        // The percent style (7, or 8 for totals): the fraction 0.1 stored, shown as 10.00%.
        Assert.Matches("s=\"[78]\"><v>0\\.1</v>", sheet);
    }

    private static string PivotChild(string key) => PivotPath.Child(PivotPath.Root, key);

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
        var statement = deepest[deepest.IndexOf("SELECT", StringComparison.Ordinal)..]; // the DECLARE prefix names the parameter's value
        Assert.DoesNotContain("'Oslo'", statement); // a parameter, not SQL text
        Assert.Contains("@", statement);
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
