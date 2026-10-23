using Modulus.Ui.Abstractions.Dashboard;
using Modulus.Ui.Abstractions.State;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The dashboard layout's model: the default and resolved layouts, the moves and sizes, the JSON, and the saved layout per user.</summary>
public class DashboardModelTests
{
    private static readonly DashboardTileSpec[] Specs =
    [
        new("revenue", "Revenue", 3, 2),
        new("orders", "Orders", 3, 2),
        new("activity", "Recent activity", 12, 3),
    ];

    private static readonly IReadOnlyList<DashboardPlacement> Default = DashboardModel.Default(Specs);

    [Fact]
    public void TheDefault_IsTheSpecsInOrder_AtTheirDefaultSizes()
    {
        Assert.Equal(["revenue", "orders", "activity"], Default.Select(p => p.Id));
        Assert.Equal(12, Default[2].Width);
        Assert.Equal(3, Default[2].Height);
    }

    [Fact]
    public void Resolve_KeepsTheSavedOrderAndSizes_DropsUnknownTiles_AddsNewOnes_AndClampsSizes()
    {
        var saved = new[] { new DashboardPlacement("orders", 6, 1), new DashboardPlacement("gone", 2, 2), new DashboardPlacement("revenue", 99, 0) };

        var layout = DashboardModel.Resolve(Specs, saved);

        Assert.Equal(["orders", "revenue", "activity"], layout.Select(p => p.Id));
        Assert.Equal(new DashboardPlacement("orders", 6, 1), layout[0]);
        Assert.Equal(new DashboardPlacement("revenue", 12, 1), layout[1]);
        Assert.Equal(new DashboardPlacement("activity", 12, 3), layout[2]);
    }

    [Fact]
    public void Resolve_IgnoresARepeatedTile()
    {
        var layout = DashboardModel.Resolve(Specs, [new("orders", 4, 2), new("orders", 8, 4)]);

        Assert.Equal(4, layout.Single(p => p.Id == "orders").Width);
    }

    [Fact]
    public void Apply_SwapsWithTheNeighbour_ForEarlierAndLater_AndNotPastTheEnds()
    {
        var first = DashboardModel.Apply(Default, "earlier", "revenue");
        Assert.Equal(Default.Select(p => p.Id), first.Select(p => p.Id));

        var later = DashboardModel.Apply(Default, "later", "revenue");
        Assert.Equal(["orders", "revenue", "activity"], later.Select(p => p.Id));

        var last = DashboardModel.Apply(Default, "later", "activity");
        Assert.Equal(Default.Select(p => p.Id), last.Select(p => p.Id));
    }

    [Fact]
    public void Apply_ChangesTheColumnsAndRows_WithinTheGrid()
    {
        var wide = DashboardModel.Apply(Default, "wider", "activity");
        Assert.Equal(12, wide.Single(p => p.Id == "activity").Width);

        var narrow = Enumerable.Range(0, 10).Aggregate(Default, (l, _) => DashboardModel.Apply(l, "narrower", "revenue"));
        Assert.Equal(1, narrow.Single(p => p.Id == "revenue").Width);

        var tall = Enumerable.Range(0, 10).Aggregate(Default, (l, _) => DashboardModel.Apply(l, "taller", "revenue"));
        Assert.Equal(DashboardModel.MaxHeight, tall.Single(p => p.Id == "revenue").Height);

        var short_ = Enumerable.Range(0, 10).Aggregate(Default, (l, _) => DashboardModel.Apply(l, "shorter", "revenue"));
        Assert.Equal(1, short_.Single(p => p.Id == "revenue").Height);
    }

    [Fact]
    public void Apply_LeavesTheLayoutAlone_ForAnUnknownOpOrTile()
    {
        Assert.Equal(Default, DashboardModel.Apply(Default, "explode", "revenue"));
        Assert.Equal(Default, DashboardModel.Apply(Default, "wider", "nope"));
    }

    [Fact]
    public void View_TitlesTheTiles_AndMarksTheFirstAndLast()
    {
        var view = DashboardModel.View("home", Specs, Default);

        Assert.Equal("home", view.Id);
        Assert.Equal(["Revenue", "Orders", "Recent activity"], view.Tiles.Select(t => t.Title));
        Assert.True(view.Tiles[0].First);
        Assert.True(view.Tiles[^1].Last);
        Assert.False(view.Tiles[1].First);
    }

    [Fact]
    public void Json_RoundTrips()
    {
        var json = DashboardModel.ToJson(Default);

        Assert.Equal(Default, DashboardModel.ParseJson(json));
        Assert.StartsWith("{\"tiles\":[{\"id\":\"revenue\"", json);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("[1,2]")]
    [InlineData("{\"tiles\":{}}")]
    public void ParseJson_RefusesWhatIsNotALayout(string? json) =>
        Assert.Null(DashboardModel.ParseJson(json));

    [Fact]
    public void ParseJson_DropsBadEntries_AndClampsTheRest()
    {
        const string json = """{"tiles":[{"id":"ok","width":40,"height":0},{"id":"bad id!","width":3,"height":2},{"id":"x","width":"3","height":2},{"width":3,"height":2}]}""";

        var layout = DashboardModel.ParseJson(json);

        Assert.Equal([new DashboardPlacement("ok", 12, 1)], layout);
    }

    [Fact]
    public void IdsAreLettersDigitsDashesAndUnderscores_UpTo40()
    {
        Assert.True(DashboardModel.IsValidId("sales-2026_q3"));
        Assert.False(DashboardModel.IsValidId(new string('a', 41)));
        Assert.False(DashboardModel.IsValidId("a/b"));
        Assert.False(DashboardModel.IsValidId(""));
    }

    [Fact]
    public async Task ASignedInUsersLayout_IsSavedAndComesBack()
    {
        var store = new MemoryUiStateStore();
        var moved = DashboardModel.Apply(Default, "later", "revenue");

        Assert.True(await DashboardModel.SaveAsync(store, "u1", "home", moved, cancellationToken: CancellationToken.None));

        var back = await DashboardModel.LoadAsync(store, "u1", "home", Specs, cancellationToken: CancellationToken.None);
        Assert.Equal(["orders", "revenue", "activity"], back.Select(p => p.Id));
        Assert.Equal(Default.Select(p => p.Id), (await DashboardModel.LoadAsync(store, "u2", "home", Specs, cancellationToken: CancellationToken.None)).Select(p => p.Id));
    }

    [Fact]
    public async Task AnAnonymousVisitor_KeepsNothing()
    {
        var store = new MemoryUiStateStore();

        Assert.False(await DashboardModel.SaveAsync(store, "", "home", DashboardModel.Apply(Default, "later", "revenue"), cancellationToken: CancellationToken.None));
        Assert.Equal(Default, await DashboardModel.LoadAsync(store, "", "home", Specs, cancellationToken: CancellationToken.None));
    }

    [Fact]
    public async Task Clear_ForgetsTheSavedLayout()
    {
        var store = new MemoryUiStateStore();
        await DashboardModel.SaveAsync(store, "u1", "home", DashboardModel.Apply(Default, "later", "revenue"), cancellationToken: CancellationToken.None);

        await DashboardModel.ClearAsync(store, "u1", "home", cancellationToken: CancellationToken.None);

        Assert.Equal(["revenue", "orders", "activity"], (await DashboardModel.LoadAsync(store, "u1", "home", Specs, cancellationToken: CancellationToken.None)).Select(p => p.Id));
    }
}
