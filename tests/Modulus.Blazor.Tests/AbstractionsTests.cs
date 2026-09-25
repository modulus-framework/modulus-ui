using Modulus.Ui.Abstractions;
using Modulus.Ui.Abstractions.Charts;
using Modulus.Ui.Abstractions.Navigation;
using Xunit;

namespace Modulus.Blazor.Tests;

public class MenuProviderTests
{
    private sealed class Contributor(params MenuItemDefinition[] items) : IMenuContributor
    {
        public Task ConfigureMenuAsync(MenuConfigurationContext context)
        {
            foreach (var item in items) context.Add(item);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SortsByOrder_StableAcrossContributors_AndRecursesIntoChildren()
    {
        var settings = new MenuItemDefinition("settings", "Settings", "settings") { Order = 90 };
        settings.AddChild(new MenuItemDefinition("b", "B", "settings/b") { Order = 2 });
        settings.AddChild(new MenuItemDefinition("a", "A", "settings/a") { Order = 1 });

        var provider = new MenuProvider(
        [
            new Contributor(settings, new MenuItemDefinition("customers", "Customers", "customers") { Order = 10 }),
            new Contributor(new MenuItemDefinition("dashboard", "Dashboard", "") { Order = 0 },
                            new MenuItemDefinition("orders", "Orders", "orders") { Order = 10 }),
        ], new EmptyServices());

        var menu = await provider.GetMainMenuAsync();

        Assert.Equal(["dashboard", "customers", "orders", "settings"], menu.Select(m => m.Key));
        Assert.Equal(["a", "b"], menu[^1].Children.Select(m => m.Key));
    }

    private sealed class EmptyServices : IServiceProvider
    {
        public object? GetService(Type serviceType) => null;
    }
}

public class ProTableTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-5, 1)]
    [InlineData(3, 3)]
    public void Current_IsAtLeastOne(int value, int expected) =>
        Assert.Equal(expected, new ProTableRequest { Current = value }.Current);

    [Theory]
    [InlineData(0, 1)]
    [InlineData(50, 50)]
    [InlineData(100_000, 200)]
    public void PageSize_IsClamped(int value, int expected) =>
        Assert.Equal(expected, new ProTableRequest { PageSize = value }.PageSize);

    [Fact]
    public void SortBy_ThenToPage_ReturnsRequestedSlice()
    {
        var request = new ProTableRequest { Current = 2, PageSize = 3, SortField = "value", SortOrder = SortDirection.Descending };

        var page = Enumerable.Range(1, 10).SortBy(request, "Value", x => x).ToPage(request);

        Assert.Equal(10, page.Total);
        Assert.Equal([7, 6, 5], page.Data);
    }

    [Fact]
    public void SortBy_IgnoresOtherFields() =>
        Assert.Equal([3, 1, 2], new[] { 3, 1, 2 }.SortBy(new ProTableRequest { SortField = "name", SortOrder = SortDirection.Ascending }, "value", x => x));
}

public class ChartGeometryTests
{
    [Theory]
    [InlineData(87, new double[] { 0, 20, 40, 60, 80, 100 })]
    [InlineData(1000, new double[] { 0, 200, 400, 600, 800, 1000 })]
    [InlineData(0, new double[] { 0 })]
    public void NiceTicks_CoverMaxWithRoundSteps(double max, double[] expected) =>
        Assert.Equal(expected, ChartGeometry.NiceTicks(max));

    [Theory]
    [InlineData(950, "950")]
    [InlineData(1200, "1.2k")]
    [InlineData(3_400_000, "3.4M")]
    public void Compact_Abbreviates(double value, string expected) =>
        Assert.Equal(expected, ChartGeometry.Compact(value));

    [Fact]
    public void Sparkline_PlacesPointsAtSlotCentres()
    {
        var (line, area) = ChartGeometry.Sparkline([0, 10], 100, 50, padTop: 0);

        Assert.Equal("M25,50L75,0", line);
        Assert.Equal("M25,50L75,0L75,50L25,50Z", area);
    }

    [Theory]
    [InlineData(50, 200, "25")]
    [InlineData(500, 200, "100")]
    [InlineData(5, 0, "0")]
    public void Percent_IsClamped(double value, double max, string expected) =>
        Assert.Equal(expected, ChartGeometry.Percent(value, max));
}
