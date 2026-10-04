using System.Data;
using System.Data.Common;
using AntDesign;
using Bunit;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Data;
using Modulus.Ui.Abstractions.Pivot;
using Xunit;
using SortDirection = Modulus.Ui.Abstractions.SortDirection;

namespace Modulus.Blazor.Tests;

/// <summary>Grouping table rows: the request and URL, TableQuery's groups and totals (in memory and as SQL), the group markup and ProTable.</summary>
public class GroupByTests : BunitContext
{
    public GroupByTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<AntDesign.JsInterop.HtmlElement>(_ => true).SetResult(new AntDesign.JsInterop.HtmlElement());
        JSInterop.Setup<AntDesign.JsInterop.DomRect>(_ => true).SetResult(new AntDesign.JsInterop.DomRect());
        Services.AddAntDesign();
    }

    public sealed class Row
    {
        public int Id { get; set; }
        public string Dept { get; set; } = "";
        public string Name { get; set; } = "";
        public decimal Salary { get; set; }
        public int Days { get; set; }
    }

    // Design ×3, Engineering ×4, Sales ×3.
    private static readonly Row[] Rows =
    [
        new() { Id = 1, Dept = "Engineering", Name = "Ada", Salary = 100, Days = 1 },
        new() { Id = 2, Dept = "Design", Name = "Ben", Salary = 60, Days = 2 },
        new() { Id = 3, Dept = "Sales", Name = "Cy", Salary = 50, Days = 3 },
        new() { Id = 4, Dept = "Engineering", Name = "Dee", Salary = 120, Days = 4 },
        new() { Id = 5, Dept = "Design", Name = "Eve", Salary = 70, Days = 5 },
        new() { Id = 6, Dept = "Engineering", Name = "Fay", Salary = 90, Days = 6 },
        new() { Id = 7, Dept = "Sales", Name = "Gus", Salary = 55, Days = 7 },
        new() { Id = 8, Dept = "Engineering", Name = "Hal", Salary = 110, Days = 8 },
        new() { Id = 9, Dept = "Design", Name = "Ivy", Salary = 80, Days = 9 },
        new() { Id = 10, Dept = "Sales", Name = "Jo", Salary = 45, Days = 10 },
    ];

    private static readonly TableQuery<Row> Query = TableQuery<Row>.By(r => r.Id)
        .Sort("dept", r => r.Dept).Sort("name", r => r.Name).Sort("salary", r => r.Salary)
        .Group("dept")
        .Total("salary", "Total salary", r => r.Salary)
        .Total("salary", "Average salary", r => r.Salary, PivotAggregate.Average)
        .Total("salary", "Top salary", r => r.Salary, PivotAggregate.Max, decimals: 0)
        .Total("days", "Days", r => r.Days);

    private static ProTableRequest Grouped(int current, int size, params SortKey[] sorts) =>
        new() { Current = current, PageSize = size, GroupBy = "dept", Sorts = sorts };

    [Fact]
    public void Request_AndUrl_CarryTheGroupField()
    {
        var request = new ProTableRequest { GroupBy = "dept" };
        Assert.Equal("dept", request.GroupBy);
        Assert.Null(new ProTableRequest { GroupBy = "x y" }.GroupBy);   // not a field key

        var query = ProTableUrl.Write(request).ToList();
        Assert.Contains(new KeyValuePair<string, string?>("groupBy", "dept"), query);
        Assert.Equal("dept", ProTableUrl.Read(query).GroupBy);
        Assert.True(ProTableUrl.IsTableKey("groupBy"));
    }

    [Fact]
    public void Page_SortsByTheGroupFirst_AndCarriesWholeGroupCountsAndTotals()
    {
        // Sorted by name within each department: Design (Ben, Eve, Ivy), Engineering (Ada, Dee, Fay, Hal), Sales (Cy, Gus, Jo).
        var page = Query.Page(Rows.AsQueryable(), Grouped(2, 4, new SortKey("name", SortDirection.Ascending)));

        Assert.Equal(["Dee", "Fay", "Hal", "Cy"], page.Items.Select(r => r.Name));   // rows 5–8
        var groups = page.Groups!;
        Assert.Equal(["Engineering", "Sales"], groups.Select(g => g.Label));
        Assert.Equal([3, 1], groups.Select(g => g.Rows));
        Assert.Equal([4, 3], groups.Select(g => g.Count));          // the whole group, not the page
        Assert.True(groups[0].ContinuesBefore);                      // Ada is on page 1
        Assert.False(groups[0].ContinuesAfter);
        Assert.False(groups[1].ContinuesBefore);
        Assert.True(groups[1].ContinuesAfter);                       // Gus and Jo are on page 3

        var engineering = groups[0].Totals;
        Assert.Equal(["Total salary", "Average salary", "Top salary", "Days"], engineering.Select(t => t.Title));
        Assert.Equal([420m, 105m, 120m, 19m], engineering.Select(t => t.Value));
        Assert.Equal(0, engineering[2].Decimals);

        Assert.Equal([780m, 78m, 120m, 55m], page.Totals!.Select(t => t.Value));   // every row
        Assert.True(TableGroups.HasFooter(groups[0]));
        Assert.False(TableGroups.HasFooter(groups[1]));
    }

    [Fact]
    public void Page_MarksAGroupThatGoesOn_AndFollowsTheGroupFieldsDirection()
    {
        var first = Query.Page(Rows.AsQueryable(), Grouped(1, 5));
        Assert.Equal(["Design", "Engineering"], first.Groups!.Select(g => g.Label));
        Assert.Equal([3, 2], first.Groups!.Select(g => g.Rows));
        Assert.False(first.Groups![0].ContinuesBefore);
        Assert.True(first.Groups![1].ContinuesAfter);               // Engineering goes on to page 2
        Assert.False(TableGroups.HasFooter(first.Groups![1]));

        var descending = Query.Page(Rows.AsQueryable(), Grouped(1, 10, new SortKey("dept", SortDirection.Descending), new SortKey("salary", SortDirection.Descending)));
        Assert.Equal(["Sales", "Engineering", "Design"], descending.Groups!.Select(g => g.Label));
        Assert.Equal(["Gus", "Cy", "Jo"], descending.Items.Take(3).Select(r => r.Name));   // then by salary, descending
    }

    [Fact]
    public void Page_IgnoresAFieldThatDoesNotGroup_AndRestrictDropsIt()
    {
        var page = Query.Page(Rows.AsQueryable(), new ProTableRequest { PageSize = 3, GroupBy = "name" });
        Assert.Null(page.Groups);
        Assert.NotNull(page.Totals);
        Assert.True(Query.CanGroup("DEPT"));

        var restricted = Query.Restrict(f => f != "dept" && f != "salary");
        Assert.False(restricted.CanGroup("dept"));
        var limited = restricted.Page(Rows.AsQueryable(), Grouped(1, 3));
        Assert.Null(limited.Groups);
        Assert.Equal(["Days"], limited.Totals!.Select(t => t.Title));
        Assert.Equal(55m, limited.Totals![0].Value);
    }

    [Fact]
    public void Response_CarriesGroupsAndTotals()
    {
        var response = Query.Page(Rows.AsQueryable(), Grouped(1, 4)).ToResponse();
        Assert.Equal(10, response.Total);
        Assert.Equal(["Design", "Engineering"], response.Groups!.Select(g => g.Label));
        Assert.Equal(4, response.Totals!.Count);
    }

    [Fact]
    public void Markup_HeaderFooterAndTotals()
    {
        var group = new TableGroup("Sales & Ops", "Sales & Ops", 3, [new TableTotal("salary", "Total salary", PivotAggregate.Sum, 150m)]) { Rows = 2, ContinuesBefore = true };

        var head = TableGroups.HeaderRow(group, "Department", 5);
        Assert.Contains("data-m-group-head=\"Sales &amp; Ops\"", head);
        Assert.Contains("colspan=\"5\"", head);
        Assert.Contains("aria-label=\"Collapse Sales &amp; Ops\"", head);
        Assert.Contains("<span class=\"m-group-field\">Department:</span> <strong>Sales &amp; Ops</strong>", head);
        Assert.Contains("<span class=\"m-group-count\">3 rows</span><span class=\"m-group-more\">continued</span>", head);
        Assert.Contains("<span class=\"m-group-total-title\">Total salary</span> <span class=\"m-group-total-value\">" + 150m.ToString("N2") + "</span>", head);

        Assert.Contains("Subtotal:", TableGroups.FooterRow(group, 5));
        Assert.Equal("", TableGroups.FooterRow(group with { ContinuesAfter = true }, 5));
        Assert.Equal(" data-m-group=\"Sales &amp; Ops\"", TableGroups.RowAttribute(group));
        Assert.Equal("", TableGroups.Totals(null));
        Assert.Contains("10 rows", TableGroups.Totals(group.Totals, 10));

        var spans = TableGroups.Spans([group with { Rows = 2 }, group with { Key = "b", Rows = 3 }], 4);
        Assert.Equal([(0, 1), (2, 3)], spans.Select(s => (s.First, s.Last)));
    }

    // ---- The SQL: a GROUP BY over the page's groups, and one over every row ----

    private sealed class RowDb(Interceptor interceptor) : DbContext
    {
        public DbSet<Row> Rows => Set<Row>();

        protected override void OnConfiguring(DbContextOptionsBuilder options) =>
            options.UseSqlServer("Server=.;Database=Groups;Trusted_Connection=True").AddInterceptors(interceptor);   // never opened
    }

    // Answers each command with the next table, without a database; keeps the SQL.
    private sealed class Interceptor(params DataTable[] answers) : DbCommandInterceptor, IDbConnectionInterceptor
    {
        private int _next;
        public List<string> Sql { get; } = [];

        public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Sql.Add(command.CommandText);
            return InterceptionResult<DbDataReader>.SuppressWithResult(answers[_next++].CreateDataReader());
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(ReaderExecuting(command, eventData, result));

        public override InterceptionResult<object> ScalarExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<object> result)
        {
            Sql.Add(command.CommandText);
            return InterceptionResult<object>.SuppressWithResult(10);
        }

        public InterceptionResult ConnectionOpening(DbConnection connection, ConnectionEventData eventData, InterceptionResult result) => InterceptionResult.Suppress();

        public ValueTask<InterceptionResult> ConnectionOpeningAsync(DbConnection connection, ConnectionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(InterceptionResult.Suppress());
    }

    private static DataTable Table(string[] columns, params object[][] rows)
    {
        var table = new DataTable();
        foreach (var c in columns) table.Columns.Add(c, typeof(object));
        foreach (var r in rows) table.Rows.Add(r);
        return table;
    }

    [Fact]
    public async Task Sql_GroupsThePageValues_AndTotalsEveryRow()
    {
        var interceptor = new Interceptor(
            Table(["Count"], [10]),
            Table(["Id", "Days", "Dept", "Name", "Salary"], [1, 1, "Design", "Ben", 60m], [2, 2, "Design", "Eve", 70m]),
            Table(["Key", "Count", "V0", "V1", "V2", "V3"], ["Design", 3, 210m, 210m, 80m, 16m]),
            Table(["Key", "Count", "V0", "V1", "V2", "V3"], [1, 10, 780m, 780m, 120m, 55m]));
        await using var db = new RowDb(interceptor);

        var page = await Query.PageAsync(db.Rows, Grouped(1, 2), countAsync: (q, ct) => q.CountAsync(ct));

        Assert.Equal(4, interceptor.Sql.Count);   // count, rows, the page's groups, every row
        var groups = interceptor.Sql[2];
        Assert.Contains("GROUP BY [r].[Dept]", groups);
        Assert.Contains("SUM([r].[Salary])", groups);
        Assert.Contains("MAX([r].[Salary])", groups);
        Assert.Contains("@", groups);             // the page's values are a parameter, never SQL text
        Assert.DoesNotContain("'Design'", groups);
        Assert.Contains("COUNT(*)", interceptor.Sql[3]);
        Assert.Contains("ORDER BY [r].[Dept], [r].[Id]", interceptor.Sql[1]);

        Assert.Equal(3, page.Groups!.Single().Count);
        Assert.Equal(70m, page.Groups![0].Totals[1].Value);   // average = 210 / 3
        Assert.Equal(78m, page.Totals![1].Value);
    }

    // ---- ProTable ----

    [Fact]
    public async Task ProTable_GroupsTheRows_WithHeadersSubtotalsTotals_AndCollapses()
    {
        var requests = new List<ProTableRequest>();
        var cut = Render<Modulus.Blazor.Components.Pro.ProTable<Row>>(p => p
            .Add(t => t.Request, r =>
            {
                requests.Add(r);
                return Task.FromResult(Query.Page(Rows.AsQueryable(), r).ToResponse());
            })
            .Add(t => t.RowKey, r => r.Id)
            .Add(t => t.PageSize, 10)
            .Add(t => t.Selectable, true)
            .Add(t => t.GroupFields, [new TableColumn("dept", "Department")])
            .Add(t => t.Columns, r => b =>
            {
                b.OpenComponent<PropertyColumn<Row, string>>(0);
                b.AddAttribute(1, "Property", (System.Linq.Expressions.Expression<Func<Row, string>>)(x => x.Name));
                b.CloseComponent();
                b.OpenComponent<PropertyColumn<Row, string>>(2);
                b.AddAttribute(3, "Property", (System.Linq.Expressions.Expression<Func<Row, string>>)(x => x.Dept));
                b.CloseComponent();
                b.OpenComponent<ActionColumn>(4);   // reads the row: it must never see a group row
                b.AddAttribute(5, "ChildContent", (Microsoft.AspNetCore.Components.RenderFragment)(c => c.AddContent(0, "Edit " + r.Name)));
                b.CloseComponent();
            }));
        cut.WaitForAssertion(() => Assert.Equal(10, cut.FindAll("tbody tr.ant-table-row").Count));
        Assert.Empty(cut.FindAll(".m-group-row"));
        Assert.Contains("Total salary", cut.Find(".m-table-totals").TextContent);   // totals without grouping too

        await cut.InvokeAsync(() => cut.Instance.GroupByAsync("DEPT"));
        cut.WaitForAssertion(() => Assert.Equal(3, cut.FindAll("tr.m-group-row").Count));
        Assert.Equal("dept", requests[^1].GroupBy);
        Assert.Equal("dept", cut.Instance.GroupBy);
        Assert.Contains("Group: Department", cut.Find(".m-table-group").TextContent);

        var heads = cut.FindAll("tr.m-group-row");
        Assert.Equal(["Design", "Engineering", "Sales"], heads.Select(h => h.QuerySelector("strong")!.TextContent));
        var columns = cut.FindAll("thead tr:first-child th").Count;
        Assert.Equal(columns.ToString(), heads[0].QuerySelector("td")!.GetAttribute("colspan"));
        Assert.Contains("Department:", heads[0].TextContent);
        Assert.Contains("3 rows", heads[0].TextContent);
        Assert.Equal(3, cut.FindAll("tr.m-group-footer").Count);
        Assert.Equal(13, cut.FindAll("tbody tr.ant-table-row").Count);   // 10 rows and 3 headers
        Assert.Equal(10, cut.FindAll("tbody tr.ant-table-row td:last-child").Count(td => td.TextContent.Trim().StartsWith("Edit ")));

        // The first group closes: its rows and subtotal go, its header stays.
        await cut.Find("tr.m-group-row .m-group-toggle").ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() => Assert.Equal(10, cut.FindAll("tbody tr.ant-table-row").Count));
        Assert.Equal("false", cut.Find("tr.m-group-row .m-group-toggle").GetAttribute("aria-expanded"));
        Assert.Equal("Expand Design", cut.Find("tr.m-group-row .m-group-toggle").GetAttribute("aria-label"));
        Assert.Equal(2, cut.FindAll("tr.m-group-footer").Count);

        await cut.InvokeAsync(() => cut.Instance.GroupByAsync(null));
        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll(".m-group-row")));
        Assert.Null(requests[^1].GroupBy);
    }
}
