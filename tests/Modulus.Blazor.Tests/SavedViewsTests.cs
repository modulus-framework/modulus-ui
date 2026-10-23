using System.Security.Claims;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Pro;
using Modulus.Samples.Shared;
using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Data;
using Modulus.Ui.Abstractions.State;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>Saved views, the UI state store and field access: per-user state and permission-aware fields.</summary>
public class SavedViewsTests
{
    private static IReadOnlyList<KeyValuePair<string, string?>> Q(string query) => SavedViews.Parse(query);

    private static ClaimsPrincipal User(string name, params string[] roles) =>
        new(new ClaimsIdentity([new Claim(ClaimTypes.Name, name), .. roles.Select(r => new Claim(ClaimTypes.Role, r))], "test"));

    // Store and user ----------------------------------------------------------------------------------------------

    [Fact]
    public void UserKey_IsTheIdThenTheName_AndEmptyWhenAnonymous()
    {
        Assert.Equal("", UiUser.Key(null));
        Assert.Equal("", UiUser.Key(new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "ana")]))));
        Assert.Equal("ana", UiUser.Key(User("ana")));
        Assert.Equal("42", UiUser.Key(new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Name, "ana"), new Claim(ClaimTypes.NameIdentifier, "42")], "test"))));
    }

    [Fact]
    public async Task MemoryStore_KeepsStatePerUser_NothingForAnonymous_AndRefusesHugeValues()
    {
        var store = new MemoryUiStateStore();
        await store.SetAsync("ana", "k", "1", cancellationToken: CancellationToken.None);
        await store.SetAsync("bo", "k", "2", cancellationToken: CancellationToken.None);
        await store.SetAsync("", "k", "3", cancellationToken: CancellationToken.None);

        Assert.Equal("1", await store.GetAsync("ana", "k", cancellationToken: CancellationToken.None));
        Assert.Equal("2", await store.GetAsync("bo", "k", cancellationToken: CancellationToken.None));
        Assert.Null(await store.GetAsync("", "k", cancellationToken: CancellationToken.None));

        await store.SetAsync("ana", "k", null, cancellationToken: CancellationToken.None);
        Assert.Null(await store.GetAsync("ana", "k", cancellationToken: CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(async () => await store.SetAsync("ana", "k", new string('x', MemoryUiStateStore.MaxValueLength + 1), cancellationToken: CancellationToken.None));
    }

    // Saved views -------------------------------------------------------------------------------------------------

    [Fact]
    public void Canonical_SortsKeys_DropsEmptyAndTransientKeys_AndEscapes()
    {
        Assert.Equal("a=1&b=x%20y&b=2", SavedViews.Canonical(Q("?b=x+y&page=3&a=1&c=&b=2&cursor=abc&views-error=oops")));
        Assert.Equal(SavedViews.Canonical(Q("a=1&b=2")), SavedViews.Canonical(Q("b=2&a=1&page=9")));
        Assert.Equal("", SavedViews.Canonical(Q("page=2")));
    }

    [Fact]
    public async Task Save_ReplacesByName_MarksOneDefault_AndListsInOrder()
    {
        var views = new SavedViews(new MemoryUiStateStore());
        Assert.Null(await views.SaveAsync("ana", "/p", "By city", Q("rows=city"), cancellationToken: CancellationToken.None));
        Assert.Null(await views.SaveAsync("ana", "/p", "By year", Q("cols=date:year"), isDefault: true, cancellationToken: CancellationToken.None));
        Assert.Null(await views.SaveAsync("ana", "/p", " by CITY ", Q("rows=city&page=4"), isDefault: true, cancellationToken: CancellationToken.None));

        var list = await views.ListAsync("ana", "/p", cancellationToken: CancellationToken.None);

        Assert.Equal(["by CITY", "By year"], list.Select(v => v.Name));
        Assert.Equal("rows=city", list[0].Query);
        Assert.Equal([true, false], list.Select(v => v.IsDefault));
        Assert.Empty(await views.ListAsync("bo", "/p", cancellationToken: CancellationToken.None));
        Assert.Empty(await views.ListAsync("ana", "/other", cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task Save_RefusesAnonymous_NoName_LongNames_AndTooManyViews()
    {
        var views = new SavedViews(new MemoryUiStateStore());
        Assert.NotNull(await views.SaveAsync("", "/p", "A", Q("a=1"), cancellationToken: CancellationToken.None));
        Assert.NotNull(await views.SaveAsync("ana", "/p", "  ", Q("a=1"), cancellationToken: CancellationToken.None));
        Assert.NotNull(await views.SaveAsync("ana", "/p", new string('n', SavedViews.MaxName + 1), Q("a=1"), cancellationToken: CancellationToken.None));
        Assert.NotNull(await views.SaveAsync("ana", "/p", "Big", Q("a=" + new string('x', SavedViews.MaxQuery)), cancellationToken: CancellationToken.None));

        for (var i = 0; i < SavedViews.MaxViews; i++) Assert.Null(await views.SaveAsync("ana", "/p", $"V{i}", Q($"a={i}"), cancellationToken: CancellationToken.None));
        Assert.NotNull(await views.SaveAsync("ana", "/p", "One more", Q("a=x"), cancellationToken: CancellationToken.None));
        Assert.Null(await views.SaveAsync("ana", "/p", "v3", Q("a=y"), cancellationToken: CancellationToken.None)); // replacing one is still fine
    }

    [Fact]
    public async Task DefaultAndDelete_ChangeTheDefaultUrl()
    {
        var views = new SavedViews(new MemoryUiStateStore());
        await views.SaveAsync("ana", "/p", "Mine", Q("rows=city"), cancellationToken: CancellationToken.None);
        Assert.Null(await views.DefaultUrlAsync("ana", "/p", "/tables/p", [], cancellationToken: CancellationToken.None));

        await views.SetDefaultAsync("ana", "/p", "mine", cancellationToken: CancellationToken.None);
        Assert.Equal("/tables/p?rows=city", await views.DefaultUrlAsync("ana", "/p", "/tables/p", Q("page=2"), cancellationToken: CancellationToken.None));
        Assert.Null(await views.DefaultUrlAsync("ana", "/p", "/tables/p", Q("rows=status"), cancellationToken: CancellationToken.None)); // the URL has a layout

        await views.SetDefaultAsync("ana", "/p", null, cancellationToken: CancellationToken.None);
        Assert.Null(await views.DefaultAsync("ana", "/p", cancellationToken: CancellationToken.None));

        await views.SetDefaultAsync("ana", "/p", "Mine", cancellationToken: CancellationToken.None);
        await views.DeleteAsync("ana", "/p", "MINE", cancellationToken: CancellationToken.None);
        Assert.Empty(await views.ListAsync("ana", "/p", cancellationToken: CancellationToken.None));
        Assert.Null(await views.DefaultUrlAsync("ana", "/p", "/tables/p", [], cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task List_OfAValueItCannotRead_IsEmpty()
    {
        var store = new MemoryUiStateStore();
        await store.SetAsync("ana", "views:/p", "{not json", cancellationToken: CancellationToken.None);
        Assert.Empty(await new SavedViews(store).ListAsync("ana", "/p", cancellationToken: CancellationToken.None));
    }

    [Fact]
    public void Html_MarksTheCurrentView_AndPostsEveryActionThroughOneForm()
    {
        var model = new SavedViewsModel("pv", "/p", "/tables/p", Q("b=2&a=1&page=3"),
            [new SavedView("All", ""), new SavedView("Mine <b>", "a=1&b=2", IsDefault: true)], CanSave: true, Error: "Too <long>");

        var html = SavedViewsHtml.Render(model, ("__RequestVerificationToken", "tok"));

        Assert.Same(model.Views[1], model.Current);
        Assert.Contains("<details class=\"m-dropdown m-views\" id=\"pv\" open>", html);
        Assert.Contains("<span>Mine &lt;b&gt;</span>", html);
        Assert.Contains("href=\"/tables/p?a=1&amp;b=2\" aria-current=\"true\"", html);
        Assert.Contains("href=\"/tables/p\"><span>All</span>", html);
        Assert.Contains("form=\"pv-form\" name=\"op\" value=\"delete:Mine &lt;b&gt;\"", html);
        Assert.Contains("value=\"default:All\"", html);
        Assert.Contains("aria-pressed=\"true\"", html);
        Assert.Contains("<div class=\"m-views-error\" role=\"alert\">Too &lt;long&gt;</div>", html);
        Assert.Contains("<form id=\"pv-form\" method=\"post\" action=\"/_modulus/views\" hidden>", html);
        Assert.Contains("name=\"query\" value=\"a=1&amp;b=2\"", html);
        Assert.Contains("name=\"__RequestVerificationToken\" value=\"tok\"", html);
        Assert.Single(html.Split("<form").Skip(1)); // one form, after the dropdown's panel: nothing nests in it
    }

    [Fact]
    public void Html_ForAnonymous_HasNoSaveForm()
    {
        var html = SavedViewsHtml.Render(new SavedViewsModel("v", "/p", "/p", [], [], CanSave: false));

        Assert.Contains("Sign in to save views.", html);
        Assert.DoesNotContain("m-views-save", html);
        Assert.Contains("<span>Views</span>", html);
    }

    // Field access ------------------------------------------------------------------------------------------------

    [Fact]
    public void FieldAccess_OpensUnruledFields_AndRuledOnesByRoleOrTest()
    {
        var access = FieldAccess.Create().Require("cost", "Finance", "Admin").Require("date", u => u.HasClaim("tier", "gold"));

        var anon = access.For(null);
        var finance = access.For(User("ana", "Finance"));
        var gold = access.For(new ClaimsPrincipal(new ClaimsIdentity([new Claim("tier", "gold")], "test")));

        Assert.True(anon("name"));
        Assert.False(anon("cost"));
        Assert.False(anon("date:month"));
        Assert.True(finance("COST"));
        Assert.False(finance("date"));
        Assert.True(gold("date:year"));
        Assert.True(access.IsRestricted("date:week"));
        Assert.False(access.IsRestricted("name"));
        Assert.Throws<ArgumentException>(() => FieldAccess.Create().Require("x"));
    }

    private sealed record Item(int Id, string Name, decimal Cost);

    [Fact]
    public void TableQueryRestrict_DropsTheSortsAndSearchesOfHiddenFields()
    {
        var query = TableQuery<Item>.By(r => r.Id)
            .Sort("name", r => r.Name).Sort("cost", r => r.Cost)
            .SearchText("name", r => r.Name).SearchText("cost", r => r.Cost.ToString())
            .DefaultSort("cost", descending: true);

        var open = query.Restrict(f => f != "cost");

        Assert.True(open.CanSort("name"));
        Assert.False(open.CanSort("cost"));
        Assert.True(query.CanSort("cost")); // the original is unchanged
        Item[] rows = [new(1, "a", 5m), new(2, "b", 1m), new(3, "c", 9m)];
        var search = new ProTableRequest { Searches = new Dictionary<string, string?> { ["cost"] = "9", ["name"] = "c" } };
        Assert.Equal([3], query.Filter(rows.AsQueryable(), search).Select(r => r.Id));
        search = new ProTableRequest { Searches = new Dictionary<string, string?> { ["cost"] = "9" } };
        Assert.Equal([1, 2, 3], open.Filter(rows.AsQueryable(), search).Select(r => r.Id)); // a search on a hidden field is ignored
    }

    [Fact]
    public void PivotQueryRestrict_HidesTheSampleMeasure_FromAnyoneButEditors()
    {
        var anon = PivotTablePage.Pivot.Restrict(PivotTablePage.Access.For(null));
        var admin = PivotTablePage.Pivot.Restrict(PivotTablePage.Access.For(User("ana", "Admin")));

        Assert.DoesNotContain(anon.Measures, m => m.Key == "largest");
        Assert.Contains(admin.Measures, m => m.Key == "largest");
        Assert.Equal(PivotTablePage.Pivot.Fields.Count, anon.Fields.Count);
        // A URL that asks for the measure gets the pivot without it.
        var state = anon.State(Q("measures=largest,count"));
        Assert.Equal(["count"], state.Measures);
    }

    [Fact]
    public void GridSchemaAllowed_KeepsTheColumnsTheUserMaySee()
    {
        GridColumn[] columns = [new() { Key = "name" }, new() { Key = "cost" }, new() { Key = "qty" }];

        Assert.Equal(["name", "qty"], GridSchema.Allowed(columns, f => f != "cost").Select(c => c.Key));
    }

    [Fact]
    public async Task EfStore_KeepsStateInTheDatabase_AcrossInstances()
    {
        var file = Path.Combine(Path.GetTempPath(), $"ui-state-{Guid.NewGuid():N}.db");
        var services = new ServiceCollection();
        services.AddDbContextFactory<UiStateDbContext>(o => o.UseSqlite($"Data Source={file};Pooling=False"));
        using var provider = services.BuildServiceProvider();
        try
        {
            provider.EnsureUiStateDatabase();
            var factory = provider.GetRequiredService<IDbContextFactory<UiStateDbContext>>();
            var views = new SavedViews(new EfUiStateStore(factory));
            Assert.Null(await views.SaveAsync("ana", "/p", "Cities", Q("rows=city"), isDefault: true, cancellationToken: CancellationToken.None));
            await new EfUiStateStore(factory).SetAsync("", "views:/p", "x", cancellationToken: CancellationToken.None); // anonymous: nothing kept

            // A new store (as after a restart) reads the same rows.
            var again = new SavedViews(new EfUiStateStore(factory));
            Assert.Equal(new SavedView("Cities", "rows=city", true), Assert.Single(await again.ListAsync("ana", "/p", cancellationToken: CancellationToken.None)));
            await again.DeleteAsync("ana", "/p", "Cities", cancellationToken: CancellationToken.None);
            Assert.Null(await new EfUiStateStore(factory).GetAsync("ana", "views:/p", cancellationToken: CancellationToken.None));
            await using var db = await factory.CreateDbContextAsync(cancellationToken: CancellationToken.None);
            Assert.Equal(0, await db.UiState.CountAsync(cancellationToken: CancellationToken.None));
        }
        finally
        {
            File.Delete(file);
        }
    }

    // Component ---------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Component_SavesTheCurrentView_AndOpensTheDefault()
    {
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<IUiStateStore, MemoryUiStateStore>();
        ctx.Services.AddScoped<SavedViews>();
        ctx.AddAuthorization().SetAuthorized("ana");
        var nav = ctx.Services.GetRequiredService<NavigationManager>();
        nav.NavigateTo("/tables/p?rows=city&page=2");

        var cut = ctx.Render<AppSavedViews>(p => p.Add(c => c.Page, "/p").Add(c => c.Id, "pv"));
        Assert.Contains("No saved views yet.", cut.Markup);

        await cut.Find(".m-views-save input[type=text]").InputAsync(new ChangeEventArgs { Value = "Cities" });
        await cut.Find(".m-views-save input[type=checkbox]").ChangeAsync(new ChangeEventArgs { Value = true });
        await cut.Find(".m-views-save button").ClickAsync(new());

        var views = ctx.Services.GetRequiredService<SavedViews>();
        Assert.Equal(new SavedView("Cities", "rows=city", true), Assert.Single(await views.ListAsync("ana", "/p", cancellationToken: CancellationToken.None)));
        cut.WaitForAssertion(() => Assert.Contains("aria-current=\"true\"", cut.Markup));
        Assert.Contains("<span>Cities</span>", cut.Find("summary").InnerHtml);

        // A visit with no layout in the URL opens the default view.
        nav.NavigateTo("/tables/p");
        ctx.Render<AppSavedViews>(p => p.Add(c => c.Page, "/p"));
        Assert.Equal("http://localhost/tables/p?rows=city", nav.Uri);
    }

    [Fact]
    public void Component_ForAnonymous_OffersNoSave()
    {
        using var ctx = new BunitContext();
        ctx.Services.AddSingleton<IUiStateStore, MemoryUiStateStore>();
        ctx.Services.AddScoped<SavedViews>();
        ctx.AddAuthorization();

        var cut = ctx.Render<AppSavedViews>();

        Assert.Contains("Sign in to save views.", cut.Markup);
        Assert.Empty(cut.FindAll(".m-views-save"));
    }
}
