using AntDesign;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.State;
using Xunit;
using SortDirection = Modulus.Ui.Abstractions.SortDirection;

namespace Modulus.Blazor.Tests;

/// <summary>A table's state in the URL: ProTableUrl's keys (shared with the MVC grids) and ProTable's UrlState.</summary>
public class ProTableUrlTests : BunitContext
{
    public ProTableUrlTests()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        JSInterop.Setup<AntDesign.JsInterop.HtmlElement>(_ => true).SetResult(new AntDesign.JsInterop.HtmlElement());
        JSInterop.Setup<AntDesign.JsInterop.DomRect>(_ => true).SetResult(new AntDesign.JsInterop.DomRect());
        Services.AddAntDesign();
    }

    private static IReadOnlyList<KeyValuePair<string, string?>> Q(string query) => SavedViews.Parse(query);

    [Fact]
    public void ReadWrite_RoundTrip_LeavingOutTheDefaults()
    {
        var request = ProTableUrl.Read(Q("sortField=Salary&sortOrder=Descending&s.Name=ann&pageSize=20&current=3&keyword=x"));
        Assert.Equal((3, 20, "Salary", SortDirection.Descending), (request.Current, request.PageSize, request.SortField, request.SortOrder));
        Assert.Equal("ann", request.Searches!["Name"]);

        Assert.Equal("sortField=Salary&sortOrder=Descending&s.Name=ann&pageSize=20&current=3", ProTableUrl.Query(ProTableUrl.Write(request)));
        Assert.Empty(ProTableUrl.Write(new ProTableRequest()));
        Assert.Equal("pageSize=10", ProTableUrl.Query(ProTableUrl.Write(new ProTableRequest(), pageSize: 25)));
    }

    [Fact]
    public void Read_IgnoresAMalformedSort_AndKnowsItsKeys()
    {
        var request = ProTableUrl.Read(Q("sortField=a-b&sortOrder=Ascending&current=x"));
        Assert.Null(request.SortField);
        Assert.Equal(1, request.Current);
        Assert.Null(ProTableUrl.Read(Q("sortField=Name&sortOrder=sideways")).SortField);

        Assert.True(ProTableUrl.IsTableKey("current"));
        Assert.True(ProTableUrl.IsTableKey("s.Name"));
        Assert.False(ProTableUrl.IsTableKey("keyword"));
        Assert.True(ProTableUrl.SameQuery(Q("a=1&b=2"), Q("b=2&a=1&c=")));
        Assert.False(ProTableUrl.SameQuery(Q("a=1"), Q("a=2")));
    }

    [Fact]
    public void SavedViews_LeaveOutThePageNumber()
    {
        Assert.Equal("keyword=x&sortField=Name", SavedViews.Canonical(Q("current=4&sortField=Name&keyword=x")));
    }

    private (IRenderedComponent<ProTable<GridRecord>> Cut, List<ProTableRequest> Requests, List<string> Read) RenderTable(Func<IEnumerable<KeyValuePair<string, string?>>>? pageQuery = null)
    {
        var requests = new List<ProTableRequest>();
        var read = new List<string>();
        var rows = Enumerable.Range(1, 50).Select(i => new GridRecord($"r{i}", new Dictionary<string, string?>())).ToList();
        var cut = Render<ProTable<GridRecord>>(p => p
            .Add(t => t.Request, r =>
            {
                requests.Add(r);
                return Task.FromResult(new ProTableResponse<GridRecord> { Data = rows.Skip(r.Skip).Take(r.PageSize).ToList(), Total = rows.Count });
            })
            .Add(t => t.RowKey, r => r.Id)
            .Add(t => t.UrlState, true)
            .Add(t => t.UrlQuery, pageQuery)
            .Add(t => t.OnUrlChanged, (IReadOnlyList<KeyValuePair<string, string?>> q) => read.Add(SavedViews.Canonical(q)))
            .Add(t => t.Columns, r => b =>
            {
                b.OpenComponent<PropertyColumn<GridRecord, string>>(0);
                b.AddAttribute(1, "Property", (System.Linq.Expressions.Expression<Func<GridRecord, string>>)(x => x.Id));
                b.CloseComponent();
            }));
        return (cut, requests, read);
    }

    [Fact]
    public async Task UrlState_OpensTheTableTheUrlNames_AndWritesEachChangeBesideThePagesKeys()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/tables/advanced?keyword=ann&sortField=Name&sortOrder=Descending&s.Office=Paris&current=2");

        var (cut, requests, read) = RenderTable(() => [new("keyword", "ann")]);

        Assert.Equal(["keyword=ann&s.Office=Paris&sortField=Name&sortOrder=Descending"], read);
        cut.WaitForAssertion(() => Assert.NotEmpty(requests));
        var first = requests[^1];
        Assert.Equal((2, "Name", SortDirection.Descending, "Paris"), (first.Current, first.SortField, first.SortOrder, first.Searches!["Office"]));
        Assert.Equal("http://localhost/tables/advanced?keyword=ann&sortField=Name&sortOrder=Descending&s.Office=Paris&current=2", nav.Uri); // nothing changed, nothing written

        // A header sort goes back to page 1 and writes the URL, keeping the page's own keys.
        await cut.InvokeAsync(() => cut.Instance.SortAsync("Name"));
        Assert.Equal("http://localhost/tables/advanced?keyword=ann&s.Office=Paris", nav.Uri);
        Assert.Single(read); // its own navigation is not read back
    }

    [Fact]
    public void UrlState_FollowsTheUrl_WhenItChangesFromOutside()
    {
        var nav = Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/tables/advanced");
        var (cut, requests, read) = RenderTable();
        cut.WaitForAssertion(() => Assert.NotEmpty(requests));

        // A saved view's link (or back and forward) changes the URL: the page reads its keys, then the table reloads.
        nav.NavigateTo("/tables/advanced?keyword=bo&sortField=Salary&sortOrder=Ascending");

        cut.WaitForAssertion(() => Assert.Equal("Salary", requests[^1].SortField));
        Assert.Equal(["", "keyword=bo&sortField=Salary&sortOrder=Ascending"], read);
        Assert.Equal(SortDirection.Ascending, cut.Instance.SortOf("Salary"));
    }
}
