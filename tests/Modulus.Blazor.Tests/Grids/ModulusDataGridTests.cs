using System.ComponentModel.DataAnnotations;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Data;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>DataGridAuto (the columns a grid discovers on a row type) and the ModulusDataGrid that renders both engines.</summary>
public class ModulusDataGridTests : AsyncBunitContext
{
    private enum OrderStatus { Draft, Confirmed, Received }

    private sealed class Customer
    {
        public string Name { get; set; } = "";
        public string? City { get; set; }
    }

    private sealed class Order
    {
        public int Id { get; set; }
        public string Number { get; set; } = "";
        public Customer? Customer { get; set; }
        public DateOnly Ordered { get; set; }
        public DateOnly? Due { get; set; }
        public decimal Total { get; set; }
        public OrderStatus Status { get; set; }
        public bool Rushed { get; set; }
        [ScaffoldColumn(false)] public string Secret { get; set; } = "";
    }

    private static readonly Order[] Orders =
    [
        new() { Id = 1, Number = "PO-1", Customer = new Customer { Name = "Acme Fabrics", City = "Bologna" }, Ordered = new DateOnly(2026, 1, 10), Due = new DateOnly(2026, 3, 1), Total = 250m, Status = OrderStatus.Confirmed },
        new() { Id = 2, Number = "PO-2", Customer = new Customer { Name = "Acme Mills", City = "Como" }, Ordered = new DateOnly(2026, 2, 20), Total = 80m, Status = OrderStatus.Draft },
        new() { Id = 3, Number = "PO-3", Ordered = new DateOnly(2026, 5, 5), Total = 400m, Status = OrderStatus.Received, Rushed = true },
        new() { Id = 4, Number = "PO-4", Customer = new Customer { Name = "Zenith", City = "Prato" }, Ordered = new DateOnly(2026, 6, 15), Due = new DateOnly(2026, 9, 1), Total = 120.5m, Status = OrderStatus.Confirmed, Secret = "s3cret-value" },
    ];

    public ModulusDataGridTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<AntDesign.JsInterop.HtmlElement>(_ => true).SetResult(new AntDesign.JsInterop.HtmlElement());
        JSInterop.Setup<AntDesign.JsInterop.DomRect>(_ => true).SetResult(new AntDesign.JsInterop.DomRect());
        Services.AddAntDesign();
    }

    // ---- DataGridAuto: discovery, query, schema -----------------------------------------------------------------------------------

    [Fact]
    public void Discovery_MakesSimpleAndFlattenedColumns_SkipsTheIdAndTheHidden()
    {
        var columns = DataGridAuto.Columns<Order>();

        Assert.Equal(["number", "customerName", "customerCity", "ordered", "due", "total", "status", "rushed"], columns.Select(c => c.Key));
        Assert.Equal(["Number", "Customer name", "Customer city", "Ordered", "Due", "Total", "Status", "Rushed"], columns.Select(c => c.Title));
        Assert.Equal("Id", DataGridAuto.IdKey<Order>());
        Assert.Equal(1, DataGridAuto.RowKeyOf<Order>()(Orders[0])); // the row key, boxed
        Assert.True(columns.Single(c => c.Key == "total").AlignRight);
        Assert.DoesNotContain(columns, c => c.Key is "id" or "secret");
    }

    [Fact]
    public void Query_SearchesSortsRangesAndPages_InMemory()
    {
        var query = DataGridAuto.Query<Order>();
        var rows = Orders.AsQueryable();

        var page = query.Page(rows, new ProTableRequest { SortField = "total", SortOrder = SortDirection.Descending, Current = 2, PageSize = 2 });
        Assert.Equal(4, page.Total);
        Assert.Equal(["PO-4", "PO-2"], page.Items.Select(o => o.Number)); // 400, 250 | 120.5, 80

        var acme = query.Page(rows, Search("customerName", "Acme")); // flattened, null-safe
        Assert.Equal(["PO-1", "PO-2"], acme.Items.Select(o => o.Number));

        var exact = query.Page(rows, Search("number", "=PO-1"));
        Assert.Equal(["PO-1"], exact.Items.Select(o => o.Number));

        var without = query.Page(rows, Search("customerName", "!Acme")); // a null cell does not contain it, so it stays
        Assert.Equal(["PO-3", "PO-4"], without.Items.Select(o => o.Number));

        var big = query.Page(rows, Search("total", ">100"));
        Assert.Equal(["PO-1", "PO-3", "PO-4"], big.Items.Select(o => o.Number));

        var range = query.Page(rows, Search("total", "80..250"));
        Assert.Equal(["PO-1", "PO-2", "PO-4"], range.Items.Select(o => o.Number));

        var winter = query.Page(rows, Search("ordered", "2026-01-01..2026-02-28"));
        Assert.Equal(["PO-1", "PO-2"], winter.Items.Select(o => o.Number));

        var sinceMay = query.Page(rows, Search("ordered", "2026-05-01.."));
        Assert.Equal(["PO-3", "PO-4"], sinceMay.Items.Select(o => o.Number));

        var confirmed = query.Page(rows, Search("status", "confirmed")); // the enum's name, case-insensitive
        Assert.Equal(["PO-1", "PO-4"], confirmed.Items.Select(o => o.Number));

        var rushed = query.Page(rows, Search("rushed", "yes"));
        Assert.Equal(["PO-3"], rushed.Items.Select(o => o.Number));

        Assert.Equal(4, query.Page(rows, Search("total", "not-a-number")).Total); // unparseable matches everything
        Assert.Equal(4, query.Page(rows, Search("ordered", "tomorrow")).Total);

        static ProTableRequest Search(string key, string value) => new() { Searches = ColumnSearch.With(null, key, value), PageSize = 10 };
    }

    [Fact]
    public void Query_TranslatesToSql_WithParameters()
    {
        var query = DataGridAuto.Query<ShipmentRow>(defaultSort: "total", descending: true);
        var sql = query.Apply(new ShipmentDb().Rows, new ProTableRequest { Searches = ColumnSearch.With(null, "customer", "acme") }).ToQueryString();

        Assert.Contains("ORDER BY", sql);
        Assert.Contains("DESC", sql);
        Assert.Contains("LIKE", sql);        // the text search
        Assert.Contains("@", sql);           // parameterized, not SQL text
        Assert.DoesNotContain("'acme'", sql);
    }

    [Fact]
    public void Query_ValueLists_FromACheckboxList()
    {
        var query = DataGridAuto.Query<Order>();
        var rows = Orders.AsQueryable();
        List<string> Numbers(string key, string value) =>
            [.. query.Page(rows, new ProTableRequest { Searches = ColumnSearch.With(null, key, value), PageSize = 10 }).Items.Select(o => o.Number)];

        Assert.Equal(["PO-1", "PO-2"], Numbers("customerName", "=Acme Fabrics|Acme Mills"));
        Assert.Equal(["PO-1", "PO-2", "PO-3"], Numbers("customerName", "!=Zenith"));      // a blank (no customer) stays
        Assert.Equal(["PO-3", "PO-4"], Numbers("customerName", "=Zenith|"));               // "" is the blank cell
        Assert.Equal(["PO-1", "PO-2", "PO-4"], Numbers("customerName", "!=|"));
        Assert.Equal(["PO-1", "PO-3", "PO-4"], Numbers("status", "=Confirmed|Received"));  // enum labels
        Assert.Equal(["PO-3"], Numbers("rushed", "=Yes|"));                                // Yes/No
        Assert.Equal(["PO-1", "PO-2", "PO-4"], Numbers("rushed", "!=Yes"));
    }

    [Fact]
    public void Query_ValueList_TranslatesToSqlIn()
    {
        var query = DataGridAuto.Query<ShipmentRow>();
        var sql = query.Apply(new ShipmentDb().Rows, new ProTableRequest { Searches = ColumnSearch.With(null, "customer", "!=Acme|Zenith") }).ToQueryString();

        var where = sql[sql.IndexOf("WHERE", StringComparison.Ordinal)..];
        Assert.Contains("NOT IN (@", where); // the values as parameters, not SQL text
        Assert.DoesNotContain("LIKE", where);
        Assert.DoesNotContain("Acme", where);
    }

    private sealed class ShipmentRow
    {
        public int Id { get; set; }
        public string Customer { get; set; } = "";
        public decimal Total { get; set; }
        public DateOnly Sent { get; set; }
    }

    private sealed class ShipmentDb : DbContext
    {
        public DbSet<ShipmentRow> Rows => Set<ShipmentRow>();

        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseSqlServer("Server=.;Database=DataGrid;Trusted_Connection=True"); // never opened: ToQueryString only
    }

    [Fact]
    public void SchemaColumnsAndRecords_FeedTheSchemaEngine()
    {
        var columns = DataGridAuto.SchemaColumns<Order>();
        Assert.Equal("customerName", columns[1].Key);
        Assert.Equal(GridCellType.Number, columns.Single(c => c.Key == "total").Type);
        Assert.Equal(120, columns.Single(c => c.Key == "total").Width);
        Assert.Equal(130, columns.Single(c => c.Key == "ordered").Width);
        var status = columns.Single(c => c.Key == "status");
        Assert.Equal(GridCellType.Select, status.Type);
        Assert.Equal(["Draft", "Confirmed", "Received"], status.Options.Select(o => o.Value));

        var zenith = DataGridAuto.Records<Order>([Orders[3]])[0];
        Assert.Equal("4", zenith.Id);
        Assert.Equal("PO-4", zenith["number"]);
        Assert.Equal("Zenith", zenith["customerName"]);
        Assert.Equal("120.5", zenith["total"]);              // invariant, culture-free
        Assert.Equal("2026-06-15", zenith["ordered"]);
        Assert.Equal("2026-09-01", zenith["due"]);
        Assert.Equal("Confirmed", zenith["status"]);
        Assert.Equal("false", zenith["rushed"]);

        var none = DataGridAuto.Records<Order>([Orders[2]])[0];
        Assert.Null(none["customerName"]);                   // the flattened column of a null parent
        Assert.Null(none["due"]);
        Assert.Equal("true", none["rushed"]);
    }

    // ---- The typed engine, rendered -----------------------------------------------------------------------------------------------

    [Fact]
    public void TypedGrid_RendersTheDiscoveredColumnsAndCells()
    {
        var cut = Render<ModulusDataGrid<Order>>(p => p
            .Add(g => g.HeaderTitle, "Orders")
            .Add(g => g.Query, Orders.AsQueryable()));

        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("tbody tr.ant-table-row").Count));
        Assert.Contains("Orders", cut.Find(".m-table-toolbar-title").TextContent);
        // A header (title, sort carets, search icon) per discovered column; the hidden property stays out.
        Assert.Equal(DataGridAuto.Columns<Order>().Count, cut.FindAll("thead .m-th-search").Count);
        Assert.Contains("Customer name", cut.Markup);
        Assert.Contains("Acme Fabrics", cut.Markup);
        Assert.Contains("Zenith", cut.Markup);
        Assert.Contains("120.5", cut.Markup);               // decimal cell, formatted
        Assert.Contains("2026-06-15", cut.Markup);          // date cell
        Assert.Contains("Confirmed", cut.Markup);           // enum cell, its label
        Assert.DoesNotContain("s3cret", cut.Markup);
    }

    [Fact]
    public void TypedGrid_SortsByHeaderClick()
    {
        var cut = Render<ModulusDataGrid<Order>>(p => p.Add(g => g.Query, Orders.AsQueryable()));
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("tbody tr.ant-table-row").Count));

        cut.Find("thead .m-th-sort").Click(); // the first header (Number): ascending
        cut.WaitForAssertion(() => Assert.Contains("PO-1", FirstRow(cut)));
        cut.Find("thead .m-th-sort").Click(); // again: descending
        cut.WaitForAssertion(() => Assert.Contains("PO-4", FirstRow(cut)));

        static string FirstRow(IRenderedComponent<ModulusDataGrid<Order>> cut) =>
            cut.Find("tbody tr.ant-table-row").TextContent;
    }

    [Fact]
    public void TypedGrid_SearchesAColumn_FromTheFilterRow()
    {
        var cut = Render<ModulusDataGrid<Order>>(p => p.Add(g => g.Query, Orders.AsQueryable()));
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("tbody tr.ant-table-row").Count));

        cut.Find(".m-table-filter-toggle").Click(); // the filter row under the headers
        Assert.Equal(DataGridAuto.Columns<Order>().Count, cut.FindAll("input.m-th-filter").Count);
        cut.FindAll("input.m-th-filter")[1].Change("Acme"); // Customer name contains

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tbody tr.ant-table-row").Count));
        Assert.NotNull(cut.Find(".m-table-clear")); // the toolbar's Clear, now that a search is on
        cut.Find(".m-table-clear").Click();
        cut.WaitForAssertion(() => Assert.Equal(4, cut.FindAll("tbody tr.ant-table-row").Count));
    }

    [Fact]
    public void ClientGrid_PagesTheQueryInMemory()
    {
        var cut = Render<ModulusDataGrid<Order>>(p => p
            .Add(g => g.Query, Orders.AsQueryable())
            .Add(g => g.ServerSide, false)
            .Add(g => g.PageSize, 2));

        cut.WaitForAssertion(() => Assert.Equal(2, cut.FindAll("tbody tr.ant-table-row").Count));
        Assert.NotEmpty(cut.FindAll(".ant-pagination"));
    }

    // ---- The schema engine, rendered ----------------------------------------------------------------------------------------------

    [Fact]
    public void SchemaGrid_RendersTheNestedEngine_OverDiscoveredColumns()
    {
        var settings = new NestedGridSettings
        {
            Levels = [new NestedGridLevel { Title = "Rows", Columns = DataGridAuto.SchemaColumns<Order>() }],
        };
        var requests = new List<NestedGridRequest>();
        var cut = Render<ModulusDataGrid<GridRecord>>(p => p
            .Add(g => g.Settings, settings)
            .Add(g => g.HeaderTitle, "All orders")
            .Add(g => g.Load, r =>
            {
                requests.Add(r);
                return Task.FromResult(new NestedGridPage(DataGridAuto.Records<Order>(Orders), Orders.Length));
            }));

        Assert.Equal(new NestedGridRequest(0, null, null, null, false, 1, 5), Assert.Single(requests));
        Assert.Single(cut.FindAll(".m-nested-level-0"));
        Assert.Contains("All orders", cut.Find(".m-table-toolbar-title").TextContent);
        Assert.Contains("Customer name", cut.Markup);
        cut.WaitForAssertion(() => Assert.Contains("Acme Fabrics", cut.Markup));
    }

    [Fact]
    public void Guards_SeparateTheTwoEngines()
    {
        // Settings belong over GridRecord rows (the schema engine), not over a typed row.
        Assert.Throws<InvalidOperationException>(() => Render<ModulusDataGrid<Order>>(p => p
            .Add(g => g.Settings, new NestedGridSettings())));
        // The schema engine needs Load.
        Assert.Throws<InvalidOperationException>(() => Render<ModulusDataGrid<GridRecord>>(p => p
            .Add(g => g.Settings, new NestedGridSettings())));
        // The typed engine needs the queryable.
        Assert.Throws<InvalidOperationException>(() => Render<ModulusDataGrid<Order>>());
        // A type with nothing to discover needs own columns.
        Assert.Throws<InvalidOperationException>(() => Render<ModulusDataGrid<OnlyId>>(p => p
            .Add(g => g.Query, Array.Empty<OnlyId>().AsQueryable())));
    }

    private sealed class OnlyId
    {
        public int Id { get; set; }
    }
}
