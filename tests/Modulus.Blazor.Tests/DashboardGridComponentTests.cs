using AngleSharp.Dom;
using Bunit;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Modulus.Blazor.Components.Pro;
using Modulus.Ui.Abstractions.Dashboard;
using Modulus.Ui.Abstractions.State;
using Xunit;

namespace Modulus.Blazor.Tests;

/// <summary>The Blazor DashboardGrid: the tiles draw with their bodies, the buttons change a tile's place and size, and the ends are off.</summary>
public class DashboardGridComponentTests : AsyncBunitContext
{
    private static readonly DashboardTileSpec[] Specs =
    [
        new("revenue", "Revenue", 3, 2),
        new("orders", "Orders", 3, 2),
    ];

    public DashboardGridComponentTests()
    {
        Services.AddSingleton<IUiStateStore>(new MemoryUiStateStore());
    }

    private IRenderedComponent<DashboardGrid> Mount() =>
        Render<DashboardGrid>(p => p
            .Add(x => x.Id, "home")
            .Add(x => x.Tiles, Specs)
            .Add(x => x.Tile, tile => b => b.AddContent(0, $"body of {tile.Id}")));

    private static IElement Tile(IRenderedComponent<DashboardGrid> cut, string title) =>
        cut.FindAll("section.m-dash-tile").Single(s => s.GetAttribute("aria-label") == title);

    [Fact]
    public void DrawsEachTile_WithItsBody_AndItsSize()
    {
        var cut = Mount();

        Assert.Equal(2, cut.FindAll("section.m-dash-tile").Count);
        Assert.Contains("body of orders", cut.Markup);
        Assert.Contains("--w:3;--h:2", Tile(cut, "Revenue").GetAttribute("style"));
    }

    [Fact]
    public void Wider_WidensTheTile()
    {
        var cut = Mount();

        Tile(cut, "Revenue").QuerySelector("button[aria-label='Make Revenue wider']")!.Click();

        Assert.Contains("--w:4", Tile(cut, "Revenue").GetAttribute("style"));
    }

    [Fact]
    public void Later_MovesTheTileBehindItsNeighbour()
    {
        var cut = Mount();

        Tile(cut, "Revenue").QuerySelector("button[aria-label='Move Revenue later']")!.Click();

        Assert.Equal(["Orders", "Revenue"], cut.FindAll("section.m-dash-tile").Select(s => s.GetAttribute("aria-label")));
    }

    [Fact]
    public void TheEnds_AreOff()
    {
        var cut = Mount();

        Assert.True(Tile(cut, "Revenue").QuerySelector("button[aria-label='Move Revenue earlier']")!.HasAttribute("disabled"));
        Assert.True(Tile(cut, "Orders").QuerySelector("button[aria-label='Move Orders later']")!.HasAttribute("disabled"));
    }

    [Fact]
    public void ASignedOutVisitor_IsTold_ToSignIn()
    {
        var cut = Mount();

        Assert.Contains("Sign in to keep this layout.", cut.Markup);
    }

    [Fact]
    public void Reset_PutsTheDefaultBack()
    {
        var cut = Mount();
        Tile(cut, "Revenue").QuerySelector("button[aria-label='Move Revenue later']")!.Click();

        cut.Find("button.ant-btn-default, .m-dash-foot button").Click();

        Assert.Equal(["Revenue", "Orders"], cut.FindAll("section.m-dash-tile").Select(s => s.GetAttribute("aria-label")));
    }
}
