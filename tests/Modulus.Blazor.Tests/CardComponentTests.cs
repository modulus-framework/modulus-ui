using Bunit;
using Modulus.Blazor.Components.DataDisplay;
using Xunit;

namespace Modulus.Blazor.Tests;

public class CardComponentTests : TestContext
{
    [Fact]
    public void AppCard_Renders()
    {
        // Arrange & Act
        var cut = RenderComponent<AppCard>();

        // Assert
        Assert.NotNull(cut);
        Assert.Contains("ant-card", cut.Markup);
    }

    [Fact]
    public void AppCard_RendersBordered()
    {
        // Arrange & Act
        var cut = RenderComponent<AppCard>(parameters => parameters
            .Add(p => p.Bordered, true));

        // Assert
        Assert.True(cut.Instance.Bordered);
    }

    [Fact]
    public void AppCard_RendersHoverable()
    {
        // Arrange & Act
        var cut = RenderComponent<AppCard>(parameters => parameters
            .Add(p => p.Hoverable, true));

        // Assert
        Assert.True(cut.Instance.Hoverable);
    }

    [Fact]
    public void AppCard_RendersWithClass()
    {
        // Arrange & Act
        var cut = RenderComponent<AppCard>(parameters => parameters
            .Add(p => p.Class, "custom-class"));

        // Assert
        Assert.Equal("custom-class", cut.Instance.Class);
    }
}
