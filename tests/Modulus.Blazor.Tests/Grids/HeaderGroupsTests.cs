using AntDesign;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Ui.Abstractions;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>Multi-level headers: the bands over the shown columns, their markup, and ProTable's band rows.</summary>
public class HeaderGroupsTests : BunitContext
{
    public HeaderGroupsTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<AntDesign.JsInterop.HtmlElement>(_ => true).SetResult(new AntDesign.JsInterop.HtmlElement());
        JSInterop.Setup<AntDesign.JsInterop.DomRect>(_ => true).SetResult(new AntDesign.JsInterop.DomRect());
        Services.AddAntDesign();
    }

    private static readonly HeaderGroup[] Groups =
    [
        new("Job", "position", "dept"),
        new("Pay", "salary", "bonus"),
        new("Package", "salary", "bonus", "status") { Level = 1 },
    ];

    [Fact]
    public void Bands_MergeNeighbours_LeaveTheRestEmpty_AndIgnoreCase()
    {
        string?[] columns = [null, null, "Name", "Position", "Dept", "Salary", "Bonus", "Status"];
        Assert.Equal(
            [new HeaderBand(null, 3), new HeaderBand("Job", 2), new HeaderBand("Pay", 2), new HeaderBand(null, 1)],
            HeaderGroups.Bands(columns, Groups));
        Assert.Equal(
            [new HeaderBand(null, 5), new HeaderBand("Package", 3)],
            HeaderGroups.Bands(columns, Groups, level: 1));
        Assert.Equal([1, 0], HeaderGroups.Levels(Groups));
    }

    [Fact]
    public void Bands_FollowTheShownColumns()
    {
        // Dept hidden: Job narrows; a column between two of a band's columns splits it.
        Assert.Equal([new HeaderBand(null, 1), new HeaderBand("Job", 1), new HeaderBand("Pay", 1)],
            HeaderGroups.Bands(["name", "position", "salary"], Groups));
        Assert.Equal([new HeaderBand("Job", 1), new HeaderBand(null, 1), new HeaderBand("Job", 1)],
            HeaderGroups.Bands(["position", "name", "dept"], Groups));
        // Every column of a band hidden: no band.
        Assert.Equal([new HeaderBand(null, 2)], HeaderGroups.Bands(["name", "status"], Groups));
    }

    [Fact]
    public void Rows_OutermostFirst_WithSpansAndEncodedTitles()
    {
        var html = HeaderGroups.Rows(["name", "position", "dept", "salary", "bonus", "status"],
            [.. Groups, new HeaderGroup("<R&D>", "name") { Level = 2 }]);
        var rows = html.Split("</tr>", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, rows.Length);
        Assert.Contains("&lt;R&amp;D&gt;", rows[0]);
        Assert.Contains("""<th class="ant-table-cell m-header-band" scope="colgroup" colspan="3" data-m-span="3">Package</th>""", rows[1]);
        Assert.Contains("""<td class="ant-table-cell m-header-band m-header-band-empty" colspan="3" data-m-span="3"></td>""", rows[1]);
        Assert.Contains("""colspan="2" data-m-span="2">Job</th>""", rows[2]);

        Assert.Equal("", HeaderGroups.Rows(["name"], null));
        Assert.Equal("", HeaderGroups.Rows(["name"], Groups));   // no band over a shown column
    }

    public sealed class Row
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
        public string Position { get; set; } = "";
        public string Dept { get; set; } = "";
        public decimal Salary { get; set; }
    }

    [Fact]
    public void ProTable_RendersBandsAboveTheColumnTitles_AndNarrowsThemForHiddenColumns()
    {
        Row[] rows = [new() { Id = 1, Name = "Ada", Position = "Engineer", Dept = "R&D", Salary = 100 }];
        var hideDept = false;
        Microsoft.AspNetCore.Components.RenderFragment<Row> Columns() => r => b =>
        {
            b.OpenComponent<PropertyColumn<Row, string>>(0);
            b.AddAttribute(1, "Property", (System.Linq.Expressions.Expression<Func<Row, string>>)(x => x.Name));
            b.CloseComponent();
            b.OpenComponent<PropertyColumn<Row, string>>(2);
            b.AddAttribute(3, "Property", (System.Linq.Expressions.Expression<Func<Row, string>>)(x => x.Position));
            b.CloseComponent();
            b.OpenComponent<PropertyColumn<Row, string>>(4);
            b.AddAttribute(5, "Property", (System.Linq.Expressions.Expression<Func<Row, string>>)(x => x.Dept));
            b.AddAttribute(6, "Hidden", hideDept);
            b.CloseComponent();
            b.OpenComponent<PropertyColumn<Row, decimal>>(7);
            b.AddAttribute(8, "Property", (System.Linq.Expressions.Expression<Func<Row, decimal>>)(x => x.Salary));
            b.CloseComponent();
        };
        var cut = Render<Modulus.Blazor.Components.Pro.ProTable<Row>>(p => p
            .Add(t => t.Request, _ => Task.FromResult(new ProTableResponse<Row> { Data = rows, Total = 1 }))
            .Add(t => t.RowKey, r => r.Id)
            .Add(t => t.Selectable, true)
            .Add(t => t.HeaderGroups, [new HeaderGroup("Job", nameof(Row.Position), nameof(Row.Dept)), new HeaderGroup("Pay", nameof(Row.Salary))])
            .Add(t => t.Columns, Columns()));

        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("thead tr.m-header-bands")));
        var heads = cut.FindAll("thead > tr");
        Assert.Equal(2, heads.Count);
        Assert.Contains("m-header-bands", heads[0].ClassName);
        var bands = heads[0].Children;
        Assert.Equal(["2", "2", "1"], bands.Select(c => c.GetAttribute("colspan")));   // checkbox + Name, Job, Pay
        Assert.Equal(["", "Job", "Pay"], bands.Select(c => c.TextContent));
        Assert.Equal(heads[1].QuerySelectorAll("th").Length, bands.Sum(c => int.Parse(c.GetAttribute("colspan")!)));
        Assert.Contains("Ada", cut.Find("tbody tr.ant-table-row").TextContent);

        hideDept = true;
        cut.Render(p => p.Add(t => t.Columns, Columns()));
        cut.WaitForAssertion(() => Assert.Equal(["2", "1", "1"], cut.FindAll("thead tr.m-header-bands > *").Select(c => c.GetAttribute("colspan"))));
    }

    [Fact]
    public void EditableGrid_RendersBandsOverItsColumns_AndFollowsAHiddenColumn()
    {
        List<GridColumn> columns =
        [
            new() { Key = "name", Title = "Name" },
            new() { Key = "budget", Title = "Budget", Type = GridCellType.Currency },
            new() { Key = "due", Title = "Due", Type = GridCellType.Date },
            new() { Key = "owner", Title = "Owner" },
        ];
        var row = new GridRecord("r1");
        row["name"] = "Portal";
        var cut = Render<Modulus.Blazor.Components.Pro.EditableGrid>(p => p
            .Add(g => g.Columns, columns).Add(g => g.Rows, new List<GridRecord> { row })
            .Add(g => g.HeaderGroups, [new HeaderGroup("Plan", "budget", "due")]));

        var heads = cut.FindAll("thead > tr");
        Assert.Equal(2, heads.Count);
        // Drag handle + checkbox + Name, Plan, Owner + delete.
        Assert.Equal(["3", "2", "2"], heads[0].Children.Select(c => c.GetAttribute("colspan")));
        Assert.Equal("Plan", heads[0].Children[1].TextContent);
        Assert.Equal(heads[1].QuerySelectorAll("th").Length, heads[0].Children.Sum(c => int.Parse(c.GetAttribute("colspan")!)));

        columns[2].Hidden = true;
        cut.Render(p => p.Add(g => g.Columns, columns));
        Assert.Equal(["3", "1", "2"], cut.FindAll("thead tr.m-header-bands > *").Select(c => c.GetAttribute("colspan")));
    }
}
